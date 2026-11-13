using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TopDown.Desktop.Graphics;

/// <summary>Sprite ids the engine uses for its built-in effects. Content maps them to files.</summary>
public static class EffectSprites
{
    public static readonly string[] Blood = ["fx.blood.1", "fx.blood.2", "fx.blood.3"];
    public const string BloodDrop = "fx.blood_drop";
    public const string Gore = "fx.gore";
    public const string Gib = "fx.gib";
    public const string Spark = "fx.spark";
    public const string Shard = "fx.shard";
    public const string Smoke = "fx.smoke";
    public const string MuzzleFlash = "fx.muzzle";
    public const string Bullet = "fx.bullet";
    public const string Crosshair = "ui.crosshair";
    public const string Arrow = "ui.arrow";
}

/// <summary>A short-lived light (muzzle flash, explosion) used by the lighting pass.</summary>
public readonly record struct TransientLight(Vector2 Position, float Radius, Color Color, float Intensity);

/// <summary>
/// Purely visual effects: particles, persistent floor decals (blood), bullet tracers and
/// flashes. Capped collections, no per-frame allocation.
/// </summary>
public sealed class EffectsLayer
{
    private const int MaxParticles = 900;
    private const int MaxDecals = 700;

    private readonly List<Particle> _particles = new(MaxParticles);
    private readonly List<Decal> _decals = new(MaxDecals);
    private readonly List<TracerLine> _tracers = new();
    private readonly List<Flash> _flashes = new();
    private readonly Random _random = new(5);

    public IEnumerable<TransientLight> Lights
    {
        get
        {
            foreach (Flash flash in _flashes)
            {
                float t = flash.Life / flash.MaxLife;
                yield return new TransientLight(flash.Position, flash.LightRadius, flash.Color, t);
            }
        }
    }

    public void Clear()
    {
        _particles.Clear();
        _decals.Clear();
        _tracers.Clear();
        _flashes.Clear();
    }

    public void BloodSpray(Vector2 position, Vector2 direction, int amount, Color tint)
    {
        for (int i = 0; i < amount; i++)
        {
            Vector2 velocity = Rotate(direction, Range(-0.6f, 0.6f)) * Range(60f, 340f);
            AddParticle(new Particle(position, velocity, Range(0.15f, 0.45f), EffectSprites.BloodDrop, tint, Range(1f, 2.2f), 5f)
            {
                LeavesDecal = true,
            });
        }
    }

    /// <summary>Blood flying in every direction (a head giving way, a limb torn off).</summary>
    public void BloodBurst(Vector2 position, int amount, Color tint, float speed = 1f)
    {
        for (int i = 0; i < amount; i++)
        {
            Vector2 velocity = Rotate(Vector2.UnitX, Range(0f, MathF.Tau)) * Range(40f, 300f) * speed;
            AddParticle(new Particle(position, velocity, Range(0.12f, 0.4f), EffectSprites.BloodDrop, tint, Range(1f, 2.6f), 5f)
            {
                LeavesDecal = true,
            });
        }
    }

    /// <summary>Lumps of flesh thrown around <paramref name="direction"/>; they stay on the floor.</summary>
    public void Chunks(Vector2 position, Vector2 direction, int amount, Color tint)
    {
        for (int i = 0; i < amount; i++)
        {
            Vector2 velocity = Rotate(direction, Range(-1.3f, 1.3f)) * Range(60f, 260f);
            AddParticle(new Particle(position, velocity, Range(0.2f, 0.45f), EffectSprites.Gib, tint, Range(0.6f, 1.2f), 6f)
            {
                Spin = Range(-14f, 14f),
                Rotation = Range(0f, MathF.Tau),
                LeavesDecal = true,
            });
        }
    }

    /// <summary>A torn-off body part flying away, spinning, then lying on the floor for good.</summary>
    public void FlyingPart(Vector2 position, Vector2 velocity, float rotation, string sprite, Vector2 origin, Color tint)
    {
        AddParticle(new Particle(position, velocity, 0.45f, sprite, tint, 1f, 5f)
        {
            Rotation = rotation,
            Spin = Range(-9f, 9f),
            Origin = origin,
            LeavesDecal = true,
        });
    }

    public void BloodPool(Vector2 position, Color tint, float scale = 1f)
    {
        string sprite = EffectSprites.Blood[_random.Next(EffectSprites.Blood.Length)];
        AddDecal(new Decal(position, Range(0f, MathF.Tau), Range(0.8f, 1.3f) * scale, sprite, tint));
    }

