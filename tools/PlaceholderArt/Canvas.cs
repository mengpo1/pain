namespace PlaceholderArt;

/// <summary>Straight (non-premultiplied) RGBA colour with components in 0..1.</summary>
public readonly record struct Rgba(float R, float G, float B, float A = 1f)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);
    public static readonly Rgba White = new(1, 1, 1);
    public static readonly Rgba Black = new(0, 0, 0);

    /// <summary>Parses "#rrggbb" or "#rrggbbaa".</summary>
    public static Rgba Hex(string hex)
    {
        var h = hex.TrimStart('#');
        float C(int i) => Convert.ToInt32(h.Substring(i, 2), 16) / 255f;
        return new Rgba(C(0), C(2), C(4), h.Length >= 8 ? C(6) : 1f);
    }

    public static Rgba Grey(float v, float a = 1f) => new(v, v, v, a);

    public Rgba WithAlpha(float a) => this with { A = a };

    /// <summary>Multiplies the alpha.</summary>
    public Rgba Fade(float f) => this with { A = A * f };

    /// <summary>Multiplies the RGB components (clamped).</summary>
    public Rgba Shade(float f) => new(Clamp01(R * f), Clamp01(G * f), Clamp01(B * f), A);

    /// <summary>k &lt; 1 darkens by multiplication, k &gt; 1 brightens by mixing towards white.</summary>
    public Rgba Tone(float k) => k <= 1f ? Shade(MathF.Max(k, 0f)) : Lerp(White with { A = A }, MathF.Min(k - 1f, 1f));

    public Rgba Lerp(Rgba to, float t) =>
        new(R + (to.R - R) * t, G + (to.G - G) * t, B + (to.B - B) * t, A + (to.A - A) * t);

    public float Luma => 0.299f * R + 0.587f * G + 0.114f * B;

    static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}

/// <summary>Colour sources evaluated per pixel (at the pixel centre).</summary>
public static class Paint
{
    public static Func<float, float, Rgba> Radial(float cx, float cy, float radius, Rgba inner, Rgba outer, float power = 1f) =>
        (x, y) =>
        {
            float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
            return inner.Lerp(outer, MathF.Pow(Math.Clamp(d, 0f, 1f), power));
        };

    public static Func<float, float, Rgba> Linear(float x0, float y0, float x1, float y1, Rgba from, Rgba to) =>
        (x, y) =>
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t = ((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy);
            return from.Lerp(to, Math.Clamp(t, 0f, 1f));
        };
}

/// <summary>
/// A small RGBA float canvas. Pixels are stored premultiplied; pixel (x, y) covers [x, x+1) x [y, y+1).
/// Shapes are anti-aliased by supersampling. With <see cref="Wrap"/> set, drawing and blurring wrap
/// around the edges, which is how the seamless tiles are made.
/// </summary>
public sealed class Canvas
{
    /// <summary>Sub-samples per axis for anti-aliasing (5 x 5 = 25 samples per pixel).</summary>
    public const int Supersample = 5;

    readonly float[] _px;

    public int Width { get; }
    public int Height { get; }
    public bool Wrap { get; }

    public Canvas(int width, int height, bool wrap = false)
    {
        Width = width;
        Height = height;
        Wrap = wrap;
        _px = new float[width * height * 4];
    }

    /// <summary>Builds a canvas by evaluating a colour function for every pixel.</summary>
    public static Canvas Generate(int width, int height, Func<int, int, Rgba> pixel, bool wrap = false)
    {
        var c = new Canvas(width, height, wrap);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                c[x, y] = pixel(x, y);
        return c;
    }

    public Canvas Clone()
    {
        var c = new Canvas(Width, Height, Wrap);
        Array.Copy(_px, c._px, _px.Length);
        return c;
    }

