using System.Numerics;

namespace TopDown.Core.Geometry;

/// <summary>Axis-aligned rectangle in world units (Y points down).</summary>
public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    public bool Contains(Vector2 point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public bool Intersects(RectF other) =>
        Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

    public static RectF FromCenter(Vector2 center, float width, float height) =>
        new(center.X - width * 0.5f, center.Y - height * 0.5f, width, height);
}
