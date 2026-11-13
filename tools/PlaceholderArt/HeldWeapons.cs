namespace PlaceholderArt;

/// <summary>
/// Weapons as seen from above in a character's hands, drawn pixel by pixel (the side views in
/// <see cref="Weapons"/> stay for weapons lying on the floor). Each sprite points to +X; <c>Grip</c> is the
/// pixel the hand holds, measured from the sprite centre, and goes into the weapon's data.
/// </summary>
public static class HeldWeapons
{
    static readonly Rgba Ink = Rgba.Hex("#140c1c");
    static readonly Rgba GunDark = Rgba.Hex("#1d2026"), Gun = Rgba.Hex("#3a404a"), GunLight = Rgba.Hex("#8c96a6");
    static readonly Rgba WoodDark = Rgba.Hex("#5e3a1e"), Wood = Rgba.Hex("#9a5f33"), WoodLight = Rgba.Hex("#cf9663");
    static readonly Rgba SteelDark = Rgba.Hex("#7d8590"), Steel = Rgba.Hex("#c3cad3"), SteelLight = Rgba.Hex("#f3f7fb");
    static readonly Rgba Tape = Rgba.Hex("#26222c"), TapeLight = Rgba.Hex("#4a4452");

    public sealed record Held(string Name, Canvas Sprite, float GripX);

    public static IEnumerable<Held> All()
    {
        yield return Make("pistol", 20, 5, c =>
        {
            Bar(c, 3, 15, 3, 2, Gun, GunLight);
            Px(c, 15, 3, GunDark); Px(c, 15, 4, GunDark);
            Px(c, 13, 3, GunLight);
        });
        yield return Make("revolver", 22, 5, c =>
        {
            Bar(c, 3, 6, 3, 2, GunDark, Gun);
            Bar(c, 6, 10, 2, 4, Gun, GunLight);
            Bar(c, 10, 18, 3, 2, Gun, GunLight);
            Px(c, 18, 3, GunDark);
            Px(c, 7, 3, GunDark); Px(c, 8, 4, GunDark);
        });
        yield return Make("flare_gun", 20, 5, c =>
        {
            Rgba orange = Rgba.Hex("#ff7a1a"), dark = Rgba.Hex("#b8480a"), light = Rgba.Hex("#ffc078");
            Bar(c, 3, 7, 3, 2, dark, orange);
            Bar(c, 7, 16, 2, 4, orange, light);
            Px(c, 16, 3, Rgba.Hex("#3a1a06")); Px(c, 16, 4, Rgba.Hex("#3a1a06"));
        });
        yield return Make("shotgun", 40, 12, c =>
        {
            Bar(c, 3, 11, 3, 3, Wood, WoodLight);
            Px(c, 3, 5, WoodDark); Px(c, 2, 4, GunDark); Px(c, 2, 3, GunDark); Px(c, 2, 5, GunDark);
            Bar(c, 11, 17, 3, 2, Gun, GunLight);
            Bar(c, 17, 32, 3, 2, GunDark, Gun);
            Bar(c, 18, 24, 3, 3, Wood, WoodLight);
            for (int x = 19; x < 24; x += 2) Px(c, x, 5, WoodDark);
            Px(c, 31, 3, GunLight);
        });
        yield return Make("rifle", 40, 12, c =>
        {
            Bar(c, 2, 10, 3, 3, Wood, WoodLight);
            Px(c, 1, 3, GunDark); Px(c, 1, 4, GunDark); Px(c, 1, 5, GunDark);
            Bar(c, 10, 21, 3, 2, Gun, GunLight);
            Bar(c, 13, 19, 2, 1, GunDark, GunDark);
            Bar(c, 21, 27, 3, 3, Wood, WoodLight);
            Bar(c, 27, 35, 3, 2, GunDark, Gun);
            Px(c, 33, 2, GunDark);
        });
        yield return Make("smg", 28, 9, c =>
        {
            for (int x = 2; x < 8; x++) { Px(c, x, 2, GunDark); Px(c, x, 5, GunDark); }
            Px(c, 2, 3, GunDark); Px(c, 2, 4, GunDark);
            Bar(c, 8, 19, 3, 2, Gun, GunLight);
            Bar(c, 19, 24, 3, 2, GunDark, Gun);
            Px(c, 20, 3, GunDark); Px(c, 22, 3, GunDark);
            Bar(c, 13, 15, 5, 2, GunDark, GunDark);
        });
        yield return Make("bat", 32, 5, c =>
        {
            Px(c, 1, 3, WoodDark); Px(c, 1, 4, WoodDark);
            Bar(c, 2, 9, 3, 2, Tape, TapeLight);
            for (int x = 3; x < 9; x += 2) Px(c, x, 4, TapeLight);
            Bar(c, 9, 15, 3, 2, Wood, WoodLight);
            Bar(c, 15, 21, 2, 3, Wood, WoodLight);
            Bar(c, 21, 30, 2, 4, Wood, WoodLight);
            Px(c, 30, 3, Wood); Px(c, 30, 4, Wood);
        });
        yield return Make("pipe", 28, 6, c =>
        {
            Rgba main = Rgba.Hex("#8c949e"), light = Rgba.Hex("#d7dde5"), dark = Rgba.Hex("#4c525b");
            Bar(c, 2, 23, 3, 2, main, light);
            Bar(c, 23, 26, 2, 4, Rgba.Hex("#757d87"), light);
            Px(c, 24, 2, dark); Px(c, 24, 5, dark);
            Px(c, 12, 4, Rgba.Hex("#8a5a3a"));
        });
        yield return Make("knife", 20, 6, c =>
        {
            Bar(c, 3, 9, 3, 2, Rgba.Hex("#2b2422"), Rgba.Hex("#4a3f3a"));
            Bar(c, 9, 10, 2, 4, SteelDark, SteelDark);
            Bar(c, 10, 16, 3, 2, Steel, SteelLight);
            Px(c, 16, 4, Steel);
        });
        yield return Make("machete", 28, 5, c =>
        {
            Bar(c, 2, 8, 3, 2, Rgba.Hex("#1f1d1c"), Rgba.Hex("#3f3a36"));
            Bar(c, 8, 9, 2, 4, Rgba.Hex("#3a3e44"), Rgba.Hex("#3a3e44"));
            Bar(c, 9, 23, 3, 3, Rgba.Hex("#9aa2ab"), SteelLight);
            Px(c, 23, 3, Rgba.Hex("#9aa2ab")); Px(c, 23, 4, Rgba.Hex("#9aa2ab")); Px(c, 24, 3, Rgba.Hex("#9aa2ab"));
            for (int x = 9; x < 23; x++) Px(c, x, 5, SteelDark);
        });
    }

    /// <summary>A canvas 8 px tall, the weapon along row 3-4, outlined; the grip is reported from the centre.</summary>
    static Held Make(string name, int width, int gripPixel, Action<Canvas> draw)
    {
        var c = new Canvas(width, 8);
        draw(c);
        Rig.TopDownRenderer.HardOutline(c, Ink);
        return new Held(name, c, gripPixel + 0.5f - width / 2f);
    }

    static void Px(Canvas c, int x, int y, Rgba color) => c[x, y] = color;

    /// <summary>Solid run of pixels from x0 to x1 (exclusive), <paramref name="h"/> rows from y0, with a lit top row.</summary>
    static void Bar(Canvas c, int x0, int x1, int y0, int h, Rgba main, Rgba light)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x1; x++)
                c[x, y] = y == y0 && h > 1 ? light : main;
    }
}
