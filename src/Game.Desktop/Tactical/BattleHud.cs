using Game.Core.Battle;
using Game.Core.Battle.Combat;
using Game.Core.Content;
using Game.Core.Definitions;
using Game.Desktop.Graphics;
using Game.Desktop.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Tactical;

/// <summary>Something the player asked for through the interface; the screen turns it into commands.</summary>
public abstract record HudAction;
public sealed record SelectNextUnit(bool Previous) : HudAction;
public sealed record ToggleKneelAction : HudAction;
public sealed record ReloadAction(BattleItem Weapon) : HudAction;
public sealed record ChangeLevel(int Delta) : HudAction;
public sealed record CenterOnSelected : HudAction;
public sealed record EndTurnAction : HudAction;
public sealed record OpenPauseMenu : HudAction;
public sealed record OpenActionMenu(BattleItem Item) : HudAction;
public sealed record ChooseAttack(BattleItem Weapon, AttackMode Mode) : HudAction;
public sealed record FocusUnit(BattleUnit Unit) : HudAction;

/// <summary>
/// Battlescape interface: selected unit gauges, the two hands with their attack modes, command
/// buttons, turn and objective, enemies in sight. Everything shown is read from the simulation;
/// clicks only produce <see cref="HudAction"/>s.
/// </summary>
public sealed class BattleHud
{
    private static readonly AttackMode[] Modes = [AttackMode.Snap, AttackMode.Aimed, AttackMode.Auto, AttackMode.Melee];
    private readonly GameSession _session;

    public BattleHud(GameSession session) => _session = session;

    public string? Message { get; private set; }
    private float _messageTime;

    public void Show(string message, float seconds = 3f)
    {
        Message = message;
        _messageTime = seconds;
    }

    public void Update(float dt)
    {
        if (_messageTime > 0 && (_messageTime -= dt) <= 0) Message = null;
    }

    /// <summary>Height of the bottom panel in pixels (the map viewport stops above it).</summary>
    public int PanelHeight => _session.Ui.S(132);

    public HudAction? Draw(SpriteBatch batch, Battle battle, BattleView view, bool playerCanAct, BattlePlayback playback)
    {
        UiContext ui = _session.Ui;
        Localizer texts = _session.Texts;
        Rectangle screen = batch.GraphicsDevice.Viewport.Bounds;
        HudAction? action = null;
        batch.Begin(samplerState: SamplerState.PointClamp);

        DrawTopBar(batch, battle, ui, texts, screen);
        action ??= DrawEnemies(batch, battle, view, ui, screen);

        var panel = new Rectangle(0, screen.Height - PanelHeight, screen.Width, PanelHeight);
        ui.Panel(batch, panel);
        if (view.Selected is { } unit) DrawUnit(batch, unit, ui, texts, panel);

        int handsX = panel.X + ui.S(290);
        foreach ((string slot, int index) in new[] { (Inventory.RightHand, 0), (Inventory.LeftHand, 1) })
        {
            var box = new Rectangle(handsX + index * ui.S(170), panel.Y + ui.S(12), ui.S(160), panel.Height - ui.S(24));
            BattleItem? item = view.Selected?.ItemInSlot(slot);
            action ??= DrawHand(batch, battle, view, ui, texts, box, item, playerCanAct);
        }

        action ??= DrawButtons(batch, view, ui, texts, panel, playerCanAct);
        if (view.ActionMenu is { } menuItem && view.Selected is { } owner && menuItem.Owner == owner)
            action ??= DrawActionMenu(batch, battle, owner, menuItem, ui, texts, panel);

        if (Message is { } message)
        {
            Vector2 size = ui.Measure(message, 16f);
            var box = new Rectangle((int)(screen.Center.X - size.X / 2 - ui.S(12)), panel.Y - ui.S(44), (int)size.X + ui.S(24), ui.S(32));
            ui.Panel(batch, box);
            ui.Text(batch, message, new Vector2(screen.Center.X, box.Y + ui.S(6)), 16f, ui.Colors.Accent, Align.Center);
        }

        if (playback.Banner is { } banner)
            ui.Text(batch, banner.Text, new Vector2(screen.Center.X, screen.Height * 0.32f), 34f, banner.Color * Math.Min(1f, banner.Time * 2f), Align.Center);
        if (view.Targeting is not null)
            ui.Text(batch, texts.Get("ui.target_hint"), new Vector2(screen.Center.X, ui.S(44)), 15f, ui.Colors.Hostile, Align.Center);

        batch.End();
        return action;
    }

