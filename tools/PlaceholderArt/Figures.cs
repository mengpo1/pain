namespace PlaceholderArt;

public enum HeadKind
{
    Helmet,
    Bare,
    Hood,
    Alien,
    Skull,
}

/// <summary>Look of a unit: colours, head, build and how long the held weapon is.</summary>
public sealed record FigureStyle(
    Rgba Body,
    Rgba Accent,
    Rgba Skin,
    Rgba Weapon,
    HeadKind Head,
    float Height = 44f,
    float Bulk = 1f,
    float WeaponLength = 14f,
    float HeadSize = 1f);

/// <summary>
/// Unit sprite sheets: 17 frames of 64 × 80 in one row — 0–7 standing by direction (0 = north,
/// clockwise), 8–15 kneeling, 16 lying. Feet stand on the centre of the floor diamond (32, 64).
/// </summary>
public static class Figures
{
    public const int Frames = 17;
    private const float FeetX = 32f;
    private const float FeetY = 64f;

    public static Canvas Sheet(FigureStyle style)
    {
        var sheet = new Canvas(IsoDraw.FrameWidth * Frames, IsoDraw.FrameHeight);
        for (int d = 0; d < 8; d++)
        {
            sheet.Draw(Standing(style, d, kneeling: false), d * IsoDraw.FrameWidth);
            sheet.Draw(Standing(style, d, kneeling: true), (8 + d) * IsoDraw.FrameWidth);
        }

        sheet.Draw(Lying(style), 16 * IsoDraw.FrameWidth);
        return sheet;
    }

    /// <summary>Screen-space unit vector of a facing direction (world north = up-right on screen).</summary>
    public static (float X, float Y) Facing(int direction)
    {
        (int dx, int dy) = direction switch
        {
            0 => (0, -1), 1 => (1, -1), 2 => (1, 0), 3 => (1, 1),
            4 => (0, 1), 5 => (-1, 1), 6 => (-1, 0), _ => (-1, -1),
        };
        float sx = dx - dy, sy = (dx + dy) * 0.5f;
        float length = MathF.Sqrt(sx * sx + sy * sy);
        return (sx / length, sy / length);
    }

    private static Canvas Standing(FigureStyle s, int direction, bool kneeling)
    {
        var c = new Canvas(IsoDraw.FrameWidth, IsoDraw.FrameHeight);
        (float fx, float fy) = Facing(direction);
        (float px, float py) = (-fy, fx); // perpendicular: the figure's right/left on screen
        float height = kneeling ? s.Height * 0.66f : s.Height;
        float bulk = s.Bulk;

        c.Fill(Shape.Ellipse(FeetX, FeetY, 9f * bulk, 4.5f * bulk), Rgba.Black.WithAlpha(0.32f));

        float hipY = FeetY - height * 0.45f;
        float shoulderY = FeetY - height * 0.78f;
        float headY = FeetY - height * 0.9f;
        bool facingAway = fy < -0.05f;

        Rgba legs = s.Body.Shade(0.72f);
        if (kneeling)
        {
            // One knee forward, one leg folded under.
            c.Fill(Shape.Capsule(FeetX + px * 3f, hipY, FeetX + fx * 7f + px * 3f, FeetY - 3f + fy * 3f, 2.6f * bulk), legs);
            c.Fill(Shape.Capsule(FeetX - px * 3f, hipY, FeetX - fx * 4f - px * 3f, FeetY - 1f, 2.6f * bulk), legs.Shade(0.9f));
        }
        else
        {
            c.Fill(Shape.Capsule(FeetX + px * 3f, hipY, FeetX + px * 3.4f + fx * 1.5f, FeetY - 1f, 2.6f * bulk), legs);
            c.Fill(Shape.Capsule(FeetX - px * 3f, hipY, FeetX - px * 3.4f - fx * 1.5f, FeetY - 1f, 2.6f * bulk), legs.Shade(0.88f));
        }

        if (facingAway) Weapon(c, s, fx, fy, px, shoulderY, behind: true);

        // Torso with a lighter chest plate facing the direction.
        c.Fill(Shape.RoundRect(FeetX, (hipY + shoulderY) / 2f, 12f * bulk, (hipY - shoulderY) + 5f, 4f), s.Body);
        c.Fill(Shape.Ellipse(FeetX + fx * 2.5f, (hipY + shoulderY) / 2f + fy * 1.5f, 4.5f * bulk, (hipY - shoulderY) * 0.38f), s.Accent.WithAlpha(facingAway ? 0.35f : 0.9f));
        c.Fill(Shape.Circle(FeetX + px * 6.5f * bulk, shoulderY + 1.5f, 3.2f * bulk), s.Body.Shade(0.9f));
        c.Fill(Shape.Circle(FeetX - px * 6.5f * bulk, shoulderY + 1.5f, 3.2f * bulk), s.Body.Shade(0.9f));

        Head(c, s, fx, fy, FeetX, headY);

        if (!facingAway) Weapon(c, s, fx, fy, px, shoulderY, behind: false);
        c.Outline(Rgba.Black.WithAlpha(0.7f), 0.8f);
        return c;
    }

