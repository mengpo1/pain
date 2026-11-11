using Game.Core.Battle;
using Game.Core.Battle.Ai;
using Game.Core.Battle.Movement;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;
using Game.Core.Saves;
using Game.Desktop.Graphics;
using Game.Desktop.Input;
using Game.Desktop.Tactical;
using Game.Desktop.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Screens;

/// <summary>
/// The Battlescape: shows the battle, plays its events one by one, turns the player's clicks
/// into commands and lets the AI play the other sides. It never changes the battle state itself.
/// </summary>
public sealed class BattleScreen : Screen
{
    /// <summary>Most simulation steps resolved in one frame when nothing visible happens (AI turns).</summary>
    private const int MaxStepsPerFrame = 400;

    private const float EdgeScrollMargin = 6f;

    private readonly Battle _battle;
    private readonly BattleView _view = new();
    private readonly BattlePlayback _playback = new();
    private readonly BattleRenderer _renderer;
    private readonly BattleHud _hud;
    private readonly AiController _ai;
    private readonly BattleEventPresenter _presenter;
    private HudAction? _hudAction;
    private bool _paused;
    private float _pausedAt;
    private float _time;
    private float _endDelay = 1.6f;

    private BattleScreen(GameHost host, Battle battle) : base(host)
    {
        _battle = battle;
        _renderer = new BattleRenderer(Session);
        _view.Reserve = battle.PlayerReserve;
        _hud = new BattleHud(Session);
        _ai = new AiController(battle);
        _presenter = new BattleEventPresenter(Session, host.Settings, battle, _view, _playback, _hud, () => SelectNext(previous: false));
        _playback.Enqueue(battle.TakeEvents());
        SelectNext(previous: false);
        if (_view.Selected is { } unit) CenterOn(unit.Position);
        Session.Audio.PlayMusic(Session.Content.Game.Sounds.BattleMusic);
    }

    public static BattleScreen Start(GameHost host, MissionDefinition mission, ulong seed)
    {
        Log.Info($"Starting mission '{mission.Id}' with seed {seed}.");
        return new BattleScreen(host, BattleFactory.Create(host.Session.Content.Rules, mission, seed));
    }

    /// <summary>Path of the quick save (one per game pack).</summary>
    public static string QuickSavePath(GameHost host) =>
        Path.Combine(host.Paths.SavesDirectory, $"{host.Session.Content.GamePack.Manifest.Id}-quicksave.json");

    /// <summary>Loads the quick save; null (with a logged reason) if it cannot be loaded.</summary>
    public static BattleScreen? Resume(GameHost host, out string? error)
    {
        error = null;
        try
        {
            Battle battle = BattleSaves.Read(QuickSavePath(host), host.Session.Content);
            Log.Info($"Loaded {QuickSavePath(host)}.");
            return new BattleScreen(host, battle);
        }
        catch (SaveException exception)
        {
            Log.Error(exception.Message);
            error = exception.Message;
            return null;
        }
    }

    private void SaveGame()
    {
        if (!Idle || _battle.IsOver) return;
        try
        {
            BattleSaves.Write(_battle, Session.Content, QuickSavePath(Host));
            _hud.Show(Texts.Get("ui.saved"), 2f);
            Log.Info($"Saved to {QuickSavePath(Host)}.");
        }
        catch (SaveException exception)
        {
            Log.Error(exception.Message);
            _hud.Show(Texts.Format("error.save_failed", ("message", exception.Message)), 5f);
        }
    }

    private void LoadGame()
    {
        if (!File.Exists(QuickSavePath(Host))) return;
        if (Resume(Host, out string? error) is { } screen)
        {
            Host.SetScreen(screen);
            return;
        }

        _hud.Show(Texts.Format("error.load_failed", ("message", error ?? "")), 6f);
    }

    private bool PlayerControls => _battle.CurrentFaction == Faction.Player && !Host.Options.Autoplay;

    private Localizer Texts => Session.Texts;

    // ------------------------------------------------------------------ update

