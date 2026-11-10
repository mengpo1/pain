namespace PlaceholderArt;

/// <summary>Axis-aligned bounding box in pixel coordinates.</summary>
public readonly record struct Box(float X0, float Y0, float X1, float Y1)
{
    public static Box Around(float cx, float cy, float rx, float ry) => new(cx - rx, cy - ry, cx + rx, cy + ry);

    public Box Union(Box o) => new(MathF.Min(X0, o.X0), MathF.Min(Y0, o.Y0), MathF.Max(X1, o.X1), MathF.Max(Y1, o.Y1));
}

/// <summary>
/// A filled 2-D region answering point-in-shape queries; <see cref="Canvas.Fill(Shape, Rgba)"/> supersamples it.
/// Angles are in radians, measured from +X towards +Y (clockwise on screen).
/// </summary>
public abstract class Shape
{
    public abstract Box Bounds { get; }

    public abstract bool Contains(float x, float y);

    public Shape Or(Shape other) => new Combined(this, other, Op.Union);
    public Shape And(Shape other) => new Combined(this, other, Op.Intersect);
    public Shape Minus(Shape other) => new Combined(this, other, Op.Subtract);

    public static Shape Circle(float cx, float cy, float r) => new EllipseShape(cx, cy, r, r, 0f);

    public static Shape Ellipse(float cx, float cy, float rx, float ry, float angle = 0f) =>
        new EllipseShape(cx, cy, rx, ry, angle);

    public static Shape Rect(float x, float y, float w, float h) => new RoundRectShape(x + w / 2f, y + h / 2f, w, h, 0f, 0f);

    /// <summary>Rounded rectangle centred on (cx, cy).</summary>
    public static Shape RoundRect(float cx, float cy, float w, float h, float radius, float angle = 0f) =>
        new RoundRectShape(cx, cy, w, h, radius, angle);

    /// <summary>Thick line segment with round ends; the radius tapers linearly from r1 to r2.</summary>
    public static Shape Capsule(float x1, float y1, float x2, float y2, float r1, float r2) =>
        new CapsuleShape(x1, y1, x2, y2, r1, r2);

    public static Shape Capsule(float x1, float y1, float x2, float y2, float r) => new CapsuleShape(x1, y1, x2, y2, r, r);

    /// <summary>Polygon with even-odd filling.</summary>
    public static Shape Polygon(params (float X, float Y)[] points) => new PolygonShape(points);

    /// <summary>Polygon from (angle, radius) spokes around a centre - handy for stars, shards and bursts.</summary>
    public static Shape Radial(float cx, float cy, params (float Angle, float Radius)[] spokes) =>
        Polygon(spokes.Select(s => (cx + MathF.Cos(s.Angle) * s.Radius, cy + MathF.Sin(s.Angle) * s.Radius)).ToArray());

    /// <summary>Star-shaped region whose boundary radius is a function of the angle (organic blobs).</summary>
    public static Shape Blob(float cx, float cy, float maxRadius, Func<float, float> radiusAtAngle) =>
        new BlobShape(cx, cy, maxRadius, radiusAtAngle);

    /// <summary>Polar radius of an axis-aligned ellipse at a given angle.</summary>
    public static float EllipseRadius(float rx, float ry, float angle)
    {
        float c = MathF.Cos(angle) * ry, s = MathF.Sin(angle) * rx;
        return rx * ry / MathF.Sqrt(c * c + s * s);
    }

    enum Op { Union, Intersect, Subtract }

    sealed class Combined(Shape a, Shape b, Op op) : Shape
    {
        public override Box Bounds => op == Op.Union ? a.Bounds.Union(b.Bounds) : a.Bounds;

        public override bool Contains(float x, float y) => op switch
        {
            Op.Union => a.Contains(x, y) || b.Contains(x, y),
            Op.Intersect => a.Contains(x, y) && b.Contains(x, y),
            _ => a.Contains(x, y) && !b.Contains(x, y),
        };
    }

    sealed class EllipseShape(float cx, float cy, float rx, float ry, float angle) : Shape
    {
        readonly float _cos = MathF.Cos(angle), _sin = MathF.Sin(angle);

        public override Box Bounds => Box.Around(cx, cy, MathF.Max(rx, ry), MathF.Max(rx, ry));

        public override bool Contains(float x, float y)
        {
            if (rx <= 0f || ry <= 0f) return false;
            float dx = x - cx, dy = y - cy;
            float u = dx * _cos + dy * _sin, v = -dx * _sin + dy * _cos;
            return u * u / (rx * rx) + v * v / (ry * ry) <= 1f;
        }
    }

    sealed class RoundRectShape(float cx, float cy, float w, float h, float radius, float angle) : Shape
    {
        readonly float _cos = MathF.Cos(angle), _sin = MathF.Sin(angle);
        readonly float _r = MathF.Max(0f, MathF.Min(radius, MathF.Min(w, h) / 2f));

        public override Box Bounds
        {
            get
            {
                float ex = MathF.Abs(w / 2f * _cos) + MathF.Abs(h / 2f * _sin);
                float ey = MathF.Abs(w / 2f * _sin) + MathF.Abs(h / 2f * _cos);
                return Box.Around(cx, cy, ex, ey);
            }
        }

        public override bool Contains(float x, float y)
        {
            float dx = x - cx, dy = y - cy;
            float u = dx * _cos + dy * _sin, v = -dx * _sin + dy * _cos;
            // signed distance to a rounded box
            float qx = MathF.Abs(u) - (w / 2f - _r), qy = MathF.Abs(v) - (h / 2f - _r);
            float ox = MathF.Max(qx, 0f), oy = MathF.Max(qy, 0f);
            float d = MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f) - _r;
            return d <= 0f;
        }
    }

    sealed class CapsuleShape(float x1, float y1, float x2, float y2, float r1, float r2) : Shape
    {
        public override Box Bounds => Box.Around(x1, y1, r1, r1).Union(Box.Around(x2, y2, r2, r2));

        public override bool Contains(float x, float y)
        {
            float bx = x2 - x1, by = y2 - y1, len2 = bx * bx + by * by;
            float t = len2 > 0f ? Math.Clamp(((x - x1) * bx + (y - y1) * by) / len2, 0f, 1f) : 0f;
            float r = r1 + (r2 - r1) * t;
            if (r <= 0f) return false;
            float dx = x - (x1 + bx * t), dy = y - (y1 + by * t);
            return dx * dx + dy * dy <= r * r;
        }
    }

    sealed class PolygonShape : Shape
    {
        readonly (float X, float Y)[] _p;
        readonly Box _bounds;

        public PolygonShape((float X, float Y)[] points)
        {
            _p = points;
            _bounds = new Box(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
        }

        public override Box Bounds => _bounds;

        public override bool Contains(float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = _p.Length - 1; i < _p.Length; j = i++)
            {
                var (xi, yi) = _p[i];
                var (xj, yj) = _p[j];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }
    }

    sealed class BlobShape(float cx, float cy, float maxRadius, Func<float, float> radiusAt) : Shape
    {
        public override Box Bounds => Box.Around(cx, cy, maxRadius, maxRadius);

        public override bool Contains(float x, float y)
        {
            float dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
            if (d2 > maxRadius * maxRadius) return false;
            return MathF.Sqrt(d2) <= radiusAt(MathF.Atan2(dy, dx));
        }
    }
}