    private static void Weapon(Canvas c, FigureStyle s, float fx, float fy, float px, float shoulderY, bool behind)
    {
        float baseX = FeetX + px * 4f + fx * 2f;
        float baseY = shoulderY + 5f;
        float tipX = baseX + fx * s.WeaponLength;
        float tipY = baseY + fy * s.WeaponLength * 0.8f + 1f;
        Rgba color = behind ? s.Weapon.Shade(0.75f) : s.Weapon;
        if (s.WeaponLength <= 0f) return;
        c.Fill(Shape.Capsule(baseX, baseY, tipX, tipY, 1.6f), color);
        // Hands.
        c.Fill(Shape.Circle(baseX + fx * 2f, baseY + fy * 1.5f, 1.8f), s.Skin.Shade(behind ? 0.7f : 1f));
        c.Fill(Shape.Circle(baseX + fx * s.WeaponLength * 0.55f, baseY + fy * s.WeaponLength * 0.45f, 1.7f), s.Skin.Shade(behind ? 0.7f : 0.95f));
    }

    private static void Head(Canvas c, FigureStyle s, float fx, float fy, float x, float y)
    {
        float r = 4.6f * s.HeadSize;
        switch (s.Head)
        {
            case HeadKind.Helmet:
                c.Fill(Shape.Circle(x, y, r), s.Accent.Shade(0.8f));
                c.Fill(Shape.Ellipse(x + fx * 2.2f, y + 0.8f + fy * 1.2f, r * 0.62f, r * 0.34f), new Rgba(0.55f, 0.85f, 1f).WithAlpha(fy < -0.05f ? 0f : 0.95f));
                break;
            case HeadKind.Hood:
                c.Fill(Shape.Circle(x, y, r * 1.05f), s.Accent);
                c.Fill(Shape.Circle(x + fx * 1.8f, y + 1f + fy * 1.2f, r * 0.55f), s.Skin.WithAlpha(fy < -0.05f ? 0f : 1f));
                break;
            case HeadKind.Alien:
                c.Fill(Shape.Ellipse(x, y - 1.5f, r * 1.05f, r * 1.3f), s.Skin);
                if (fy >= -0.05f)
                {
                    c.Fill(Shape.Ellipse(x + fx * 1.5f - 1.8f, y - 1f, 1.5f, 1f, 0.4f), Rgba.Black);
                    c.Fill(Shape.Ellipse(x + fx * 1.5f + 1.8f, y - 1f, 1.5f, 1f, -0.4f), Rgba.Black);
                }
                break;
            case HeadKind.Skull:
                c.Fill(Shape.Circle(x, y, r), s.Skin);
                if (fy >= -0.05f)
                {
                    c.Fill(Shape.Circle(x + fx * 1.4f - 1.6f, y, 1.2f), Rgba.Black.WithAlpha(0.85f));
                    c.Fill(Shape.Circle(x + fx * 1.4f + 1.6f, y, 1.2f), Rgba.Black.WithAlpha(0.85f));
                }
                break;
            default:
                c.Fill(Shape.Circle(x, y, r), s.Skin);
                c.Fill(Shape.Circle(x - fx * 1.2f, y - 1.6f, r * 0.85f), s.Accent.Shade(0.6f).WithAlpha(0.9f));
                break;
        }
    }

    private static Canvas Lying(FigureStyle s)
    {
        var c = new Canvas(IsoDraw.FrameWidth, IsoDraw.FrameHeight);
        c.Fill(Shape.Ellipse(FeetX, FeetY, 15f * s.Bulk, 6f * s.Bulk, -0.46f), Rgba.Black.WithAlpha(0.3f));
        c.Fill(Shape.Capsule(FeetX - 11f, FeetY + 3f, FeetX + 8f, FeetY - 5f, 4f * s.Bulk), s.Body.Shade(0.8f));
        c.Fill(Shape.Ellipse(FeetX - 1f, FeetY - 1f, 6f * s.Bulk, 4f * s.Bulk, -0.46f), s.Accent.Shade(0.8f));
        c.Fill(Shape.Circle(FeetX + 11f, FeetY - 7f, 4f * s.HeadSize), s.Head == HeadKind.Helmet ? s.Accent.Shade(0.7f) : s.Skin.Shade(0.85f));
        c.Outline(Rgba.Black.WithAlpha(0.6f), 0.8f);
        return c;
    }
}