    public override void Update(float dt)
    {
        _time += dt;
        _playback.Update(dt);
        _hud.Update(dt);
        UpdateCamera(dt);
        if (_paused) return;

        if (_hudAction is { } action)
        {
            _hudAction = null;
            HandleHud(action);
        }

        RunSimulation();
        if (_battle.IsOver)
        {
            if (!_playback.IsAnimating && !_playback.HasPending && (_endDelay -= dt) <= 0)
                Host.SetScreen(new DebriefScreen(Host, _battle));
            return;
        }

        UpdateHover();
        if (Idle && PlayerControls)
        {
            // Leave aiming once the unit can no longer pay for another shot of that mode.
            if (_view.Targeting is { Mode: not AttackMode.Throw } targeting && (_view.Selected is not { IsActive: true } shooter
                || shooter.Tu < Core.Battle.Combat.CombatRules.AttackTu(shooter, targeting.Weapon.Definition, targeting.Mode)))
                _view.Targeting = null;
            HandlePlayerInput();
        }
    }

    private bool Idle => !_playback.IsAnimating && !_playback.HasPending && !_battle.IsBusy;

    /// <summary>Plays pending events, advances the simulation, and lets the AI act, until something is animated.</summary>
    private void RunSimulation()
    {
        for (int i = 0; i < MaxStepsPerFrame && !_playback.IsAnimating; i++)
        {
            if (_playback.Next() is { } battleEvent)
            {
                _presenter.Play(battleEvent);
                continue;
            }

            if (_battle.IsBusy)
            {
                _battle.Advance();
                _playback.Enqueue(_battle.TakeEvents());
                continue;
            }

            if (_battle.IsOver || PlayerControls) break;
            BattleCommand command = _ai.NextCommand();
            CommandResult result = _battle.Execute(command);
            if (!result.Accepted) _ai.Refused(command);
            _playback.Enqueue(_battle.TakeEvents());
        }
    }

    private void Execute(BattleCommand command)
    {
        CommandResult result = _battle.Execute(command);
        _playback.Enqueue(_battle.TakeEvents());
        if (!result.Accepted && result.ReasonKey is { } reason) _hud.Show(Texts.Format(reason, ("value", result.Value ?? 0)));
        else Session.Audio.Play(Session.Content.Game.Sounds.Click, volume: 0.5f);
    }

    // ------------------------------------------------------------------ player input

    private void HandlePlayerInput()
    {
        InputState input = Host.Input;
        if (input.Pressed(GameAction.NextUnit)) SelectNext(previous: input.Shift);
        if (input.Pressed(GameAction.Kneel)) HandleHud(new ToggleKneelAction());
        if (input.Pressed(GameAction.Reload) && _view.Selected is { } shooter && Inventory.MainWeapon(shooter) is { } weapon) HandleHud(new ReloadAction(weapon));
        if (input.Pressed(GameAction.EndTurn)) HandleHud(new EndTurnAction());
        if (input.Pressed(GameAction.CenterOnUnit)) HandleHud(new CenterOnSelected());
        if (input.Pressed(GameAction.Inventory)) HandleHud(new ToggleInventory());
        if (input.Pressed(GameAction.QuickSave)) SaveGame();
        if (input.Pressed(GameAction.QuickLoad)) LoadGame();
        if (input.Pressed(GameAction.Cancel))
        {
            if (_view.Targeting is not null || _view.ActionMenu is not null)
            {
                _view.Targeting = null;
                _view.ActionMenu = null;
            }
            else
            {
                Pause();
            }
        }

        if (Session.Ui.MouseOverUi || _view.Hovered is not { } tile) return;
        BattleUnit? selected = _view.Selected;
        if (input.LeftClicked)
        {
            _view.ActionMenu = null;
            if (_view.Targeting is { } targeting && selected is not null)
            {
                if (targeting.Mode == AttackMode.Throw)
                {
                    Execute(new ThrowCommand(selected.Id, targeting.Weapon.Id, tile));
                    _view.Targeting = null;
                }
                else
                {
                    Execute(new AttackCommand(selected.Id, targeting.Weapon.Id, targeting.Mode, tile));
                }
            }
            else if (_battle.UnitAt(tile) is { Faction: Faction.Player, IsActive: true } clicked)
            {
                _view.Selected = clicked;
            }
            else if (selected is not null)
            {
                Execute(new MoveCommand(selected.Id, tile));
            }
        }
        else if (input.RightClicked)
        {
            if (_view.Targeting is not null || _view.ActionMenu is not null)
            {
                _view.Targeting = null;
                _view.ActionMenu = null;
            }
            else if (selected is not null && tile != selected.Position)
            {
                Execute(new TurnCommand(selected.Id, Directions.Towards(selected.Position, tile)));
            }
        }
    }

