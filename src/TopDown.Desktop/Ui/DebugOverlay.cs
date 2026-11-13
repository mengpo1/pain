using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;
using TopDown.Desktop.Graphics;
using TopDown.Desktop.Input;

namespace TopDown.Desktop.Ui;

/// <summary>
/// Developer view, off by default. F1 shows the panel; F2–F8 toggle each layer while playing.
/// </summary>
public sealed class DebugOverlay
{
    private readonly List<(Vector2 Position, float Radius, float Age)> _noises = new();
    private readonly List<(Vector2 From, Vector2 To, float Age)> _shots = new();
    private int _frames;
    private float _fpsTimer;

    public bool Panel { get; private set; }
    public bool Hitboxes { get; private set; }
    public bool Attacks { get; private set; }
    public bool Ai { get; private set; }
    public bool Noises { get; private set; }
    public bool Collisions { get; private set; }
    public bool Aim { get; private set; }
    public bool AnyWorldLayer => Hitboxes || Attacks || Ai || Noises || Collisions || Aim;
    public float Fps { get; private set; }

    public void HandleInput(InputState input)
    {
        if (input.KeyPressed(Keys.F1)) Panel = !Panel;
        if (input.KeyPressed(Keys.F2)) Hitboxes = !Hitboxes;
        if (input.KeyPressed(Keys.F3)) Attacks = !Attacks;
        if (input.KeyPressed(Keys.F4)) Ai = !Ai;
        if (input.KeyPressed(Keys.F5)) Noises = !Noises;
        if (input.KeyPressed(Keys.F6)) Collisions = !Collisions;
        if (input.KeyPressed(Keys.F7)) Aim = !Aim;
    }

