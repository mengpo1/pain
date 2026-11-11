using Game.Core.Battle;
using Game.Core.Common;
using Game.Desktop.Graphics;
using Microsoft.Xna.Framework;

namespace Game.Desktop.Tactical;

/// <summary>A unit sliding from one tile to the next.</summary>
public sealed record WalkAnimation(int UnitId, Vector2 From, Vector2 To);

/// <summary>A projectile travelling between two world pixels, on a curve <see cref="Arc"/> pixels high.</summary>
public sealed record ProjectileAnimation(string? Sprite, Vector2 From, Vector2 To, float Arc = 0f);

/// <summary>
/// Plays the events of the simulation one at a time, so the screen can show each step before
/// asking the battle for the next one. Only presentation lives here: the state is already final.
/// </summary>
public sealed class BattlePlayback
{
    private readonly Queue<BattleEvent> _pending = new();
    private float _timer;
    private float _duration;

    public WalkAnimation? Walk { get; private set; }
    public ProjectileAnimation? Projectile { get; private set; }

    /// <summary>Impact flash position and remaining time.</summary>
    public (Vector2 Position, float Time)? Flash { get; private set; }

    /// <summary>Explosion centre, radius in tiles, and progress (0–1).</summary>
    public (Vector2 Position, int Radius)? Explosion { get; private set; }

    /// <summary>Big centred text (turn start) and remaining time.</summary>
    public (string Text, Color Color, float Time)? Banner { get; set; }

    /// <summary>Progress (0–1) of the current timed animation.</summary>
    public float Progress => _duration <= 0f ? 1f : Math.Clamp(_timer / _duration, 0f, 1f);

    public bool IsAnimating => _duration > 0f && _timer < _duration;

    public bool HasPending => _pending.Count > 0;

    public void Enqueue(IEnumerable<BattleEvent> events)
    {
        foreach (BattleEvent battleEvent in events) _pending.Enqueue(battleEvent);
    }

    public BattleEvent? Next() => _pending.TryDequeue(out BattleEvent? battleEvent) ? battleEvent : null;

    public void Update(float dt)
    {
        if (Flash is { } flash) Flash = flash.Time - dt > 0 ? (flash.Position, flash.Time - dt) : null;
        if (Banner is { } banner) Banner = banner.Time - dt > 0 ? (banner.Text, banner.Color, banner.Time - dt) : null;
        if (_duration <= 0f) return;
        _timer += dt;
        if (_timer < _duration) return;
        _duration = 0f;
        Walk = null;
        Projectile = null;
        Explosion = null;
    }

    public void PlayExplosion(Vector2 position, int radius)
    {
        Explosion = (position, radius);
        Start(0.7f);
    }

    public void PlayWalk(WalkAnimation walk, float seconds)
    {
        Walk = walk;
        Start(seconds);
    }

    public void PlayProjectile(ProjectileAnimation projectile, float seconds)
    {
        Projectile = projectile;
        Start(seconds);
    }

    public void PlayFlash(Vector2 position) => Flash = (position, 0.15f);

    /// <summary>A pause with nothing moving (let the player read what happened).</summary>
    public void Wait(float seconds) => Start(seconds);

    private void Start(float seconds)
    {
        _timer = 0f;
        _duration = Math.Max(0.01f, seconds);
    }

    /// <summary>Interpolated world position of a walking unit's feet, or null if it is not walking.</summary>
    public Vector2? WalkPosition(int unitId) =>
        Walk is { } walk && walk.UnitId == unitId ? Vector2.Lerp(walk.From, walk.To, Progress) : null;

    public Vector2? ProjectilePosition =>
        Projectile is { } projectile
            ? Vector2.Lerp(projectile.From, projectile.To, Progress) - new Vector2(0, projectile.Arc * 4f * Progress * (1f - Progress))
            : null;

    public static Vector2 FeetWorld(Battle battle, Position p) =>
        Iso.TileCenter(p, battle.Map.StandElevation(p));
}
