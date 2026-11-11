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
    private Vector2 _punch;

    public Vector2 Position { get; private set; }
    public Vector2 ShakeOffset { get; private set; }
    public Vector2 ViewportSize { get; private set; } = new(1280, 720);
    public float Zoom { get; private set; } = 1f;

    public Matrix Transform =>
        Matrix.CreateTranslation(new Vector3(-(Position + ShakeOffset), 0f))
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(new Vector3(ViewportSize * 0.5f, 0f));

    /// <summary>Screen pixels per world unit in pixel-art mode (0: the world is drawn at screen resolution).</summary>
    public int PixelScale { get; private set; }

    /// <summary>Size of the image the world is drawn into (the screen, or the small pixel-art image with a margin).</summary>
    public Point SceneSize => PixelScale == 0
        ? new Point((int)ViewportSize.X, (int)ViewportSize.Y)
        : new Point((int)MathF.Ceiling(ViewportSize.X / PixelScale) + 2, (int)MathF.Ceiling(ViewportSize.Y / PixelScale) + 2);

    /// <summary>Where the camera centre falls, snapped to whole world units in pixel-art mode.</summary>
    private Vector2 Snapped
    {
        get
        {
            Vector2 center = Position + ShakeOffset;
            return PixelScale == 0 ? center : new Vector2(MathF.Floor(center.X), MathF.Floor(center.Y));
        }
    }

    /// <summary>World to scene image: the screen transform, or one unit per pixel around the snapped centre.</summary>
    public Matrix SceneTransform => PixelScale == 0
        ? Transform
        : Matrix.CreateTranslation(new Vector3(-Snapped, 0f)) * Matrix.CreateTranslation(SceneSize.X / 2, SceneSize.Y / 2, 0f);

    /// <summary>
    /// Where the scene image goes on screen: enlarged by the pixel scale and shifted by what the snapping
    /// left out, so the view still scrolls smoothly while every pixel stays whole.
    /// </summary>
    public Rectangle SceneDestination
    {
        get
        {
            if (PixelScale == 0) return new Rectangle(0, 0, (int)ViewportSize.X, (int)ViewportSize.Y);
            Vector2 fraction = Position + ShakeOffset - Snapped;
            Vector2 topLeft = ViewportSize * 0.5f - (fraction + new Vector2(SceneSize.X / 2, SceneSize.Y / 2)) * PixelScale;
            return new Rectangle((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y), SceneSize.X * PixelScale, SceneSize.Y * PixelScale);
        }
    }

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

    public void SetViewport(int width, int height, CameraSettings settings, float userZoom, bool pixelArt = false)
    {
        ViewportSize = new Vector2(width, height);
        Zoom = height / MathF.Max(1f, settings.ViewHeight) * settings.Zoom * userZoom;
        PixelScale = pixelArt ? Math.Max(1, (int)MathF.Round(Zoom)) : 0;
        if (pixelArt) Zoom = PixelScale;
    }

    public void SnapTo(Vector2 position)
    {
        Position = position;
        _trauma = 0f;
        _punch = Vector2.Zero;
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
        _punch *= MathF.Exp(-PunchRecovery * dt);
        ShakeOffset += _punch * shakeScale;
    }

    /// <summary>How fast a camera jolt settles back (1/s).</summary>
    private const float PunchRecovery = 18f;

    /// <summary>Jolts the view in the direction of a blow, by <paramref name="amount"/> world units.</summary>
    public void Punch(Vector2 direction, float amount)
    {
        if (direction.LengthSquared() < 0.0001f || amount <= 0f) return;
        _punch += Vector2.Normalize(direction) * amount;
        if (_punch.Length() > MaxPunch) _punch = Vector2.Normalize(_punch) * MaxPunch;
    }

    private const float MaxPunch = 14f;

    /// <summary>Adds screen shake; <paramref name="amount"/> around 0.1–1.</summary>
    public void Shake(float amount) => _trauma = Math.Clamp(_trauma + amount, 0f, 1f);

    public Vector2 ScreenToWorld(Vector2 screen) => Vector2.Transform(screen, Matrix.Invert(Transform));

    public Vector2 WorldToScreen(Vector2 world) => Vector2.Transform(world, Transform);
}
