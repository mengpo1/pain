using Microsoft.Xna.Framework;
using TopDown.Content;

namespace TopDown.Desktop.Graphics;

/// <summary>
/// Follows a target with exponential smoothing, leans toward the aim point and shakes on
/// impacts. The visible world height is constant, so every screen shows the same area.
/// </summary>
public sealed class Camera2D
{
    private readonly Random _random = new(3);
    private float _trauma;

    public Vector2 Position { get; private set; }
    public Vector2 ShakeOffset { get; private set; }
    public Vector2 ViewportSize { get; private set; } = new(1280, 720);
    public float Zoom { get; private set; } = 1f;

    public Matrix Transform =>
        Matrix.CreateTranslation(new Vector3(-(Position + ShakeOffset), 0f))
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(new Vector3(ViewportSize * 0.5f, 0f));

    /// <summary>World rectangle currently on screen (with a small margin).</summary>
    public Rectangle VisibleWorld
    {
        get
        {
            Vector2 half = ViewportSize * 0.5f / Zoom + new Vector2(64f);
            Vector2 center = Position + ShakeOffset;
            return new Rectangle((int)(center.X - half.X), (int)(center.Y - half.Y), (int)(half.X * 2f), (int)(half.Y * 2f));
        }
    }

    public void SetViewport(int width, int height, CameraSettings settings, float userZoom)
    {
        ViewportSize = new Vector2(width, height);
        Zoom = height / MathF.Max(1f, settings.ViewHeight) * settings.Zoom * userZoom;
    }

    public void SnapTo(Vector2 position)
    {
        Position = position;
        _trauma = 0f;
        ShakeOffset = Vector2.Zero;
    }

    /// <summary>Moves toward <paramref name="target"/>, leaning toward <paramref name="aim"/>.</summary>
    public void Update(float dt, Vector2 target, Vector2 aim, bool lookFar, CameraSettings settings, float shakeScale)
    {
        Vector2 offset = aim - target;
        float factor = lookFar ? settings.FarLook : settings.LookAhead;
        float max = lookFar ? settings.FarLookMax : settings.LookAheadMax;
        Vector2 lean = offset * factor;
        if (lean.Length() > max) lean = Vector2.Normalize(lean) * max;

        Vector2 desired = target + lean;
        float blend = 1f - MathF.Exp(-settings.FollowSharpness * dt);
        Position = Vector2.Lerp(Position, desired, blend);

        _trauma = MathF.Max(0f, _trauma - settings.ShakeDecay * dt);
        float shake = _trauma * _trauma * settings.MaxShake * shakeScale;
        ShakeOffset = shake > 0.01f
            ? new Vector2((float)_random.NextDouble() * 2f - 1f, (float)_random.NextDouble() * 2f - 1f) * shake
            : Vector2.Zero;
    }

    /// <summary>Adds screen shake; <paramref name="amount"/> around 0.1–1.</summary>
    public void Shake(float amount) => _trauma = Math.Clamp(_trauma + amount, 0f, 1f);

    public Vector2 ScreenToWorld(Vector2 screen) => Vector2.Transform(screen, Matrix.Invert(Transform));

    public Vector2 WorldToScreen(Vector2 world) => Vector2.Transform(world, Transform);
}