    public void Sparks(Vector2 position, Vector2 normal, Color tint)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 velocity = Rotate(normal, Range(-1.1f, 1.1f)) * Range(80f, 260f);
            AddParticle(new Particle(position, velocity, Range(0.08f, 0.22f), EffectSprites.Spark, tint, 1f, 8f) { Additive = true });
        }

        AddParticle(new Particle(position, normal * 20f, 0.5f, EffectSprites.Smoke, tint * 0.5f, 0.6f, 2f) { Grow = 1.5f });
    }

    public void GlassShards(Vector2 position, Color tint)
    {
        for (int i = 0; i < 14; i++)
        {
            Vector2 velocity = Rotate(Vector2.UnitX, Range(0f, MathF.Tau)) * Range(40f, 220f);
            AddParticle(new Particle(position, velocity, Range(0.3f, 0.7f), EffectSprites.Shard, tint, Range(0.8f, 1.4f), 6f)
            {
                Spin = Range(-12f, 12f),
                LeavesDecal = true,
            });
        }
    }

    public void Smoke(Vector2 position, Color tint, int amount = 3)
    {
        for (int i = 0; i < amount; i++)
        {
            Vector2 velocity = Rotate(Vector2.UnitX, Range(0f, MathF.Tau)) * Range(5f, 30f);
            AddParticle(new Particle(position, velocity, Range(0.5f, 1f), EffectSprites.Smoke, tint, Range(0.8f, 1.2f), 1.5f) { Grow = 1.2f });
        }
    }

    public void MuzzleFlash(Vector2 position, float angle, Color tint, float lightRadius)
    {
        _flashes.Add(new Flash(position, angle, 0.06f, 0.06f, tint, lightRadius));
    }

    public void Tracer(Vector2 from, Vector2 to, Color tint) => _tracers.Add(new TracerLine(from, to, 0.07f, tint));

    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                if (p.LeavesDecal) AddDecal(new Decal(p.Position, p.Rotation, p.Size, p.Sprite, p.Color) { Origin = p.Origin });
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Velocity *= MathF.Exp(-p.Drag * dt);
            p.Rotation += p.Spin * dt;
            p.Size += p.Grow * dt;
            _particles[i] = p;
        }

        Age(_tracers, dt, (t, life) => t with { Life = life }, t => t.Life);
        Age(_flashes, dt, (f, life) => f with { Life = life }, f => f.Life);
    }

    public void DrawFloor(SpriteBatch batch, AssetCache assets)
    {
        foreach (Decal decal in _decals)
        {
            SpriteFrame frame = assets.Sprite(decal.Sprite);
            batch.Draw(frame.Texture, decal.Position, frame.Source, decal.Color, decal.Rotation, decal.Origin ?? frame.Origin, decal.Scale, SpriteEffects.None, 0f);
        }
    }

    public void DrawTop(SpriteBatch batch, AssetCache assets)
    {
        foreach (Particle p in _particles)
        {
            if (p.Additive) continue;
            DrawParticle(batch, assets, p);
        }

        foreach (TracerLine tracer in _tracers)
        {
            float t = tracer.Life / 0.07f;
            Primitives.Line(batch, assets.Pixel, tracer.From, tracer.To, tracer.Color * t, 1.5f);
        }
    }

    /// <summary>Glowing effects drawn with additive blending.</summary>
    public void DrawAdditive(SpriteBatch batch, AssetCache assets)
    {
        foreach (Particle p in _particles)
        {
            if (p.Additive) DrawParticle(batch, assets, p);
        }

        foreach (Flash flash in _flashes)
        {
            SpriteFrame frame = assets.Sprite(EffectSprites.MuzzleFlash);
            float scale = 1f + (1f - flash.Life / flash.MaxLife) * 0.5f;
            batch.Draw(frame.Texture, flash.Position, frame.Source, flash.Color, flash.Angle, frame.Origin with { X = frame.Source.Width * 0.15f }, scale, SpriteEffects.None, 0f);
        }
    }

    private static void DrawParticle(SpriteBatch batch, AssetCache assets, Particle p)
    {
        SpriteFrame frame = assets.Sprite(p.Sprite);
        float fade = Math.Clamp(p.Life / (p.MaxLife * 0.4f), 0f, 1f);
        Color color = p.LeavesDecal ? p.Color : p.Color * fade;
        batch.Draw(frame.Texture, p.Position, frame.Source, color, p.Rotation, p.Origin ?? frame.Origin, p.Size, SpriteEffects.None, 0f);
    }

    private void AddParticle(Particle particle)
    {
        if (_particles.Count >= MaxParticles) _particles.RemoveAt(0);
        _particles.Add(particle);
    }

    private void AddDecal(Decal decal)
    {
        if (_decals.Count >= MaxDecals) _decals.RemoveAt(0);
        _decals.Add(decal);
    }

    private static void Age<T>(List<T> items, float dt, Func<T, float, T> withLife, Func<T, float> life)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            float remaining = life(items[i]) - dt;
            if (remaining <= 0f) items.RemoveAt(i);
            else items[i] = withLife(items[i], remaining);
        }
    }

    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private record struct Particle(Vector2 Position, Vector2 Velocity, float Life, string Sprite, Color Color, float Size, float Drag)
    {
        public float MaxLife { get; init; } = Life;
        public float Rotation { get; set; }
        public float Spin { get; init; }
        public float Grow { get; init; }
        public bool Additive { get; init; }
        public bool LeavesDecal { get; init; }
        /// <summary>Pivot in the image (default: the sprite's own origin).</summary>
        public Vector2? Origin { get; init; }
    }

    private readonly record struct Decal(Vector2 Position, float Rotation, float Scale, string Sprite, Color Color)
    {
        public Vector2? Origin { get; init; }
    }

    private readonly record struct TracerLine(Vector2 From, Vector2 To, float Life, Color Color);

    private readonly record struct Flash(Vector2 Position, float Angle, float Life, float MaxLife, Color Color, float LightRadius);
}
