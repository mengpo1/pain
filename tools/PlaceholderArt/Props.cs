namespace PlaceholderArt;

/// <summary>Top-down props in the base colours (the thermal mod remaps them to greys).</summary>
public static class Props
{
    /// <summary>Soft contact shadow under a prop.</summary>
    static void Shadow(Canvas c, Shape shape, float opacity = 0.4f, float blur = 1.2f)
    {
        var s = new Canvas(c.Width, c.Height);
        s.Fill(shape, Rgba.Black.Fade(opacity));
        s.Blur(blur);
        c.Draw(s);
    }

    public static Canvas Crate(bool shadow = true)
    {
        var c = new Canvas(32, 32);
        Rgba frame = Rgba.Hex("#8a5a2c"), panel = Rgba.Hex("#b98448"), dark = Rgba.Hex("#4e3016"), light = Rgba.Hex("#e0b47a");
        if (shadow) Shadow(c, Shape.RoundRect(17f, 17.5f, 28f, 28f, 2f));
        c.Fill(Shape.RoundRect(16f, 16f, 28f, 28f, 1.5f), dark);
        c.Fill(Shape.RoundRect(16f, 16f, 26.4f, 26.4f, 1.2f), frame);
        c.Fill(Shape.Rect(5.5f, 5.5f, 21f, 21f), dark);
        c.Fill(Shape.Rect(6f, 6f, 20f, 20f), panel);
        foreach (float y in new[] { 12.4f, 19.2f }) c.Fill(Shape.Rect(6f, y, 20f, 0.8f), dark.Fade(0.6f));
        var rng = new Random(91);
        for (int i = 0; i < 7; i++)
        {
            float y = 7f + rng.NextSingle() * 18f, x = 6.5f + rng.NextSingle() * 10f;
            c.Fill(Shape.Capsule(x, y, x + 3f + rng.NextSingle() * 5f, y + 0.1f, 0.2f), dark.Fade(0.35f));
        }
        c.Fill(Shape.Capsule(6.4f, 6.4f, 25.6f, 25.6f, 2.3f), dark);
        c.Fill(Shape.Capsule(6.4f, 6.2f, 25.6f, 25.4f, 1.6f), frame);
        c.Fill(Shape.Rect(3f, 2.9f, 26f, 0.9f), light.Fade(0.7f));
        c.Fill(Shape.Rect(2.9f, 3f, 0.9f, 26f), light.Fade(0.45f));
        foreach (var (x, y) in new[] { (4.6f, 4.6f), (27.4f, 4.6f), (4.6f, 27.4f), (27.4f, 27.4f) })
            c.Fill(Shape.Circle(x, y, 0.75f), Rgba.Hex("#d8d0c0"));
        return c;
    }

    public static Canvas Table(bool shadow = true)
    {
        var c = new Canvas(64, 32);
        Rgba wood = Rgba.Hex("#8e5a2e"), dark = Rgba.Hex("#4f2e14"), light = Rgba.Hex("#c48a52");
        if (shadow) Shadow(c, Shape.RoundRect(33f, 17.5f, 60f, 28f, 3f));
        c.Fill(Shape.RoundRect(32f, 16f, 60f, 28f, 3f), dark);
        c.Fill(Shape.RoundRect(32f, 15.8f, 58f, 26f, 2.4f), (x, y) =>
            wood.Tone(0.86f + 0.24f * Noise.Tile(x / 64f, y / 32f, 3, 20, 3, 92)));
        c.Fill(Shape.Rect(3f, 15.6f, 58f, 0.7f), dark.Fade(0.55f));                     // plank seam
        c.Fill(Shape.RoundRect(32f, 3.6f, 56f, 0.9f, 0.4f), light.Fade(0.6f));          // lit edge
        // a sheet of paper with a few lines of text
        c.Fill(Shape.RoundRect(21.2f, 16.6f, 10f, 13f, 0.4f, 0.22f), Rgba.Black.Fade(0.25f));
        c.Fill(Shape.RoundRect(20.6f, 16f, 10f, 13f, 0.4f, 0.22f), Rgba.Hex("#ece6d6"));
        for (int i = 0; i < 4; i++)
        {
            float y = 12.2f + i * 2.4f, x = 17.6f - (y - 16f) * 0.22f;
            c.Fill(Shape.Capsule(x, y, x + 5.5f - (i == 3 ? 2.5f : 0f), y + 1.25f, 0.35f), Rgba.Hex("#6e6a70").Fade(0.7f));
        }
        // a mug of coffee
        c.Fill(Shape.Circle(45.6f, 11.6f, 3.6f), Rgba.Black.Fade(0.25f));
        c.Fill(Shape.Capsule(47.6f, 10.4f, 49.8f, 9.6f, 0.9f), Rgba.Hex("#e8e4ee"));
        c.Fill(Shape.Circle(45f, 11f, 3.4f), Rgba.Hex("#e8e4ee"));
        c.Fill(Shape.Circle(45f, 11f, 2.4f), Rgba.Hex("#3a2214"));
        c.Fill(Shape.Ellipse(44.2f, 10.2f, 0.9f, 0.5f, -0.5f), Rgba.Hex("#8a6a50"));
        return c;
    }

