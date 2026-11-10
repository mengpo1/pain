using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;
using TopDown.Desktop.Audio;
using TopDown.Desktop.Graphics;
using TopDown.Desktop.Input;
using TopDown.Desktop.Ui;
using NVector2 = System.Numerics.Vector2;

namespace TopDown.Desktop.Screens;

/// <summary>
/// Runs a level: fixed-step simulation, input → commands, events → sounds and effects,
/// rendering with interpolation, instant restart.
/// </summary>
public sealed class PlayScreen : Screen
{
    /// <summary>Simulation rate. Rendering runs at whatever rate the display allows.</summary>
    private const float Step = 1f / 120f;
    private const float MaxFrameTime = 0.1f;
    private const float LockOnScreenRadius = 90f;

    private readonly IReadOnlyList<LevelDefinition> _playlist;
    private readonly int _levelIndex;
    private readonly ModifierDefinition _modifier;
    private readonly Camera2D _camera = new();
    private readonly EffectsLayer _effects = new();
    private readonly WorldRenderer _renderer;
    private readonly PostProcessor _post;
    private readonly Hud _hud;
    private readonly Autopilot? _autopilot;
    private readonly Dictionary<Actor, bool> _visibility = new();
    private readonly List<WorldLight> _lights = new();
    private readonly Func<Actor, bool> _isVisible;

    private GameWorld _world = null!;
    private PlayerCommands _pending;
    private float _accumulator;
    private float _time;
    private float _outcomeTime;
    private bool _paused;
    private int _pauseIndex;
    private int _attempt;
    private Actor? _lockTarget;
    private Vector2 _aimWorld;
    private int _lastLegFrame = -1;

    public PlayScreen(GameHost host, IReadOnlyList<LevelDefinition> playlist, int levelIndex, ModifierDefinition modifier, bool autopilot)
        : base(host)
    {
        _playlist = playlist;
        _levelIndex = levelIndex;
        _modifier = modifier;
        ThemeDefinition theme = Session.Content.Theme;
        _renderer = new WorldRenderer(Session.Assets, theme);
        _post = new PostProcessor(host.GraphicsDevice, Session.Assets, theme.Post);
        _hud = new Hud(Session.Ui, host.Input.Bindings);
        _autopilot = autopilot ? new Autopilot() : null;
        _isVisible = IsVisible;
        Restart();
    }

    public override bool ShowSystemCursor => _paused;

    private LevelDefinition Level => _playlist[_levelIndex];
    private InputState Input => Host.Input;
    private AudioPlayer Audio => Session.Audio;
    private Localizer T => Session.Texts;
    private GameSounds Sounds => Session.Content.Game.Sounds;

    /// <summary>Instant restart: a new world from the level definition, nothing reloaded from disk.</summary>
    private void Restart()
    {
        _attempt++;
        _world = GameWorld.Create(Level, Session.Content.Definitions, seed: _attempt * 7919, _modifier);
        _effects.Clear();
        _hud.Clear();
        _visibility.Clear();
        _accumulator = 0f;
        _outcomeTime = 0f;
        _paused = false;
        _lockTarget = null;
        _pending = default;
        Vector2 player = WorldRenderer.ToXna(_world.Player.Position);
        _aimWorld = player + new Vector2(MathF.Cos(_world.Player.Facing), MathF.Sin(_world.Player.Facing)) * 50f;
        UpdateViewport();
        _camera.SnapTo(player);
        Audio.PlayMusic(Level.Music ?? Session.Content.Game.MenuMusic);
        Audio.PlayAmbience(Level.Ambience);
        if (_attempt == 1 && Level.IntroKey is not null) _hud.ShowMessage(T.Get(Level.IntroKey), 4f);
    }

