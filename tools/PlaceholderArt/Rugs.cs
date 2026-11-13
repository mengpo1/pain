namespace PlaceholderArt;

/// <summary>Floor rugs, drawn pixel by pixel (decor under the furniture).</summary>
public static class Rugs
{
    /// <summary>64 x 96 patterned rug: fringed ends, a border, a central medallion.</summary>
    public static Canvas Persian()
    {
        Rgba field = Rgba.Hex("#7a1e2c"), border = Rgba.Hex("#1f2a52"), gold = Rgba.Hex("#d6a446"), cream = Rgba.Hex("#e9dcc0"), dark = Rgba.Hex("#4a0f1a");
        const int w = 64, h = 96;
        var c = Canvas.Generate(w, h, (x, y) =>
        {
            if (y < 3 || y >= h - 3) return x % 2 == 0 && x > 1 && x < w - 2 ? cream : Rgba.Transparent;
            int bx = Math.Min(x, w - 1 - x), by = Math.Min(y - 3, h - 4 - y);
            int b = Math.Min(bx, by);
            if (b == 0) return dark;
            if (b < 7) return b == 3 ? gold : (x + y) % 6 == 0 && b > 1 && b < 6 ? cream : border;
            if (b == 7) return gold;
            float dx = (x - w / 2f + 0.5f) / 18f, dy = (y - h / 2f + 0.5f) / 28f;
            float d = MathF.Abs(dx) + MathF.Abs(dy);
            if (d < 0.35f) return gold;
            if (d < 0.5f) return border;
            if (MathF.Abs(d - 0.8f) < 0.06f) return gold;
            if (d < 0.8f) return dark;
            return (x / 4 + y / 4) % 7 == 0 ? dark : field;
        });
        return c;
    }

    /// <summary>64 x 64 round shag rug in teal rings.</summary>
    public static Canvas Round()
    {
        Rgba[] rings = [Rgba.Hex("#1d6f73"), Rgba.Hex("#2a9a96"), Rgba.Hex("#e7d9b0"), Rgba.Hex("#2a9a96")];
        return Canvas.Generate(64, 64, (x, y) =>
        {
            float d = MathF.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f));
            if (d > 30.5f) return Rgba.Transparent;
            if (d > 29.5f) return Rgba.Hex("#124548");
            Rgba c = rings[(int)(d / 5f) % rings.Length];
            return c.Tone(1f + (Noise.Hash(x, y, 72) - 0.5f) * 0.1f);
        });
    }
}