    public void Record(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case NoiseMade noise:
                _noises.Add((WorldRenderer.ToXna(noise.Position), noise.Radius, 0f));
                break;
            case ShotFired shot:
                Vector2 origin = WorldRenderer.ToXna(shot.Shooter.Position);
                _shots.Add((origin, origin + new Vector2(MathF.Cos(shot.Angle), MathF.Sin(shot.Angle)) * shot.Weapon.MuzzleDistance, 0f));
                break;
        }
    }

    public void Update(float dt)
    {
        _frames++;
        _fpsTimer += dt;
        if (_fpsTimer >= 0.5f)
        {
            Fps = _frames / _fpsTimer;
            _frames = 0;
            _fpsTimer = 0f;
        }

        Age(_noises, dt, 1.2f, (n, a) => n with { Age = a }, n => n.Age);
        Age(_shots, dt, 1f, (s, a) => s with { Age = a }, s => s.Age);
    }

    public void DrawWorld(SpriteBatch batch, Texture2D pixel, GameWorld world, Camera2D camera, Vector2 aimTarget)
    {
        if (!AnyWorldLayer) return;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, RasterizerState.CullNone, null, camera.Transform);
        if (Collisions) DrawCollisions(batch, pixel, world, camera);
        if (Hitboxes) DrawHitboxes(batch, pixel, world);
        if (Attacks) DrawAttacks(batch, pixel, world);
        if (Ai) DrawAi(batch, pixel, world);
        if (Noises)
        {
            foreach ((Vector2 position, float radius, float age) in _noises)
                Primitives.Circle(batch, pixel, position, radius, Color.Yellow * (1f - age / 1.2f), 1.5f);
        }

        if (Aim)
        {
            Vector2 player = WorldRenderer.ToXna(world.Player.Position);
            Primitives.Line(batch, pixel, player, aimTarget, Color.White * 0.5f);
            Vector2 facing = player + new Vector2(MathF.Cos(world.Player.Facing), MathF.Sin(world.Player.Facing)) * 40f;
            Primitives.Line(batch, pixel, player, facing, Color.Lime, 2f);
        }

        batch.End();
    }

    private static void DrawCollisions(SpriteBatch batch, Texture2D pixel, GameWorld world, Camera2D camera)
    {
        TileMap map = world.Map;
        Rectangle view = camera.VisibleWorld;
        int minX = Math.Max(0, (int)(view.Left / map.TileSize));
        int minY = Math.Max(0, (int)(view.Top / map.TileSize));
        int maxX = Math.Min(map.Width - 1, (int)(view.Right / map.TileSize));
        int maxY = Math.Min(map.Height - 1, (int)(view.Bottom / map.TileSize));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Blocking blocking = map.Get(x, y).Blocking;
                if (blocking == Blocking.None) continue;
                Color color = blocking.HasFlag(Blocking.Vision) ? Color.Red : Color.Cyan;
                RectF rect = map.TileRect(x, y);
                Primitives.Rect(batch, pixel, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), color * 0.6f);
            }
        }

        foreach (Prop prop in world.Props)
            Primitives.Rect(batch, pixel, new Vector2(prop.Bounds.X, prop.Bounds.Y), new Vector2(prop.Bounds.Width, prop.Bounds.Height), Color.Orange);
        foreach (Door door in world.Doors)
            Primitives.Line(batch, pixel, WorldRenderer.ToXna(door.Hinge), WorldRenderer.ToXna(door.Tip), door.Locked ? Color.Red : Color.Orange, door.Definition.Thickness);
    }

    private static void DrawHitboxes(SpriteBatch batch, Texture2D pixel, GameWorld world)
    {
        foreach (Actor actor in world.Actors)
        {
            Color color = !actor.IsAlive ? Color.Gray : actor.IsPlayer ? Color.Lime : actor.IsDown ? Color.Yellow : Color.Red;
            Primitives.Circle(batch, pixel, WorldRenderer.ToXna(actor.Position), actor.Radius, color, 1.5f);
        }

        foreach (WorldWeapon weapon in world.Weapons)
            Primitives.Circle(batch, pixel, WorldRenderer.ToXna(weapon.Position), WorldWeapon.CollisionRadius, weapon.IsFlying ? Color.OrangeRed : Color.White, 1f);
        foreach (Projectile projectile in world.Projectiles)
            Primitives.Circle(batch, pixel, WorldRenderer.ToXna(projectile.Position), 2f, Color.Yellow);
        foreach (ItemPickup item in world.Items)
            if (!item.Collected) Primitives.Circle(batch, pixel, WorldRenderer.ToXna(item.Position), 10f, Color.Gold);
    }

    private void DrawAttacks(SpriteBatch batch, Texture2D pixel, GameWorld world)
    {
        foreach (Actor actor in world.Actors)
        {
            if (!actor.CanAct) continue;
            WeaponDefinition weapon = world.Combat.CurrentWeapon(actor);
            if (weapon.IsFirearm) continue;
            Color color = actor.State == ActorState.Attacking ? Color.Red : Color.White * 0.25f;
            Primitives.Arc(batch, pixel, WorldRenderer.ToXna(actor.Position), weapon.Range + actor.Radius, actor.Facing, Geo.DegToRad(weapon.ArcDegrees) * 0.5f, color);
        }

        foreach ((Vector2 from, Vector2 to, float age) in _shots)
        {
            Primitives.Line(batch, pixel, from, to, Color.Magenta * (1f - age), 2f);
            Primitives.Circle(batch, pixel, from, 3f, Color.Magenta * (1f - age));
        }
    }

    private static void DrawAi(SpriteBatch batch, Texture2D pixel, GameWorld world)
    {
        foreach (Actor actor in world.Actors)
        {
            if (actor.Brain is not { } brain || !actor.IsAlive) continue;
            Vector2 position = WorldRenderer.ToXna(actor.Position);
            Color color = brain.State switch
            {
                AiState.Chase => Color.Red,
                AiState.Suspicious or AiState.Investigate or AiState.Search => Color.Orange,
                AiState.Dormant => Color.DarkGray,
                _ => Color.LightGreen,
            };
            Primitives.Arc(batch, pixel, position, brain.VisionRange, actor.Facing, brain.VisionAngle * 0.5f, color * 0.35f);
            Primitives.Circle(batch, pixel, position, brain.CloseDetectionRadius, color * 0.3f);

            IReadOnlyList<System.Numerics.Vector2> path = brain.Path;
            Vector2 previous = position;
            for (int i = brain.PathIndex; i < path.Count; i++)
            {
                Vector2 next = WorldRenderer.ToXna(path[i]);
                Primitives.Line(batch, pixel, previous, next, Color.Cyan * 0.8f, 1.5f);
                previous = next;
            }

            if (brain.Target is not null)
            {
                Vector2 known = WorldRenderer.ToXna(brain.LastKnownTargetPosition);
                Primitives.Circle(batch, pixel, known, 5f, Color.Red);
                if (brain.CanSeeTarget) Primitives.Line(batch, pixel, position, WorldRenderer.ToXna(brain.Target.Position), Color.Red * 0.5f);
            }
        }
    }

    public void DrawPanel(SpriteBatch batch, UiContext ui, GameWorld? world, Camera2D? camera)
    {
        if (!Panel) return;
        var lines = new List<string> { $"FPS {Fps:0}" };
        if (world is not null)
        {
            Actor player = world.Player;
            lines.Add($"Player {player.Position.X:0},{player.Position.Y:0}  {player.State}  facing {Geo.RadToDeg(player.Facing):0}°");
            lines.Add($"Actors {world.Actors.Count(a => a.IsAlive)}/{world.Actors.Count}  bullets {world.Projectiles.Count}  weapons {world.Weapons.Count}");
            lines.Add($"Objective {world.Objectives.CurrentIndex + 1}/{world.Objectives.Count}  time {world.Time:0.0}s");
            if (camera is not null) lines.Add($"Camera zoom {camera.Zoom:0.00}");
        }

        lines.Add($"F2 hitboxes {Flag(Hitboxes)}  F3 attacks {Flag(Attacks)}  F4 AI {Flag(Ai)}");
        lines.Add($"F5 noises {Flag(Noises)}  F6 collisions {Flag(Collisions)}  F7 aim {Flag(Aim)}  F9 reload content");

        float y = 120f * ui.Scale;
        foreach (string line in lines)
        {
            ui.Text(batch, line, new Vector2(16f, y), 16f, Color.LightGreen, shadow: 1f);
            y += 20f * ui.Scale;
        }
    }

    private static string Flag(bool on) => on ? "ON" : "off";

    private static void Age<T>(List<T> items, float dt, float max, Func<T, float, T> withAge, Func<T, float> age)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            float next = age(items[i]) + dt;
            if (next >= max) items.RemoveAt(i);
            else items[i] = withAge(items[i], next);
        }
    }
}