    public override void Update(float dt)
    {
        _time += dt;
        UpdateViewport();
        if (Input.Pressed(GameAction.Pause) && _world.Outcome == WorldOutcome.Playing)
        {
            _paused = !_paused;
            _pauseIndex = 0;
            Audio.Play(Sounds.UiSelect, channel: AudioChannel.Interface);
        }

        if (_paused)
        {
            UpdatePauseMenu();
            return;
        }

        if (Input.Pressed(GameAction.Restart))
        {
            Restart();
            return;
        }

        if (_world.Outcome == WorldOutcome.Victory && _outcomeTime > 0.6f && Input.Pressed(GameAction.Confirm))
        {
            NextLevel();
            return;
        }

        GatherCommands();
        float frame = MathF.Min(dt, MaxFrameTime);
        _accumulator += frame;
        while (_accumulator >= Step)
        {
            _world.Step(Step, _pending);
            _pending.AttackPressed = _pending.PickupOrThrowPressed = _pending.InteractPressed = false;
            HandleEvents();
            _accumulator -= Step;
        }

        if (_world.Outcome != WorldOutcome.Playing) _outcomeTime += frame;
        float alpha = _accumulator / Step;
        NVector2 playerPosition = NVector2.Lerp(_world.Player.PreviousPosition, _world.Player.Position, alpha);
        _camera.Update(frame, WorldRenderer.ToXna(playerPosition), _aimWorld, Input.Held(GameAction.LookFar), Session.Content.Game.Camera, Host.Settings.ScreenShake);
        Audio.ListenerPosition = _camera.Position;
        _effects.Update(frame);
        _hud.Update(frame, _world);
        Host.Debug.Update(dt);
        Host.Debug.HandleInput(Input);
        PlayFootsteps();
    }

    private void GatherCommands()
    {
        if (_lockTarget is { } locked && (!locked.IsAlive || locked.IsDown)) _lockTarget = null;
        if (Input.Pressed(GameAction.LockOn)) _lockTarget = _lockTarget is null ? FindLockTarget() : null;

        _aimWorld = _lockTarget is { } target ? WorldRenderer.ToXna(target.Position) : _camera.ScreenToWorld(Input.MousePosition);
        Vector2 move = Input.MoveVector();
        _pending.Move = new NVector2(move.X, move.Y);
        _pending.AimTarget = new NVector2(_aimWorld.X, _aimWorld.Y);
        _pending.AttackHeld = Input.Held(GameAction.Attack);
        _pending.AttackPressed |= Input.Pressed(GameAction.Attack);
        _pending.PickupOrThrowPressed |= Input.Pressed(GameAction.PickupThrow);
        _pending.InteractPressed |= Input.Pressed(GameAction.Interact);
        _autopilot?.Drive(_world, ref _pending, _time);
        if (_autopilot is not null) _aimWorld = WorldRenderer.ToXna(_pending.AimTarget);
    }