    private void DrawTopBar(SpriteBatch batch, Battle battle, UiContext ui, Localizer texts, Rectangle screen)
    {
        var bar = new Rectangle(0, 0, screen.Width, ui.S(34));
        ui.Panel(batch, bar);
        MissionDefinition mission = battle.Mission;
        string side = texts.Get(battle.CurrentFaction switch
        {
            Faction.Player => mission.PlayerNameKey,
            Faction.Hostile => mission.HostileNameKey,
            _ => mission.NeutralNameKey,
        });
        string turn = $"{texts.Format("ui.turn", ("turn", battle.Turn))} — {side}";
        ui.Text(batch, turn, new Vector2(ui.S(12), ui.S(7)), 15f, battle.CurrentFaction == Faction.Player ? ui.Colors.Friendly : ui.Colors.Hostile);

        string objective = mission.Objective.Kind switch
        {
            ObjectiveKind.ReachZone => texts.Format("ui.zone", ("inside", Objectives.ZoneProgress(battle, mission.Objective).Inside), ("needed", Objectives.ZoneProgress(battle, mission.Objective).Needed)),
            ObjectiveKind.Survive => texts.Format("ui.survive", ("turns", mission.Objective.Turns)),
            _ => mission.Objective.DescriptionKey.Length > 0 ? texts.Get(mission.Objective.DescriptionKey) : "",
        };
        ui.Text(batch, objective, new Vector2(screen.Center.X, ui.S(7)), 15f, ui.Colors.Text, Align.Center);
    }

    /// <summary>One red square per enemy in sight (OpenXcom's top-right indicators); click to look at it.</summary>
    private static HudAction? DrawEnemies(SpriteBatch batch, Battle battle, BattleView view, UiContext ui, Rectangle screen)
    {
        List<BattleUnit> enemies = battle.Units.Where(u => u.IsActive && u.Faction != Faction.Player && BattleRenderer.IsShown(battle, view, u)).ToList();
        HudAction? action = null;
        int size = ui.S(26);
        for (int i = 0; i < enemies.Count; i++)
        {
            var box = new Rectangle(screen.Width - (i + 1) * (size + ui.S(6)) - ui.S(6), ui.S(42), size, size);
            bool seenBySelected = view.Selected?.VisibleUnits.Contains(enemies[i]) ?? false;
            batch.Draw(ui.Assets.Pixel, box, (seenBySelected ? ui.Colors.Hostile : ui.Colors.Hostile * 0.5f));
            ui.Text(batch, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), new Vector2(box.Center.X, box.Y + ui.S(4)), 13f, Color.White, Align.Center);
            if (box.Contains(ui.Input.MousePosition))
            {
                ui.MouseOverUi = true;
                if (ui.Input.LeftClicked)
                {
                    ui.Input.ClickConsumed = true;
                    action = new FocusUnit(enemies[i]);
                }
            }
        }