    private void HandleHud(HudAction action)
    {
        BattleUnit? unit = _view.Selected;
        switch (action)
        {
            case SelectNextUnit next:
                SelectNext(next.Previous);
                break;
            case ToggleKneelAction when unit is not null && PlayerControls && Idle:
                Execute(new KneelCommand(unit.Id));
                break;
            case ReloadAction reload when unit is not null && PlayerControls && Idle:
                Execute(new ReloadCommand(unit.Id, reload.Weapon.Id));
                break;
            case ChangeLevel change:
                _view.Level = Math.Clamp(_view.Level + change.Delta, 0, _battle.Map.Height - 1);
                break;
            case CenterOnSelected when unit is not null:
                CenterOn(unit.Position);
                break;
            case EndTurnAction when PlayerControls && Idle:
                _view.Targeting = null;
                _view.ActionMenu = null;
                Execute(new EndTurnCommand());
                break;
            case OpenPauseMenu:
                Pause();
                break;
            case OpenActionMenu open:
                _view.ActionMenu = _view.ActionMenu == open.Item ? null : open.Item;
                _view.Targeting = null;
                break;
            case ChooseAttack choose:
                _view.ActionMenu = null;
                _view.Targeting = (choose.Weapon, choose.Mode);
                break;
            case FocusUnit focus:
                CenterOn(focus.Unit.Position);
                break;
            case CycleReserve:
                _battle.PlayerReserve.Mode = (ReserveMode)(((int)_battle.PlayerReserve.Mode + 1) % 4);
                _view.Path = null;
                break;
            case ToggleKneelReserve:
                _battle.PlayerReserve.Kneel = !_battle.PlayerReserve.Kneel;
                _view.Path = null;
                break;
            case ToggleInventory:
                _view.InventoryOpen = !_view.InventoryOpen;
                _view.ActionMenu = null;
                break;
            case MoveItem move when unit is not null && PlayerControls && Idle:
                Execute(new MoveItemCommand(unit.Id, move.Item.Id, move.Slot));
                break;
            case PrimeGrenade prime when unit is not null && PlayerControls && Idle:
                _view.ActionMenu = null;
                Execute(new PrimeCommand(unit.Id, prime.Grenade.Id, prime.Fuse));
                break;
        }
    }

    private void Pause()
    {
        _paused = true;
        _pausedAt = _time;
    }

    private void SelectNext(bool previous)
    {
        List<BattleUnit> units = _battle.ActiveUnitsOf(Faction.Player).OrderBy(u => u.Id).ToList();
        if (units.Count == 0)
        {
            _view.Selected = null;
            return;
        }

        int index = _view.Selected is { } current ? units.IndexOf(current) : -1;
        index = previous ? (index <= 0 ? units.Count - 1 : index - 1) : (index + 1) % units.Count;
        _view.Selected = units[index];
        _view.Targeting = null;
        _view.ActionMenu = null;
        CenterOn(units[index].Position);
    }

    // ------------------------------------------------------------------ camera and hover

    private void CenterOn(Position p)
    {
        _view.Camera.Center = Iso.TileCenter(p);
        _view.Level = Math.Max(_view.Level, p.Z);
        if (p.Z < _view.Level && _battle.UnitAt(p)?.Faction == Faction.Player) _view.Level = p.Z;
    }

    private void UpdateCamera(float dt)
    {
        InputState input = Host.Input;
        Rectangle screen = Host.GraphicsDevice.Viewport.Bounds;
        _view.Camera.Viewport = new Rectangle(0, 0, screen.Width, screen.Height - _hud.PanelHeight);
        float speed = Host.Settings.ScrollSpeed * dt / _view.Camera.Zoom;
        var move = Vector2.Zero;
        if (input.Held(GameAction.ScrollUp)) move.Y -= 1;
        if (input.Held(GameAction.ScrollDown)) move.Y += 1;
        if (input.Held(GameAction.ScrollLeft)) move.X -= 1;
        if (input.Held(GameAction.ScrollRight)) move.X += 1;
        Point mouse = input.MousePosition;
        if (Host.Settings.EdgeScroll && Host.IsActive && screen.Contains(mouse))
        {
            if (mouse.X < EdgeScrollMargin) move.X -= 1;
            if (mouse.X > screen.Width - EdgeScrollMargin) move.X += 1;
            if (mouse.Y < EdgeScrollMargin) move.Y -= 1;
            if (mouse.Y > screen.Height - EdgeScrollMargin) move.Y += 1;
        }

        if (input.MouseHeld(MouseButton.Middle)) _view.Camera.Center -= input.MouseDelta.ToVector2() / _view.Camera.Zoom;
        _view.Camera.Center += move * speed;
        if (input.ScrollDelta != 0 && !Session.Ui.MouseOverUi) _view.Camera.ZoomBy(input.ScrollDelta > 0 ? 1.15f : 1f / 1.15f);
        if (input.Pressed(GameAction.ZoomIn)) _view.Camera.ZoomBy(1.15f);
        if (input.Pressed(GameAction.ZoomOut)) _view.Camera.ZoomBy(1f / 1.15f);
        if (input.Pressed(GameAction.LevelUp)) _view.Level = Math.Min(_battle.Map.Height - 1, _view.Level + 1);
        if (input.Pressed(GameAction.LevelDown)) _view.Level = Math.Max(0, _view.Level - 1);
        if (input.Pressed(GameAction.ToggleDebug)) _view.RevealAll = !_view.RevealAll;
    }