    /// <summary>Visible enemy closest to the cursor, within a screen radius.</summary>
    private Actor? FindLockTarget()
    {
        Vector2 mouse = Input.MousePosition;
        Actor? best = null;
        float bestDistance = LockOnScreenRadius;
        foreach (Actor actor in _world.Actors)
        {
            if (!actor.IsAlive || actor.IsDown || !_world.Player.IsHostileTo(actor)) continue;
            float distance = Vector2.Distance(_camera.WorldToScreen(WorldRenderer.ToXna(actor.Position)), mouse);
            if (distance < bestDistance && _world.Physics.HasLineOfSight(_world.Player.Position, actor.Position, Blocking.Vision))
            {
                best = actor;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void NextLevel()
    {
        if (_levelIndex + 1 < _playlist.Count) Host.SetScreen(new PlayScreen(Host, _playlist, _levelIndex + 1, _modifier, _autopilot is not null));
        else Host.SetScreen(new MenuScreen(Host));
    }

    private void UpdatePauseMenu()
    {
        string[] items = ["pause.resume", "pause.restart", "pause.quit"];
        if (Input.Pressed(GameAction.MenuDown)) _pauseIndex = (_pauseIndex + 1) % items.Length;
        if (Input.Pressed(GameAction.MenuUp)) _pauseIndex = (_pauseIndex + items.Length - 1) % items.Length;
        if (!Input.Pressed(GameAction.Confirm) && !Input.LeftClicked) return;
        Audio.Play(Sounds.UiSelect, channel: AudioChannel.Interface);
        switch (_pauseIndex)
        {
            case 0: _paused = false; break;
            case 1: Restart(); break;
            default: Host.SetScreen(new MenuScreen(Host)); break;
        }
    }

    private void HandleEvents()
    {
        foreach (GameEvent gameEvent in _world.Events)
        {
            Host.Debug.Record(gameEvent);
            HandleEvent(gameEvent);
        }

        _world.ClearEvents();
    }

    private void HandleEvent(GameEvent gameEvent)
    {
        ThemeColors colors = _renderer.Colors;
        switch (gameEvent)
        {
            case ShotFired shot:
                Audio.Play(shot.Weapon.Sounds.Attack, shot.Muzzle);
                _effects.MuzzleFlash(WorldRenderer.ToXna(shot.Muzzle), shot.Angle, colors.Fx, 160f * _post.Settings.MuzzleFlashLight);
                _effects.Smoke(WorldRenderer.ToXna(shot.Muzzle), colors.Fx * 0.4f, 1);
                if (shot.Shooter.IsPlayer) _camera.Shake(shot.Weapon.Recoil * 0.06f);
                break;
            case BulletTrace trace:
                _effects.Tracer(WorldRenderer.ToXna(trace.From), WorldRenderer.ToXna(trace.To), colors.Fx);
                break;
            case BulletImpact impact:
                _effects.Sparks(WorldRenderer.ToXna(impact.Position), WorldRenderer.ToXna(impact.Normal), colors.Fx);
                Audio.Play(Sounds.Impact, impact.Position, volume: 0.5f);
                break;
            case MeleeSwing swing:
                Audio.Play(swing.Weapon.Sounds.Attack, swing.Attacker.Position);
                break;
            case ActorHit hit:
                Audio.Play(hit.Weapon?.Sounds.Hit, hit.Target.Position);
                if (hit.Damage > 0f)
                    _effects.BloodSpray(WorldRenderer.ToXna(hit.Target.Position), WorldRenderer.ToXna(hit.Direction), 10, Blood(hit.Target));
                break;
            case ActorKilled killed:
                Audio.Play(killed.Victim.Definition.Sounds.Death, killed.Victim.Position);
                _effects.BloodSpray(WorldRenderer.ToXna(killed.Victim.Position), WorldRenderer.ToXna(killed.Direction), 18, Blood(killed.Victim));
                _effects.BloodPool(WorldRenderer.ToXna(killed.Victim.Position + killed.Direction * 8f), Blood(killed.Victim), killed.Victim.Radius / 11f);
                if (killed.Killer is { IsPlayer: true }) _camera.Shake(0.25f);
                if (killed.Victim == _lockTarget) _lockTarget = null;
                break;
            case PlayerDied:
                _camera.Shake(0.6f);
                _hud.Clear();
                break;
            case WeaponPickedUp pickedUp:
                Audio.Play(pickedUp.Weapon.Sounds.Pickup ?? Sounds.Pickup, pickedUp.Actor.Position);
                break;
            case WeaponThrown thrown:
                Audio.Play(Sounds.Throw, thrown.Actor.Position);
                break;
            case ThrownWeaponBounced bounced:
                Audio.Play(Sounds.Bounce, bounced.Position, volume: 0.7f);
                break;
            case WeaponEmpty empty:
                Audio.Play(empty.Weapon.Sounds.Empty, empty.Owner.Position);
                if (empty.Owner.IsPlayer) _hud.ShowMessage(T.Get("hud.outOfAmmo"), 1.2f);
                break;
            case DoorHit doorHit:
                Audio.Play(doorHit.Door.Definition.HitSound, doorHit.Door.Hinge);
                _camera.Shake(0.15f);
                break;
            case DoorPushed pushed:
                Audio.Play(pushed.Door.Definition.HitSound, pushed.Door.Hinge, volume: 0.35f);
                break;
            case TileBroken broken:
                Audio.Play(broken.Tile.BreakSound, broken.Position);
                _effects.GlassShards(WorldRenderer.ToXna(broken.Position), colors.Fx);
                break;
            case EnemyAlerted alerted:
                Audio.Play(alerted.Enemy.Definition.Sounds.Alert, alerted.Enemy.Position, volume: 0.6f);
                break;
            case ItemCollected collected:
                Audio.Play(Sounds.Pickup, collected.Item.Position);
                _hud.ShowMessage(T.Format("hud.collected", ("item", T.Get(collected.Item.Definition.NameKey))), 2.5f);
                break;
            case Activated activated:
                Audio.Play(Sounds.UiSelect, activated.Interactable.Position);
                break;
            case ObjectiveCompleted:
                Audio.Play(Sounds.ObjectiveComplete, channel: AudioChannel.Interface);
                if (_world.Objectives.Current is { } next) _hud.ShowMessage(T.Get(next.TextKey), 3f);
                break;
            case LevelCompleted:
                Audio.Play(Sounds.LevelComplete, channel: AudioChannel.Interface);
                break;
            case MessageRequested message:
                _hud.ShowMessage(T.Get(message.TextKey), message.Duration);
                break;
            case SoundRequested sound:
                Audio.Play(sound.SoundId, sound.Position);
                break;
        }
    }

    private Color Blood(Actor actor) =>
        ColorParser.Parse(actor.Definition.Sprites.BloodTint, Color.White).MultiplyBy(_renderer.Colors.Blood);

    /// <summary>Footsteps follow the legs animation events of the player.</summary>
    private void PlayFootsteps()
    {
        Actor player = _world.Player;
        if (Sounds.Footstep is null || player.Definition.Sprites.Legs is not { } legs || !player.IsAlive) return;
        if (!Session.Content.Assets.Animations.TryGetValue(legs, out AnimationDefinition? animation) || animation.Events.Count == 0) return;
        int frames = Session.Assets.FrameCount(legs);
        int frame = (int)(WorldRenderer.LegAnimationTime(player) / MathF.Max(0.01f, animation.FrameDuration)) % Math.Max(1, frames);
        if (frame != _lastLegFrame && animation.Events.TryGetValue(frame.ToString(System.Globalization.CultureInfo.InvariantCulture), out string? name) && name == "footstep")
            Audio.Play(Sounds.Footstep, player.Position, volume: 0.35f);
        _lastLegFrame = frame;
    }

    private bool IsVisible(Actor actor)
    {
        if (!_post.Settings.HideUnseenActors || actor.IsPlayer) return true;
        return _visibility.TryGetValue(actor, out bool visible) && visible;
    }

    private void UpdateVisibility()
    {
        if (!_post.Settings.HideUnseenActors) return;
        Actor player = _world.Player;
        foreach (Actor actor in _world.Actors)
        {
            if (actor.IsPlayer) continue;
            bool near = NVector2.Distance(actor.Position, player.Position) < 44f;
            _visibility[actor] = near || _world.Physics.HasLineOfSight(player.Position, actor.Position, Blocking.Vision);
        }
    }

    private void UpdateViewport()
    {
        Rectangle bounds = Host.GraphicsDevice.Viewport.Bounds;
        _camera.SetViewport(bounds.Width, bounds.Height, Session.Content.Game.Camera, Host.Settings.Zoom);
    }

    public override void Draw(SpriteBatch batch, float dt)
    {
        float alpha = _paused ? 1f : _accumulator / Step;
        UpdateVisibility();
        _post.BeginScene(_renderer.Colors.Background);
        _renderer.Draw(batch, _world, _camera, _effects, alpha, _time, _isVisible);
        _post.EndScene(batch, _camera, CollectLights(), _paused ? 0f : dt);

        Host.Debug.DrawWorld(batch, Session.Assets.Pixel, _world, _camera, _aimWorld);

        Rectangle screen = Host.GraphicsDevice.Viewport.Bounds;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        string levelName = T.Get(Level.NameKey);
        _hud.Draw(batch, _world, screen, _time, levelName);
        if (_world.Outcome == WorldOutcome.Defeat) _hud.DrawDeath(batch, screen, _outcomeTime);
        if (_world.Outcome == WorldOutcome.Victory) _hud.DrawVictory(batch, screen, _world, _time, _levelIndex + 1 < _playlist.Count);
        if (_paused) DrawPause(batch, screen);
        Host.Debug.DrawPanel(batch, Session.Ui, _world, _camera);
        if (!_paused) DrawCrosshair(batch);
        batch.End();
    }

    private IEnumerable<WorldLight> CollectLights()
    {
        _lights.Clear();
        PostEffects post = _post.Settings;
        if (post.Darkness <= 0f) return _lights;

        foreach (LevelLight light in Level.Lights)
        {
            float flicker = light.Flicker > 0f ? 1f - light.Flicker * (0.5f + 0.5f * MathF.Sin(_time * 23f + light.Position.X)) * (float)Random.Shared.NextDouble() : 1f;
            _lights.Add(new WorldLight(WorldRenderer.ToXna(Level.ToWorld(light.Position)), light.Radius * Level.TileSize,
                ColorParser.Parse(light.Color, Color.White), light.Intensity * flicker));
        }

        if (post.PlayerLightRadius > 0f && _world.Player.IsAlive)
            _lights.Add(new WorldLight(WorldRenderer.ToXna(_world.Player.Position), post.PlayerLightRadius, Color.White, post.PlayerLightIntensity));

        foreach (TransientLight flash in _effects.Lights)
            _lights.Add(new WorldLight(flash.Position, flash.Radius, flash.Color, flash.Intensity));
        return _lights;
    }

    private void DrawCrosshair(SpriteBatch batch)
    {
        SpriteFrame frame = Session.Assets.Sprite(EffectSprites.Crosshair);
        Vector2 position = _lockTarget is not null || _autopilot is not null ? _camera.WorldToScreen(_aimWorld) : Input.MousePosition;
        float scale = 2f * Session.Ui.Scale;
        Color color = _lockTarget is not null ? _renderer.Colors.Accent : Color.White;
        batch.Draw(frame.Texture, position, frame.Source, color, _lockTarget is not null ? _time * 3f : 0f, frame.Origin, scale, SpriteEffects.None, 0f);
    }

    private void DrawPause(SpriteBatch batch, Rectangle screen)
    {
        UiContext ui = Session.Ui;
        batch.Draw(Session.Assets.Pixel, screen, ui.Colors.Shadow * 0.6f);
        ui.Title(batch, T.Get("pause.title"), new Vector2(screen.Width * 0.5f, screen.Height * 0.28f), 56f, ui.Colors.Accent, _time);
        string[] items = ["pause.resume", "pause.restart", "pause.quit"];
        for (int i = 0; i < items.Length; i++)
        {
            Color color = i == _pauseIndex ? ui.Colors.Secondary : ui.Colors.Text;
            var position = new Vector2(screen.Width * 0.5f, screen.Height * 0.45f + i * 46f * ui.Scale);
            Vector2 size = ui.Measure(T.Get(items[i]), 30f);
            var area = new Rectangle((int)(position.X - size.X / 2 - 10), (int)position.Y - 4, (int)size.X + 20, (int)size.Y + 8);
            if (Input.MouseMoved && area.Contains(Input.MousePosition)) _pauseIndex = i;
            ui.Text(batch, (i == _pauseIndex ? "> " : "") + T.Get(items[i]), position, 30f, color, Align.Center);
        }
    }

    public override void Dispose() => _post.Dispose();
}

internal static class ColorExtensions
{
    public static Color MultiplyBy(this Color a, Color b) =>
        new(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, a.A * b.A / 255);
}
