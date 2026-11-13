namespace PlaceholderArt;

/// <summary>
/// 32x16 weapon sprites in the base colours: side profile laid along X, muzzle / tip towards +X, grips
/// pointing down (+Y). The thermal mod remaps these to greys.
/// </summary>
public static class Weapons
{
    static readonly Rgba Gunmetal = Rgba.Hex("#363b44"), GunDark = Rgba.Hex("#202328"), GunLight = Rgba.Hex("#7c8594");
    static readonly Rgba Wood = Rgba.Hex("#9a5f33"), WoodDark = Rgba.Hex("#5e3a1e"), WoodLight = Rgba.Hex("#c99060");
    static readonly Rgba Steel = Rgba.Hex("#c3cad3"), SteelDark = Rgba.Hex("#7d8590"), SteelLight = Rgba.Hex("#f3f7fb");

    public static IEnumerable<(string Name, Canvas Sprite)> All()
    {
        yield return ("bat", Bat());
        yield return ("pipe", Pipe());
        yield return ("knife", Knife());
        yield return ("machete", Machete());
        yield return ("pistol", Pistol());
        yield return ("revolver", Revolver());
        yield return ("shotgun", Shotgun());
        yield return ("smg", Smg());
        yield return ("rifle", Rifle());
        yield return ("flare_gun", FlareGun());
    }

    static Canvas New() => new(32, 16);

    /// <summary>Fill + 1 px top highlight strip for a horizontal rounded bar.</summary>
    static void Bar(Canvas c, float x0, float x1, float cy, float h, float r, Rgba main, Rgba dark, Rgba light)
    {
        c.Fill(Shape.RoundRect((x0 + x1) / 2f, cy, x1 - x0, h, r), dark);
        c.Fill(Shape.RoundRect((x0 + x1) / 2f, cy - 0.3f, x1 - x0 - 0.8f, h - 1f, MathF.Max(0f, r - 0.4f)), main);
        c.Fill(Shape.RoundRect((x0 + x1) / 2f, cy - h / 2f + 0.75f, x1 - x0 - 1.6f, 0.7f, 0.3f), light.Fade(0.8f));
    }

    static void TriggerGuard(Canvas c, float x, float y, Rgba color)
    {
        c.Fill(Shape.Circle(x, y, 2.1f).Minus(Shape.Circle(x, y, 1.25f)).And(Shape.Rect(x - 3f, y - 0.6f, 6f, 4f)), color);
        c.Fill(Shape.Capsule(x - 0.2f, y - 0.9f, x - 0.5f, y + 0.6f, 0.45f), color);
    }

    public static Canvas Bat()
    {
        var c = New();
        c.Fill(Shape.Capsule(4f, 8.6f, 28f, 7.8f, 1.15f, 2.7f), WoodDark);
        c.Fill(Shape.Capsule(4f, 8.3f, 27.8f, 7.4f, 0.7f, 2.1f), Wood);
        c.Fill(Shape.Capsule(9f, 7.5f, 27f, 6.3f, 0.3f, 0.8f), WoodLight.Fade(0.8f));
        c.Fill(Shape.Circle(3.2f, 8.7f, 1.8f), WoodDark);
        // grip tape
        c.Fill(Shape.Capsule(4.6f, 8.6f, 10.5f, 8.4f, 1.3f, 1.5f), Rgba.Hex("#24202a"));
        for (float x = 5.4f; x < 10.5f; x += 1.6f)
            c.Fill(Shape.Capsule(x, 7.3f, x + 0.6f, 9.8f, 0.25f), Rgba.Hex("#4a4452"));
        return c;
    }

    public static Canvas Pipe()
    {
        var c = New();
        Rgba main = Rgba.Hex("#8c949e"), dark = Rgba.Hex("#4c525b"), light = Rgba.Hex("#d7dde5");
        c.Fill(Shape.RoundRect(16f, 8f, 23f, 4f, 1.6f), dark);
        c.Fill(Shape.RoundRect(16f, 7.7f, 22f, 2.8f, 1.2f), main);
        c.Fill(Shape.RoundRect(16f, 6.9f, 21f, 0.8f, 0.4f), light.Fade(0.85f));
        // threaded fitting on the far end, rust flecks
        c.Fill(Shape.RoundRect(27.5f, 8f, 4f, 5.2f, 1f), dark);
        c.Fill(Shape.RoundRect(27.5f, 7.6f, 3.2f, 4f, 0.8f), Rgba.Hex("#757d87"));
        c.Fill(Shape.Rect(26.6f, 5.8f, 0.5f, 4.4f), dark.Fade(0.7f));
        c.Fill(Shape.Circle(9f, 8.6f, 0.7f), Rgba.Hex("#8a5a3a").Fade(0.7f));
        c.Fill(Shape.Circle(19f, 9f, 0.5f), Rgba.Hex("#8a5a3a").Fade(0.6f));
        return c;
    }