    private void UpdateHover()
    {
        Point mouse = Host.Input.MousePosition;
        if (!_view.Camera.Viewport.Contains(mouse))
        {
            _view.Hovered = null;
            _view.Path = null;
            return;
        }

        Vector2 world = _view.Camera.ScreenToWorld(mouse);
        (int x, int y) = Iso.WorldToTile(world, _view.Level);
        var tile = new Position(x, y, _view.Level);
        if (!_battle.Map.InBounds(tile))
        {
            _view.Hovered = null;
            _view.Path = null;
            return;
        }

        if (_view.Hovered != tile || _view.Path is null)
        {
            _view.Hovered = tile;
            _view.Path = _view.Selected is { IsActive: true } unit && PlayerControls && _view.Targeting is null && _battle.UnitAt(tile) is null
                ? _battle.FindPath(unit, tile)
                : null;
        }
    }

    // ------------------------------------------------------------------ drawing

    public override void Draw(SpriteBatch batch)
    {
        UiContext ui = Session.Ui;
        batch.GraphicsDevice.Clear(Color.Black);
        _renderer.Draw(batch, _battle, _view, _playback, _time);
        HudAction? action = _hud.Draw(batch, _battle, _view, PlayerControls && Idle && !_paused, _playback);
        if (!_paused && action is not null) _hudAction = action;
        DrawPathCost(batch, ui);
        if (_paused) DrawPauseMenu(batch, ui);
    }

    private void DrawPathCost(SpriteBatch batch, UiContext ui)
    {
        if (_view.Path is not { Found: true } path || _view.Hovered is not { } tile || _view.Selected is not { } unit) return;
        Vector2 at = _view.Camera.WorldToScreen(Iso.TileCenter(tile)) + new Vector2(0, ui.S(14));
        batch.Begin(samplerState: SamplerState.PointClamp);
        ui.Text(batch, Texts.Format("ui.path_cost", ("cost", path.TotalTu)), at, 14f, path.TotalTu <= unit.Tu ? ui.Colors.Good : ui.Colors.Bad, Align.Center);
        batch.End();
    }

    private void DrawPauseMenu(SpriteBatch batch, UiContext ui)
    {
        Rectangle screen = batch.GraphicsDevice.Viewport.Bounds;
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(ui.Assets.Pixel, screen, Color.Black * 0.55f);
        var panel = new Rectangle(screen.Center.X - ui.S(170), screen.Center.Y - ui.S(150), ui.S(340), ui.S(296));
        ui.Panel(batch, panel);
        int row = ui.S(68), x = panel.X + ui.S(20), w = panel.Width - ui.S(40), h = ui.S(48);
        if (ui.Button(batch, new Rectangle(x, panel.Y + ui.S(20), w, h), Texts.Get("ui.resume")) || Host.Input.Pressed(GameAction.Back) && _time > _pausedAt)
            _paused = false;
        if (ui.Button(batch, new Rectangle(x, panel.Y + ui.S(20) + row, w, h), Texts.Get("ui.save"), Idle && !_battle.IsOver))
        {
            SaveGame();
            _paused = false;
        }

        if (ui.Button(batch, new Rectangle(x, panel.Y + ui.S(20) + row * 2, w, h), Texts.Get("ui.load"), File.Exists(QuickSavePath(Host))))
            LoadGame();
        if (ui.Button(batch, new Rectangle(x, panel.Y + ui.S(20) + row * 3, w, h), Texts.Get("ui.abandon")))
        {
            Session.Audio.PlayMusic(Session.Content.Game.Sounds.MenuMusic);
            Host.SetScreen(new MenuScreen(Host));
        }

        batch.End();
    }
}
