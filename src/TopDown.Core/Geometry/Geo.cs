using System.Numerics;

namespace TopDown.Core.Geometry;

/// <summary>Small 2D geometry helpers used by collisions, raycasts and AI.</summary>
public static class Geo
{
    public const float Epsilon = 1e-5f;

    public static Vector2 FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

    public static float ToAngle(Vector2 direction) => MathF.Atan2(direction.Y, direction.X);

    public static float DegToRad(float degrees) => degrees * (MathF.PI / 180f);

    public static float RadToDeg(float radians) => radians * (180f / MathF.PI);

    /// <summary>Signed smallest difference <c>to - from</c>, in [-π, π].</summary>
    public static float AngleDifference(float from, float to)
    {
        float diff = (to - from) % MathF.Tau;
        if (diff > MathF.PI) diff -= MathF.Tau;
        if (diff < -MathF.PI) diff += MathF.Tau;
        return diff;
    }

    /// <summary>Rotates <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxStep"/>.</summary>
    public static float RotateTowards(float current, float target, float maxStep)
    {
        float diff = AngleDifference(current, target);
        if (MathF.Abs(diff) <= maxStep) return target;
        return current + MathF.Sign(diff) * maxStep;
    }

    public static Vector2 NormalizeOrZero(Vector2 v)
    {
        float length = v.Length();
        return length > Epsilon ? v / length : Vector2.Zero;
    }

    /// <summary>Limits the length of a vector (used to normalise diagonal input).</summary>
    public static Vector2 ClampLength(Vector2 v, float maxLength)
    {
        float lengthSquared = v.LengthSquared();
        if (lengthSquared <= maxLength * maxLength) return v;
        return v / MathF.Sqrt(lengthSquared) * maxLength;
    }

    public static Vector2 Perpendicular(Vector2 v) => new(-v.Y, v.X);

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static Vector2 ClosestPointOnRect(Vector2 point, RectF rect) =>
        new(Math.Clamp(point.X, rect.Left, rect.Right), Math.Clamp(point.Y, rect.Top, rect.Bottom));

    public static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        if (lengthSquared < Epsilon) return a;
        float t = Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        return a + ab * t;
    }

    /// <summary>
    /// Intersection of the segment <c>origin → origin + delta</c> with segment <c>a → b</c>.
    /// Returns the fraction along <paramref name="delta"/> in [0, 1].
    /// </summary>
    public static bool SegmentIntersect(Vector2 origin, Vector2 delta, Vector2 a, Vector2 b, out float t)
    {
        t = 0f;
        Vector2 edge = b - a;
        float denominator = Cross(delta, edge);
        if (MathF.Abs(denominator) < Epsilon) return false;
        Vector2 toA = a - origin;
        float tRay = Cross(toA, edge) / denominator;
        float tEdge = Cross(toA, delta) / denominator;
        if (tRay < 0f || tRay > 1f || tEdge < 0f || tEdge > 1f) return false;
        t = tRay;
        return true;
    }

    /// <summary>
    /// Intersection of the segment <c>origin → origin + delta</c> with a circle.
    /// Returns the first entry fraction in [0, 1]; a segment starting inside the circle hits at 0.
    /// </summary>
    public static bool SegmentCircle(Vector2 origin, Vector2 delta, Vector2 center, float radius, out float t)
    {
        t = 0f;
        Vector2 offset = origin - center;
        float c = offset.LengthSquared() - radius * radius;
        if (c <= 0f) return true;
        float a = delta.LengthSquared();
        if (a < Epsilon) return false;
        float b = Vector2.Dot(offset, delta);
        if (b > 0f) return false;
        float discriminant = b * b - a * c;
        if (discriminant < 0f) return false;
        t = (-b - MathF.Sqrt(discriminant)) / a;
        return t <= 1f;
    }

    /// <summary>
    /// Intersection of the segment <c>origin → origin + delta</c> with a rectangle (slab method).
    /// A segment starting inside the rectangle hits at 0.
    /// </summary>
    public static bool SegmentRect(Vector2 origin, Vector2 delta, RectF rect, out float t, out Vector2 normal)
    {
        t = 0f;
        normal = Vector2.Zero;
        if (rect.Contains(origin))
        {
            normal = -NormalizeOrZero(delta);
            return true;
        }

        float tMin = 0f;
        float tMax = 1f;
        if (!Slab(origin.X, delta.X, rect.Left, rect.Right, ref tMin, ref tMax, out float nearX)) return false;
        if (!Slab(origin.Y, delta.Y, rect.Top, rect.Bottom, ref tMin, ref tMax, out float nearY)) return false;
        t = tMin;
        normal = nearX >= nearY
            ? new Vector2(-MathF.Sign(delta.X), 0f)
            : new Vector2(0f, -MathF.Sign(delta.Y));
        return true;
    }

    private static bool Slab(float origin, float delta, float min, float max, ref float tMin, ref float tMax, out float tNear)
    {
        tNear = float.NegativeInfinity;
        if (MathF.Abs(delta) < Epsilon) return origin >= min && origin <= max;
        float t1 = (min - origin) / delta;
        float t2 = (max - origin) / delta;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tNear = t1;
        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    /// <summary>
    /// Pushes a circle out of a rectangle. Returns false if they do not overlap.
    /// Handles the case where the centre is inside the rectangle.
    /// </summary>
    public static bool CircleRectPush(Vector2 center, float radius, RectF rect, out Vector2 push)
    {
        push = Vector2.Zero;
        Vector2 closest = ClosestPointOnRect(center, rect);
        Vector2 away = center - closest;
        float distanceSquared = away.LengthSquared();
        if (distanceSquared >= radius * radius) return false;

        if (distanceSquared > Epsilon)
        {
            float distance = MathF.Sqrt(distanceSquared);
            push = away / distance * (radius - distance);
            return true;
        }

        // Centre inside the rectangle: leave by the nearest side.
        float left = center.X - rect.Left;
        float right = rect.Right - center.X;
        float top = center.Y - rect.Top;
        float bottom = rect.Bottom - center.Y;
        float min = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
        if (min == left) push = new Vector2(-(left + radius), 0f);
        else if (min == right) push = new Vector2(right + radius, 0f);
        else if (min == top) push = new Vector2(0f, -(top + radius));
        else push = new Vector2(0f, bottom + radius);
        return true;
    }

    /// <summary>Pushes a circle out of a thick segment (capsule). Returns false if they do not overlap.</summary>
    public static bool CircleCapsulePush(Vector2 center, float radius, Vector2 a, Vector2 b, float halfThickness, out Vector2 push)
    {
        push = Vector2.Zero;
        Vector2 closest = ClosestPointOnSegment(center, a, b);
        Vector2 away = center - closest;
        float total = radius + halfThickness;
        float distanceSquared = away.LengthSquared();
        if (distanceSquared >= total * total) return false;
        float distance = MathF.Sqrt(distanceSquared);
        Vector2 normal = distance > Epsilon ? away / distance : Perpendicular(NormalizeOrZero(b - a));
        push = normal * (total - distance);
        return true;
    }
}