    public static Canvas Knife()
    {
        var c = New();
        // blade: straight spine on top, edge curving up to the point
        c.Fill(Shape.Polygon((15f, 6.4f), (24f, 6.2f), (28.8f, 8.1f), (23.6f, 9.8f), (15f, 9.8f)), SteelDark);
        c.Fill(Shape.Polygon((15f, 6.9f), (23.8f, 6.8f), (27.6f, 8.1f), (23.4f, 9.2f), (15f, 9.2f)), Steel);
        c.Fill(Shape.Polygon((15f, 8.9f), (23.4f, 8.9f), (27.4f, 8.2f), (23.6f, 9.6f), (15f, 9.6f)), SteelLight);
        c.Fill(Shape.RoundRect(14.6f, 8f, 1.6f, 5.4f, 0.6f), Rgba.Hex("#8f959e"));
        c.Fill(Shape.RoundRect(9.8f, 8f, 8.6f, 3.4f, 1.5f), Rgba.Hex("#2b2422"));
        c.Fill(Shape.RoundRect(9.8f, 7.4f, 7.6f, 1f, 0.5f), Rgba.Hex("#4a3f3a"));
        foreach (float x in new[] { 7.8f, 11.6f }) c.Fill(Shape.Circle(x, 8.1f, 0.55f), Rgba.Hex("#c8c0b0"));
        return c;
    }

    public static Canvas Machete()
    {
        var c = New();
        c.Fill(Shape.Polygon((11.5f, 5.6f), (26.2f, 4.8f), (30f, 5.8f), (28.4f, 10f), (11.5f, 10.4f)), Rgba.Hex("#5f6770"));
        c.Fill(Shape.Polygon((11.5f, 6.2f), (26f, 5.5f), (29f, 6.1f), (27.8f, 9.4f), (11.5f, 9.8f)), Rgba.Hex("#9aa2ab"));
        c.Fill(Shape.Polygon((11.5f, 9.0f), (28f, 8.7f), (27.8f, 9.4f), (11.5f, 9.8f)), Rgba.Hex("#e2e7ec"));
        c.Fill(Shape.Capsule(13f, 6.6f, 25f, 6.1f, 0.35f), Rgba.Hex("#6c747d"));
        c.Fill(Shape.RoundRect(11.2f, 8f, 1.4f, 6.2f, 0.5f), Rgba.Hex("#3a3e44"));
        c.Fill(Shape.RoundRect(6.8f, 8.1f, 8.2f, 3.2f, 1.4f), Rgba.Hex("#1f1d1c"));
        c.Fill(Shape.RoundRect(6.8f, 7.5f, 7.2f, 0.9f, 0.4f), Rgba.Hex("#3f3a36"));
        foreach (float x in new[] { 5f, 8.6f }) c.Fill(Shape.Circle(x, 8.2f, 0.5f), Rgba.Hex("#b0a898"));
        return c;
    }

    public static Canvas Pistol()
    {
        var c = New();
        c.Fill(Shape.Polygon((11.2f, 8.2f), (16.2f, 8.2f), (14.6f, 14.6f), (10.0f, 14.2f)), GunDark);       // grip
        c.Fill(Shape.Polygon((11.8f, 8.8f), (15.4f, 8.8f), (14.0f, 13.8f), (10.8f, 13.5f)), Rgba.Hex("#2b2f35"));
        for (float y = 10f; y < 13.5f; y += 1.2f) c.Fill(Shape.Capsule(11.4f, y, 14.6f, y + 0.2f, 0.22f), GunDark);
        TriggerGuard(c, 18.2f, 10f, GunDark);
        c.Fill(Shape.Rect(11f, 8.1f, 13f, 1.6f), GunDark);                                                 // frame
        Bar(c, 9f, 25.5f, 6.3f, 4.2f, 0.8f, Gunmetal, GunDark, GunLight);                                  // slide
        for (float x = 10.2f; x < 13f; x += 0.9f) c.Fill(Shape.Rect(x, 4.9f, 0.35f, 2.6f), GunDark);       // serrations
        c.Fill(Shape.Rect(24.2f, 3.6f, 0.9f, 0.9f), GunDark);                                              // front sight
        c.Fill(Shape.Rect(9.4f, 3.6f, 1.4f, 0.9f), GunDark);                                               // rear sight
        return c;
    }