    /// <summary>Straight-alpha access to a pixel (coordinates wrap when <see cref="Wrap"/> is set).</summary>
    public Rgba this[int x, int y]
    {
        get
        {
            if (!TryIndex(x, y, out int i)) return Rgba.Transparent;
            float a = _px[i + 3];
            return a <= 0f ? Rgba.Transparent : new Rgba(_px[i] / a, _px[i + 1] / a, _px[i + 2] / a, a);
        }
        set
        {
            if (!TryIndex(x, y, out int i)) return;
            float a = Math.Clamp(value.A, 0f, 1f);
            _px[i] = value.R * a;
            _px[i + 1] = value.G * a;
            _px[i + 2] = value.B * a;
            _px[i + 3] = a;
        }
    }

    public float AlphaAt(int x, int y) => TryIndex(x, y, out int i) ? _px[i + 3] : 0f;

    bool TryIndex(int x, int y, out int index)
    {
        if (Wrap)
        {
            x = Mod(x, Width);
            y = Mod(y, Height);
        }
        else if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            index = -1;
            return false;
        }
        index = (y * Width + x) * 4;
        return true;
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    /// <summary>Source-over blend of one colour into a pixel with the given coverage.</summary>
    public void Blend(int x, int y, Rgba c, float coverage = 1f)
    {
        float a = Math.Clamp(c.A * coverage, 0f, 1f);
        if (a <= 0f || !TryIndex(x, y, out int i)) return;
        float k = 1f - a;
        _px[i] = c.R * a + _px[i] * k;
        _px[i + 1] = c.G * a + _px[i + 1] * k;
        _px[i + 2] = c.B * a + _px[i + 2] * k;
        _px[i + 3] = a + _px[i + 3] * k;
    }

    public void Fill(Shape shape, Rgba color) => Fill(shape, (_, _) => color);

    /// <summary>Fills a shape, anti-aliased by counting covered sub-samples in each pixel.</summary>
    public void Fill(Shape shape, Func<float, float, Rgba> paint)
    {
        var b = shape.Bounds;
        int x0 = (int)MathF.Floor(b.X0), y0 = (int)MathF.Floor(b.Y0);
        int x1 = (int)MathF.Ceiling(b.X1), y1 = (int)MathF.Ceiling(b.Y1);
        if (!Wrap)
        {
            x0 = Math.Max(x0, 0);
            y0 = Math.Max(y0, 0);
            x1 = Math.Min(x1, Width);
            y1 = Math.Min(y1, Height);
        }

        const int n = Supersample;
        const float step = 1f / n;
        for (int py = y0; py < y1; py++)
        {
            for (int px = x0; px < x1; px++)
            {
                int hits = 0;
                for (int sy = 0; sy < n; sy++)
                    for (int sx = 0; sx < n; sx++)
                        if (shape.Contains(px + (sx + 0.5f) * step, py + (sy + 0.5f) * step))
                            hits++;
                if (hits > 0)
                    Blend(px, py, paint(px + 0.5f, py + 0.5f), hits / (float)(n * n));
            }
        }
    }

