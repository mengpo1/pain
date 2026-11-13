using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TopDown.Desktop.Graphics;

/// <summary>Lines, rectangles and circles drawn with a 1×1 texture (debug view, HUD, effects).</summary>
public static class Primitives
{
    private const int CircleSegments = 24;

    public static void Line(SpriteBatch batch, Texture2D pixel, Vector2 from, Vector2 to, Color color, float thickness = 1f)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length < 0.01f) return;
        float angle = MathF.Atan2(delta.Y, delta.X);
        batch.Draw(pixel, from, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public static void FillRect(SpriteBatch batch, Texture2D pixel, Rectangle rect, Color color) =>
        batch.Draw(pixel, rect, color);

    public static void FillRect(SpriteBatch batch, Texture2D pixel, Vector2 position, Vector2 size, Color color) =>
        batch.Draw(pixel, position, null, color, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);

    public static void Rect(SpriteBatch batch, Texture2D pixel, Vector2 position, Vector2 size, Color color, float thickness = 1f)
    {
        Vector2 topRight = position + new Vector2(size.X, 0f);
        Vector2 bottomLeft = position + new Vector2(0f, size.Y);
        Line(batch, pixel, position, topRight, color, thickness);
        Line(batch, pixel, topRight, position + size, color, thickness);
        Line(batch, pixel, position + size, bottomLeft, color, thickness);
        Line(batch, pixel, bottomLeft, position, color, thickness);
    }

    public static void Circle(SpriteBatch batch, Texture2D pixel, Vector2 center, float radius, Color color, float thickness = 1f)
    {
        Vector2 previous = center + new Vector2(radius, 0f);
        for (int i = 1; i <= CircleSegments; i++)
        {
            float angle = i * MathF.Tau / CircleSegments;
            Vector2 next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            Line(batch, pixel, previous, next, color, thickness);
            previous = next;
        }
    }

    /// <summary>Outline of a circular sector (field of view, melee arc).</summary>
    public static void Arc(SpriteBatch batch, Texture2D pixel, Vector2 center, float radius, float direction, float halfAngle, Color color, float thickness = 1f)
    {
        int segments = Math.Max(4, (int)(CircleSegments * halfAngle / MathF.PI));
        Vector2 Point(float angle) => center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        Vector2 previous = Point(direction - halfAngle);
        Line(batch, pixel, center, previous, color, thickness);
        for (int i = 1; i <= segments; i++)
        {
            Vector2 next = Point(direction - halfAngle + 2f * halfAngle * i / segments);
            Line(batch, pixel, previous, next, color, thickness);
            previous = next;
        }

        Line(batch, pixel, previous, center, color, thickness);
    }
}