    public static Canvas Revolver()
    {
        var c = New();
        c.Fill(Shape.Polygon((10.2f, 8.4f), (14.4f, 8.4f), (12.8f, 14.8f), (8.4f, 14.2f)), WoodDark);       // wooden grip
        c.Fill(Shape.Polygon((10.8f, 9.0f), (13.6f, 9.0f), (12.2f, 14.0f), (9.2f, 13.6f)), Wood);
        TriggerGuard(c, 16.2f, 10.2f, GunDark);
        c.Fill(Shape.Polygon((10.4f, 5.2f), (19f, 5.2f), (19f, 9.4f), (14.6f, 9.6f), (10.2f, 9.2f)), GunDark); // frame
        Bar(c, 17.5f, 29.5f, 6.4f, 2.8f, 0.6f, Gunmetal, GunDark, GunLight);                               // barrel
        c.Fill(Shape.Rect(17.5f, 7.6f, 7f, 1.3f), GunDark);                                                // ejector rod
        Bar(c, 12.6f, 18.6f, 7.2f, 5f, 1.2f, Rgba.Hex("#4a505a"), GunDark, GunLight);                      // cylinder
        foreach (float y in new[] { 6.1f, 8.2f }) c.Fill(Shape.Rect(13.4f, y, 4.4f, 0.45f), GunDark.Fade(0.8f));
        c.Fill(Shape.Polygon((9.6f, 5.4f), (11.6f, 4.4f), (11.6f, 6.4f)), GunDark);                        // hammer
        c.Fill(Shape.Rect(28.2f, 4.3f, 0.9f, 1f), GunDark);                                                // front sight
        return c;
    }

    public static Canvas Shotgun()
    {
        var c = New();
        c.Fill(Shape.Polygon((1.2f, 7.2f), (10.8f, 6.6f), (10.8f, 9.6f), (1.6f, 12.2f)), WoodDark);       // stock
        c.Fill(Shape.Polygon((2f, 7.8f), (10.4f, 7.2f), (10.4f, 9.0f), (2.3f, 11.4f)), Wood);
        c.Fill(Shape.Polygon((2.6f, 8.0f), (10f, 7.5f), (10f, 7.9f), (2.6f, 8.6f)), WoodLight.Fade(0.6f));
        c.Fill(Shape.Polygon((0.6f, 7.1f), (1.8f, 7.1f), (2.2f, 12.3f), (1.0f, 12.4f)), GunDark);          // butt pad
        TriggerGuard(c, 13.4f, 9.6f, GunDark);
        Bar(c, 10.2f, 17.6f, 7.4f, 3.8f, 0.8f, Gunmetal, GunDark, GunLight);                               // receiver
        c.Fill(Shape.RoundRect(23f, 8.6f, 11f, 1.6f, 0.6f), GunDark);                                      // magazine tube
        Bar(c, 17f, 31.2f, 6.6f, 2f, 0.5f, Rgba.Hex("#3e434b"), GunDark, GunLight);                        // barrel
        Bar(c, 19.2f, 26.4f, 8.7f, 3f, 1f, Wood, WoodDark, WoodLight);                                     // pump
        for (float x = 20.4f; x < 26f; x += 1.3f) c.Fill(Shape.Rect(x, 8.2f, 0.4f, 1.8f), WoodDark.Fade(0.8f));
        c.Fill(Shape.Circle(30.4f, 5.5f, 0.55f), GunLight);                                                // bead sight
        return c;
    }