    /// <summary>Draws <paramref name="src"/> over this canvas with its top-left corner at (dx, dy).</summary>
    public void Draw(Canvas src, int dx = 0, int dy = 0, float opacity = 1f)
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                int s = (y * src.Width + x) * 4;
                float a = src._px[s + 3] * opacity;
                if (a <= 0f || !TryIndex(x + dx, y + dy, out int d)) continue;
                float k = 1f - a;
                for (int ch = 0; ch < 3; ch++) _px[d + ch] = src._px[s + ch] * opacity + _px[d + ch] * k;
                _px[d + 3] = a + _px[d + 3] * k;
            }
        }
    }

    /// <summary>Draws <paramref name="src"/> underneath this canvas (destination-over).</summary>
    public void DrawUnder(Canvas src, int dx = 0, int dy = 0, float opacity = 1f)
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                int s = (y * src.Width + x) * 4;
                float a = src._px[s + 3] * opacity;
                if (a <= 0f || !TryIndex(x + dx, y + dy, out int d)) continue;
                float k = 1f - _px[d + 3];
                for (int ch = 0; ch < 3; ch++) _px[d + ch] += src._px[s + ch] * opacity * k;
                _px[d + 3] += a * k;
            }
        }
    }

    /// <summary>Separable gaussian blur of all channels (premultiplied, so edges fade cleanly).</summary>
    public void Blur(float sigma)
    {
        if (sigma <= 0f) return;
        int r = (int)MathF.Ceiling(sigma * 3f);
        var kernel = new float[2 * r + 1];
        float sum = 0f;
        for (int i = -r; i <= r; i++) sum += kernel[i + r] = MathF.Exp(-(i * i) / (2f * sigma * sigma));
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        var tmp = new float[_px.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            // pass 0: horizontal, _px -> tmp; pass 1: vertical, tmp -> _px
            float[] from = pass == 0 ? _px : tmp, to = pass == 0 ? tmp : _px;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float r0 = 0, g0 = 0, b0 = 0, a0 = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int sx = pass == 0 ? x + k : x, sy = pass == 0 ? y : y + k;
                        if (Wrap) { sx = Mod(sx, Width); sy = Mod(sy, Height); }
                        else if ((uint)sx >= (uint)Width || (uint)sy >= (uint)Height) continue;
                        int s = (sy * Width + sx) * 4;
                        float w = kernel[k + r];
                        r0 += from[s] * w; g0 += from[s + 1] * w; b0 += from[s + 2] * w; a0 += from[s + 3] * w;
                    }
                    int d = (y * Width + x) * 4;
                    to[d] = r0; to[d + 1] = g0; to[d + 2] = b0; to[d + 3] = a0;
                }
            }
        }
    }

    /// <summary>Rewrites every pixel through a function of (x, y, straight colour).</summary>
    public void Map(Func<int, int, Rgba, Rgba> f)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                this[x, y] = f(x, y, this[x, y]);
    }

    /// <summary>Adds a solid outline around the opaque parts, drawn underneath them.</summary>
    public void Outline(Rgba color, float width = 1f)
    {
        var layer = new Canvas(Width, Height, Wrap);
        int r = (int)MathF.Ceiling(width);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float m = 0f;
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        float w = Math.Clamp(width + 1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                        if (w > 0f) m = MathF.Max(m, AlphaAt(x + dx, y + dy) * w);
                    }
                }
                if (m > 0f) layer[x, y] = color.Fade(m);
            }
        }
        DrawUnder(layer);
    }

    /// <summary>Adds a soft halo of the given colour around the opaque parts, drawn underneath them.</summary>
    public void Glow(Rgba color, float sigma, float strength)
    {
        var layer = new Canvas(Width, Height, Wrap);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                layer[x, y] = color.Fade(AlphaAt(x, y));
        layer.Blur(sigma);
        layer.Map((_, _, p) => p.WithAlpha(MathF.Min(1f, p.A * strength)));
        DrawUnder(layer);
    }

    /// <summary>Tints the rim pixels of the opaque area (pixels that border transparency) towards a colour.</summary>
    public void EdgeLight(Rgba color, float strength)
    {
        var copy = Clone();
        Map((x, y, p) =>
        {
            if (p.A <= 0f) return p;
            float minN = MathF.Min(MathF.Min(copy.AlphaAt(x - 1, y), copy.AlphaAt(x + 1, y)),
                                   MathF.Min(copy.AlphaAt(x, y - 1), copy.AlphaAt(x, y + 1)));
            float edge = (1f - minN) * strength;
            return p.Lerp(color with { A = p.A }, Math.Clamp(edge, 0f, 1f));
        });
    }

    /// <summary>Encodes the canvas as an 8-bit RGBA PNG.</summary>
    public byte[] ToPng()
    {
        var bytes = new byte[Width * Height * 4];
        for (int i = 0; i < Width * Height; i++)
        {
            float a = _px[i * 4 + 3];
            if (a <= 0.5f / 255f) continue; // fully transparent pixels stay 0,0,0,0
            for (int ch = 0; ch < 3; ch++)
                bytes[i * 4 + ch] = ToByte(_px[i * 4 + ch] / a);
            bytes[i * 4 + 3] = ToByte(a);
        }
        return PngWriter.Encode(Width, Height, bytes);
    }

    static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