        return action;
    }

    private static void DrawUnit(SpriteBatch batch, BattleUnit unit, UiContext ui, Localizer texts, Rectangle panel)
    {
        float x = panel.X + ui.S(14), y = panel.Y + ui.S(10);
        ui.Text(batch, UnitNames.Of(unit, texts), new Vector2(x, y), 17f, ui.Colors.Accent);
        string status = unit.Kneeling ? texts.Get("ui.kneeling") : "";
        if (unit.TotalWounds > 0) status += (status.Length > 0 ? " · " : "") + texts.Format("ui.wounds", ("count", unit.TotalWounds));
        if (status.Length > 0) ui.Text(batch, status, new Vector2(x + ui.S(170), y + ui.S(3)), 12f, ui.Colors.Bad);

        int barWidth = ui.S(256), barHeight = ui.S(18), gap = ui.S(23);
        float by = y + ui.S(30);
        ui.Bar(batch, new Rectangle((int)x, (int)by, barWidth, barHeight), unit.Tu, unit.MaxTu, new Color(60, 150, 70), texts.Get("ui.tu"));
        ui.Bar(batch, new Rectangle((int)x, (int)(by + gap), barWidth, barHeight), unit.Energy, unit.MaxEnergy, new Color(190, 140, 40), texts.Get("ui.energy"));
        ui.Bar(batch, new Rectangle((int)x, (int)(by + gap * 2), barWidth, barHeight), Math.Max(0, unit.Health), unit.MaxHealth, new Color(180, 50, 50), texts.Get("ui.health"));
        ui.Bar(batch, new Rectangle((int)x, (int)(by + gap * 3), barWidth, barHeight), unit.Morale, 100, new Color(70, 110, 190), texts.Get("ui.morale"));
        if (unit.Stun > 0)
        {
            int stunWidth = (int)(barWidth * Math.Clamp(unit.Stun / (float)Math.Max(1, unit.MaxHealth), 0f, 1f));
            batch.Draw(ui.Assets.Pixel, new Rectangle((int)x, (int)(by + gap * 2 + barHeight - ui.S(4)), stunWidth, ui.S(4)), Color.LightGray);
        }
    }

    private HudAction? DrawHand(SpriteBatch batch, Battle battle, BattleView view, UiContext ui, Localizer texts, Rectangle box, BattleItem? item, bool enabled)
    {
        bool open = item is not null && view.ActionMenu == item;
        bool hover = box.Contains(ui.Input.MousePosition);
        if (hover) ui.MouseOverUi = true;
        batch.Draw(ui.Assets.Pixel, box, open ? ui.Colors.Accent * 0.35f : hover ? ui.Colors.PanelBorder * 0.6f : Color.Black * 0.35f);
        Primitives.Rect(batch, ui.Assets.Pixel, new Vector2(box.X, box.Y), new Vector2(box.Width, box.Height), open ? ui.Colors.Accent : ui.Colors.PanelBorder);
        if (item is null)
        {
            ui.Text(batch, texts.Get("ui.no_weapon"), new Vector2(box.Center.X, box.Center.Y - ui.S(8)), 13f, ui.Colors.Muted, Align.Center);
            return null;
        }

        SpriteFrame sprite = _session.Assets.Sprite(item.Definition.InventorySprite.Length > 0 ? item.Definition.InventorySprite : item.Definition.FloorSprite);
        Rectangle source = sprite.Source;
        float scale = Math.Min(box.Width / (float)source.Width, (box.Height - ui.S(40)) / (float)source.Height) * 1.6f;
        batch.Draw(sprite.Texture, new Vector2(box.Center.X, box.Y + ui.S(46)), source, Color.White, 0f, new Vector2(source.Width / 2f, source.Height * 0.7f), scale, SpriteEffects.None, 0f);
        ui.Text(batch, texts.Get(item.Definition.NameKey), new Vector2(box.X + ui.S(6), box.Y + ui.S(4)), 12f, ui.Colors.Text);
        if (item.AmmoSource is { } ammo && item.IsFirearm && !(item.SelfPowered && item.Definition.Rounds == 0))
        {
            int max = ammo.Definition.Rounds;
            ui.Text(batch, texts.Format("ui.rounds", ("rounds", ammo.Rounds), ("max", max)), new Vector2(box.Right - ui.S(6), box.Bottom - ui.S(20)), 13f,
                ammo.Rounds == 0 ? ui.Colors.Bad : ui.Colors.Text, Align.Right);
        }
        else if (item.IsFirearm && !item.SelfPowered)
        {
            ui.Text(batch, "0", new Vector2(box.Right - ui.S(6), box.Bottom - ui.S(20)), 13f, ui.Colors.Bad, Align.Right);
        }

        if (!enabled || !hover || !ui.Input.LeftClicked) return null;
        ui.Input.ClickConsumed = true;
        _ = battle;
        return new OpenActionMenu(item);
    }

    private static HudAction? DrawButtons(SpriteBatch batch, BattleView view, UiContext ui, Localizer texts, Rectangle panel, bool enabled)
    {
        int columns = 4, width = ui.S(118), height = ui.S(46), gap = ui.S(8);
        int x0 = panel.Right - columns * (width + gap) - ui.S(6), y0 = panel.Y + ui.S(14);
        BattleUnit? unit = view.Selected;
        BattleItem? weapon = unit is null ? null : Inventory.MainWeapon(unit);
        (string Label, bool Enabled, HudAction Action)[] buttons =
        [
            (texts.Get("ui.previous_unit"), enabled, new SelectNextUnit(true)),
            (texts.Get("ui.next_unit"), enabled, new SelectNextUnit(false)),
            (texts.Get(unit?.Kneeling == true ? "ui.stand" : "ui.kneel"), enabled && unit is not null, new ToggleKneelAction()),
            (texts.Get("ui.reload"), enabled && weapon is { IsFirearm: true, SelfPowered: false }, new ReloadAction(weapon!)),
            (texts.Get("ui.level_up"), true, new ChangeLevel(1)),
            (texts.Get("ui.level_down"), true, new ChangeLevel(-1)),
            (texts.Get("ui.center"), unit is not null, new CenterOnSelected()),
            (texts.Get("ui.end_turn"), enabled, new EndTurnAction()),
        ];

        HudAction? action = null;
        for (int i = 0; i < buttons.Length; i++)
        {
            var area = new Rectangle(x0 + i % columns * (width + gap), y0 + i / columns * (height + gap), width, height);
            if (ui.Button(batch, area, buttons[i].Label, buttons[i].Enabled, size: 13f)) action = buttons[i].Action;
        }

        Rectangle screen = batch.GraphicsDevice.Viewport.Bounds;
        var menu = new Rectangle(screen.Width - ui.S(96), ui.S(4), ui.S(90), ui.S(26));
        if (ui.Button(batch, menu, texts.Get("ui.menu"), size: 12f)) action = new OpenPauseMenu();
        return action;
    }

    /// <summary>OXCE action menu: each available attack with its accuracy (before distance) and TU cost.</summary>
    private static HudAction? DrawActionMenu(SpriteBatch batch, Battle battle, BattleUnit unit, BattleItem item, UiContext ui, Localizer texts, Rectangle panel)
    {
        var rows = new List<(AttackMode Mode, int Accuracy, int Cost)>();
        foreach (AttackMode mode in Modes)
        {
            int cost = CombatRules.AttackTu(unit, item.Definition, mode);
            if (cost <= 0) continue;
            rows.Add((mode, CombatRules.Accuracy(battle, unit, item, mode), cost));
        }

        if (rows.Count == 0) return null;
        int width = ui.S(330), height = ui.S(40);
        int x = panel.X + ui.S(290), y = panel.Y - rows.Count * (height + ui.S(4)) - ui.S(8);
        HudAction? action = null;
        for (int i = 0; i < rows.Count; i++)
        {
            (AttackMode mode, int accuracy, int cost) = rows[i];
            var area = new Rectangle(x, y + i * (height + ui.S(4)), width, height);
            string label = $"{texts.Get($"mode.{mode.ToString().ToLowerInvariant()}")}   {texts.Format("ui.accuracy", ("value", accuracy))}   {texts.Format("ui.cost", ("value", cost))}";
            if (ui.Button(batch, area, label, enabled: cost <= unit.Tu, size: 14f)) action = new ChooseAttack(item, mode);
        }

        return action;
    }
}

/// <summary>Displayed names of units: the type's name and a number within the side.</summary>
public static class UnitNames
{
    public static string Of(BattleUnit unit, Localizer texts)
    {
        if (unit.Name.Length > 0) return unit.Name;
        return $"{texts.Get(unit.Definition.NameKey)} {unit.Id}";
    }
}