    public static Canvas Smg()
    {
        var c = New();
        var wire = Rgba.Hex("#2a2e34");
        c.Fill(Shape.Capsule(8.5f, 6.0f, 2.6f, 6.4f, 0.55f), wire);                                        // folding stock
        c.Fill(Shape.Capsule(2.6f, 6.4f, 2.6f, 10.2f, 0.65f), wire);
        c.Fill(Shape.Capsule(2.6f, 10.2f, 8.6f, 8.4f, 0.55f), wire);
        c.Fill(Shape.Polygon((14.6f, 8.6f), (17.6f, 8.6f), (18.0f, 15.4f), (15.2f, 15.4f)), GunDark);      // magazine
        c.Fill(Shape.Polygon((15.2f, 9f), (17f, 9f), (17.3f, 14.8f), (15.7f, 14.8f)), Rgba.Hex("#2c3036"));
        c.Fill(Shape.Polygon((9.8f, 8.4f), (12.8f, 8.4f), (11.8f, 13.4f), (9.0f, 13.0f)), GunDark);        // grip
        TriggerGuard(c, 13.4f, 9.6f, GunDark);
        Bar(c, 7.6f, 23.6f, 6.8f, 4.2f, 0.9f, Gunmetal, GunDark, GunLight);                                // body
        Bar(c, 23f, 29.8f, 6.6f, 2.4f, 0.6f, Rgba.Hex("#2f333a"), GunDark, GunLight);                      // barrel shroud
        foreach (float x in new[] { 24.4f, 26f, 27.6f }) c.Fill(Shape.Circle(x, 6.6f, 0.4f), GunDark);
        c.Fill(Shape.Rect(19f, 8.6f, 4f, 1f), GunDark);
        c.Fill(Shape.Rect(10f, 3.8f, 1.2f, 1.2f), GunDark);
        c.Fill(Shape.Rect(21.8f, 3.8f, 0.9f, 1.2f), GunDark);
        return c;
    }

    public static Canvas Rifle()
    {
        var c = New();
        c.Fill(Shape.Polygon((0.8f, 6.4f), (9f, 6.2f), (9f, 9.4f), (1.2f, 11.2f)), WoodDark);              // stock
        c.Fill(Shape.Polygon((1.6f, 7.0f), (8.6f, 6.8f), (8.6f, 8.8f), (1.9f, 10.4f)), Wood);
        c.Fill(Shape.Polygon((14.2f, 8.4f), (17f, 8.4f), (18.4f, 15.2f), (15.6f, 15.6f), (14.6f, 11.8f)), GunDark); // curved magazine
        c.Fill(Shape.Polygon((10.4f, 8.6f), (12.6f, 8.6f), (11.6f, 12.8f), (9.4f, 12.4f)), GunDark);       // grip
        TriggerGuard(c, 13f, 9.6f, GunDark);
        Bar(c, 8.4f, 21f, 7f, 3.8f, 0.8f, Gunmetal, GunDark, GunLight);                                    // receiver
        Bar(c, 20.4f, 27f, 7.1f, 3.2f, 1f, Wood, WoodDark, WoodLight);                                     // handguard
        Bar(c, 26.6f, 31.6f, 6.8f, 1.4f, 0.4f, Rgba.Hex("#3e434b"), GunDark, GunLight);                    // barrel
        c.Fill(Shape.Rect(27.6f, 3.9f, 0.9f, 2.4f), GunDark);                                              // front sight post
        c.Fill(Shape.RoundRect(13.5f, 4.4f, 7f, 1.4f, 0.5f), GunDark);                                     // carry rail
        c.Fill(Shape.Rect(10.6f, 4.4f, 0.9f, 1.6f), GunDark);
        c.Fill(Shape.Rect(16.4f, 4.4f, 0.9f, 1.6f), GunDark);
        return c;
    }

    public static Canvas FlareGun()
    {
        var c = New();
        Rgba orange = Rgba.Hex("#ff7a1a"), dark = Rgba.Hex("#b8480a"), light = Rgba.Hex("#ffb466");
        c.Fill(Shape.Polygon((9.8f, 8.6f), (14.4f, 8.6f), (13f, 14.8f), (8.6f, 14.2f)), dark);            // grip
        c.Fill(Shape.Polygon((10.4f, 9.2f), (13.6f, 9.2f), (12.4f, 14.0f), (9.4f, 13.6f)), orange);
        for (float y = 10.4f; y < 13.6f; y += 1.3f) c.Fill(Shape.Capsule(10.2f, y, 13.2f, y + 0.2f, 0.25f), dark);
        TriggerGuard(c, 15.6f, 10.4f, Rgba.Hex("#1e1e22"));
        Bar(c, 9f, 15.6f, 7.4f, 4.2f, 1f, orange, dark, light);                                            // frame
        Bar(c, 13.6f, 26.8f, 6.8f, 5.2f, 1.5f, orange, dark, light);                                       // fat barrel
        c.Fill(Shape.Rect(13.6f, 8.8f, 13f, 0.5f), dark.Fade(0.8f));
        c.Fill(Shape.Ellipse(26.6f, 6.8f, 0.8f, 2.1f), Rgba.Hex("#3a1a06"));                               // bore
        c.Fill(Shape.Polygon((8.2f, 5.8f), (10.4f, 4.6f), (10.6f, 6.6f)), Rgba.Hex("#1e1e22"));           // hammer
        return c;
    }
}