    public static Canvas Plant()
    {
        var c = new Canvas(32, 32);
        Shadow(c, Shape.Circle(16.8f, 17.2f, 9f));
        c.Fill(Shape.Circle(16f, 16f, 8.2f), Rgba.Hex("#8e3f22"));
        c.Fill(Shape.Circle(16f, 16f, 7.6f), Rgba.Hex("#d07448"));
        c.Fill(Shape.Circle(16f, 16f, 6.4f), Rgba.Hex("#3a2414"));
        Rgba[] greens = [Rgba.Hex("#1f6a32"), Rgba.Hex("#2e8a3e"), Rgba.Hex("#3faa4e")];
        var rng = new Random(93);
        for (int ring = 0; ring < 2; ring++)
        {
            int count = ring == 0 ? 7 : 5;
            for (int i = 0; i < count; i++)
            {
                float a = i * 6.283f / count + ring * 0.4f + (rng.NextSingle() - 0.5f) * 0.7f;
                float len = (ring == 0 ? 5.4f : 4.4f) + (rng.NextSingle() - 0.5f) * 2.4f, d = (ring == 0 ? 2.2f : 0.6f) + len;
                float w = 1.9f + rng.NextSingle() * 0.9f;
                float lx = 16f + MathF.Cos(a) * d, ly = 16f + MathF.Sin(a) * d;
                var leaf = Shape.Ellipse(lx, ly, len, w, a);
                c.Fill(leaf, greens[ring == 0 ? 0 : 1]);
                c.Fill(Shape.Ellipse(lx - 0.25f, ly - 0.3f, len - 0.9f, w - 0.7f, a), greens[ring == 0 ? 1 : 2]);
                c.Fill(Shape.Capsule(lx - MathF.Cos(a) * (len - 1.2f), ly - MathF.Sin(a) * (len - 1.2f), lx + MathF.Cos(a) * (len - 1.5f), ly + MathF.Sin(a) * (len - 1.5f), 0.25f),
                    Rgba.Hex("#a6e08a").Fade(0.55f));
            }
        }
        c.Outline(Rgba.Hex("#0e1a10").Fade(0.7f));
        return c;
    }

    /// <summary>96x48 sports car seen from above, nose towards +X.</summary>
    public static Canvas Car(bool shadow = true)
    {
        var c = new Canvas(96, 48);
        Rgba paint = Rgba.Hex("#e0306a"), shade = Rgba.Hex("#8e163e"), light = Rgba.Hex("#ff7aa6");
        Rgba glass = Rgba.Hex("#1c2446"), glassLight = Rgba.Hex("#5ad8ff");
        if (shadow) Shadow(c, Shape.RoundRect(49f, 25.5f, 90f, 40f, 9f), 0.45f, 1.6f);
        foreach (float x in new[] { 21f, 75f })                                                     // tyres
            foreach (float y in new[] { 5.2f, 42.8f })
                c.Fill(Shape.RoundRect(x, y, 14f, 5f, 1.8f), Rgba.Hex("#121216"));
        c.Fill(Shape.RoundRect(48f, 24f, 90f, 38f, 9f), shade);
        c.Fill(Shape.RoundRect(48f, 23.6f, 87.6f, 35.4f, 8f), paint);
        c.Fill(Shape.RoundRect(48f, 9.8f, 80f, 2f, 1f), light.Fade(0.6f));                         // shoulder line
        foreach (float y in new[] { 20.6f, 27.4f })                                                 // racing stripes
            c.Fill(Shape.Rect(4f, y - 1.1f, 88f, 2.2f), Rgba.Hex("#fff0f6").Fade(0.85f));
        // cabin: rear window, roof, windscreen
        c.Fill(Shape.Polygon((22f, 9f), (30f, 7.6f), (30f, 40.4f), (22f, 39f)), glass);
        c.Fill(Shape.RoundRect(43f, 24f, 28f, 32f, 4f), shade);
        c.Fill(Shape.RoundRect(43f, 23.6f, 26.4f, 30.4f, 3.6f), paint.Tone(1.08f));
        foreach (float y in new[] { 20.6f, 27.4f }) c.Fill(Shape.Rect(30f, y - 1.1f, 26f, 2.2f), Rgba.Hex("#fff0f6").Fade(0.85f));
        c.Fill(Shape.Polygon((56.6f, 7.4f), (67f, 9.6f), (67f, 38.4f), (56.6f, 40.6f)), glass);
        c.Fill(Shape.Polygon((59f, 10f), (61.5f, 10.5f), (61.5f, 22f), (59f, 23f)), glassLight.Fade(0.55f));
        c.Fill(Shape.Polygon((24f, 10f), (25.6f, 9.8f), (25.6f, 18f), (24f, 18.4f)), glassLight.Fade(0.4f));
        foreach (float y in new[] { 3.6f, 44.4f }) c.Fill(Shape.Ellipse(58f, y, 2.4f, 1.4f), shade); // mirrors
        foreach (float y in new[] { 9.4f, 38.6f })
        {
            c.Fill(Shape.RoundRect(90.6f, y, 2.6f, 7f, 1.2f), Rgba.Hex("#fff6c8"));                // headlights
            c.Fill(Shape.RoundRect(4.6f, y, 2.4f, 7f, 1.1f), Rgba.Hex("#ff2a3a"));                 // tail lights
        }
        c.Fill(Shape.Rect(78f, 15f, 0.8f, 18f), shade.Fade(0.6f));                                  // hood seam
        c.Outline(Rgba.Hex("#140c1c").Fade(0.85f));
        return c;
    }
}
