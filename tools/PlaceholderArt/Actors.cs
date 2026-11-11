namespace PlaceholderArt;

/// <summary>
/// Colours and proportions for a top-down human. The geometry in <see cref="Actors"/> only reads colours from
/// here, so the same figures serve the colourful base pack and the greyscale thermal mod.
/// </summary>
public sealed record HumanLook
{
    public required Rgba Body { get; init; }
    public required Rgba BodyShade { get; init; }
    public required Rgba BodyLight { get; init; }
    public required Rgba Skin { get; init; }
    public required Rgba SkinShade { get; init; }
    /// <summary>Null = bald.</summary>
    public Rgba? Hair { get; init; }
    public required Rgba Pants { get; init; }
    public required Rgba Shoes { get; init; }
    public Rgba? Collar { get; init; }
    /// <summary>Tank top: bare shoulders and arms.</summary>
    public bool BareArms { get; init; }
    /// <summary>Overall size (1 = regular 32 px figure).</summary>
    public float Scale { get; init; } = 1f;
    /// <summary>Extra width across the shoulders (Y axis).</summary>
    public float Bulk { get; init; } = 1f;
}

public sealed record DogLook(Rgba Fur, Rgba FurDark, Rgba Belly, Rgba Nose);

public sealed record CreatureLook
{
    public required Rgba Body { get; init; }
    public required Rgba BodyDark { get; init; }
    public required Rgba Rim { get; init; }
    public required Rgba Limb { get; init; }
    public required Rgba LimbLight { get; init; }
    public required Rgba Claw { get; init; }
    public required Rgba Eye { get; init; }
    public required Rgba Maw { get; init; }
    /// <summary>Limb length multiplier.</summary>
    public float Reach { get; init; } = 1f;
    /// <summary>Limb thickness multiplier.</summary>
    public float Thickness { get; init; } = 1f;
}

/// <summary>
/// Top-down characters, always facing +X. Local coordinates have the body centre at the origin;
/// +Y is the character's right-hand side.
/// </summary>
public static class Actors
{
    static readonly int[] Sides = [-1, 1];

    /// <summary>Maps local body coordinates to canvas pixels.</summary>
    readonly record struct Frame(float Cx, float Cy, float S, float Bulk)
    {
        public float X(float x) => Cx + x * S;
        public float Y(float y) => Cy + y * S * Bulk;
        public float R(float r) => r * S;
    }

    static float HeadRadius(HumanLook l) => 5.1f * (1f + (l.Scale - 1f) * 0.45f);

    /// <summary>Two-tone limb segment: a darker rim and a lighter core nudged towards the top of the image.</summary>
    static void Limb(Canvas c, Frame f, (float X, float Y) a, (float X, float Y) b, float ra, float rb, Rgba shade, Rgba main)
    {
        c.Fill(Shape.Capsule(f.X(a.X), f.Y(a.Y), f.X(b.X), f.Y(b.Y), f.R(ra), f.R(rb)), shade);
        c.Fill(Shape.Capsule(f.X(a.X), f.Y(a.Y) - 0.35f, f.X(b.X), f.Y(b.Y) - 0.35f,
            MathF.Max(0.2f, f.R(ra) - 0.75f), MathF.Max(0.2f, f.R(rb) - 0.75f)), main);
    }

    static void Hand(Canvas c, HumanLook l, float x, float y, float r)
    {
        c.Fill(Shape.Circle(x, y, r), l.SkinShade);
        c.Fill(Shape.Circle(x - 0.2f, y - 0.35f, r - 0.6f), l.Skin);
    }

    // ------------------------------------------------------------------ standing human

    public static void HumanStanding(Canvas c, HumanLook l, float cx, float cy)
    {
        var f = new Frame(cx, cy, l.Scale, l.Bulk);
        float limb = l.BareArms ? 1.12f : 1f;
        Rgba arm = l.BareArms ? l.Skin : l.Body, armShade = l.BareArms ? l.SkinShade : l.BodyShade;

        // arms reach forward from the shoulders; the torso covers the shoulder joints
        foreach (int s in Sides)
        {
            (float, float) shoulder = (0.4f, 8.2f * s), elbow = (6.3f, 8.7f * s), hand = (10.3f, 4.3f * s);
            Limb(c, f, shoulder, elbow, 2.6f * limb, 2.35f * limb, armShade, arm);
            Limb(c, f, elbow, hand, 2.35f * limb, 2.0f * limb, armShade, arm);
        }

        // shoulders / torso: an oval elongated across the facing direction
        c.Fill(Shape.Ellipse(f.X(-1f), cy, f.R(6.4f), f.R(10f) * l.Bulk), l.BareArms ? l.SkinShade : l.BodyShade);
        if (l.BareArms)
        {
            c.Fill(Shape.Ellipse(f.X(-1.2f), cy - 0.4f, f.R(5.5f), f.R(9.2f) * l.Bulk), l.Skin);
            c.Fill(Shape.Ellipse(f.X(-1.5f), cy, f.R(5.5f), f.R(7.2f) * l.Bulk), l.BodyShade);
            c.Fill(Shape.Ellipse(f.X(-1.7f), cy - 0.4f, f.R(4.8f), f.R(6.5f) * l.Bulk), l.Body);
        }
        else
        {
            c.Fill(Shape.Ellipse(f.X(-1.3f), cy - 0.45f, f.R(5.6f), f.R(9.1f) * l.Bulk), l.Body);
        }
        c.Fill(Shape.Ellipse(f.X(-2.4f), f.Y(-3.6f), f.R(2.4f), f.R(3.8f) * l.Bulk), l.BodyLight.Fade(0.5f));

        foreach (int s in Sides) Hand(c, l, f.X(10.3f), f.Y(4.3f * s), f.R(2.1f) * (l.BareArms ? 1.1f : 1f));

        if (l.Collar is { } collar)
            c.Fill(Shape.Ellipse(f.X(1.3f), cy, f.R(4.4f), f.R(6.3f)), collar);

        HeadFromAbove(c, l, f.X(2.3f), cy, HeadRadius(l));
    }

    /// <summary>Head seen from above, face towards +X: hair covers the back, a crescent of face and a nose show at the front.</summary>
    static void HeadFromAbove(Canvas c, HumanLook l, float hx, float hy, float r)
    {
        foreach (int s in Sides) c.Fill(Shape.Circle(hx - 0.4f, hy + s * r * 0.93f, r * 0.25f), l.SkinShade); // ears
        c.Fill(Shape.Ellipse(hx + r - 0.1f, hy, 1.0f, 0.85f), l.SkinShade);                                  // nose
        c.Fill(Shape.Circle(hx, hy, r), l.SkinShade);
        c.Fill(Shape.Circle(hx - 0.25f, hy - 0.35f, r - 0.6f), l.Skin);
        if (l.Hair is { } hair)
        {
            c.Fill(Shape.Circle(hx - r * 0.26f, hy, r * 0.92f).And(Shape.Circle(hx, hy, r + 0.05f)), hair);
            c.Fill(Shape.Ellipse(hx - r * 0.45f, hy - r * 0.35f, r * 0.35f, r * 0.22f, -0.5f), hair.Tone(1.35f).Fade(0.45f));
        }
        else
        {
            c.Fill(Shape.Ellipse(hx - r * 0.2f, hy - r * 0.3f, r * 0.38f, r * 0.26f, -0.4f), Rgba.White.Fade(0.35f));
        }
    }

    // ------------------------------------------------------------------ lying human (knocked out / dead)

    /// <summary>Parts of a body lying on its back that can be drawn separately (severed, torn off).</summary>
    public enum HumanPart { Leg1, Leg2, Arm1, Arm2, Torso, Head }

    /// <summary>Human lying on its back, body along X with the head towards +X.</summary>
    public static void HumanDown(Canvas c, HumanLook l, float cx, float cy)
    {
        foreach (HumanPart part in Enum.GetValues<HumanPart>()) HumanDownPart(c, l, cx, cy, part);
    }

    /// <summary>One part of <see cref="HumanDown"/>, at the same place as in the whole body.</summary>
    public static void HumanDownPart(Canvas c, HumanLook l, float cx, float cy, HumanPart part)
    {
        var f = new Frame(cx, cy, l.Scale, l.Bulk);
        float limb = l.BareArms ? 1.2f : 1f;
        Rgba pantsShade = l.Pants.Shade(0.72f);
        Rgba arm = l.BareArms ? l.Skin : l.Body, armShade = l.BareArms ? l.SkinShade : l.BodyShade;
        float hr = f.R(2.05f) * (l.BareArms ? 1.15f : 1f);

        void Shoe(float x, float y)
        {
            c.Fill(Shape.Ellipse(f.X(x), f.Y(y), f.R(1.9f), f.R(2.3f)), l.Shoes);
            c.Fill(Shape.Ellipse(f.X(x) - 0.3f, f.Y(y) - 0.6f, f.R(0.9f), f.R(1.0f)), l.Shoes.Tone(1.25f));
        }

        switch (part)
        {
            case HumanPart.Leg1: // straight
                Limb(c, f, (-4f, -3.1f), (-10.8f, -4.7f), 2.9f, 2.6f, pantsShade, l.Pants);
                Limb(c, f, (-10.8f, -4.7f), (-16.6f, -6.1f), 2.6f, 2.3f, pantsShade, l.Pants);
                Shoe(-17.8f, -6.4f);
                break;
            case HumanPart.Leg2: // bent outwards
                Limb(c, f, (-4f, 3.1f), (-9.6f, 7.9f), 2.9f, 2.6f, pantsShade, l.Pants);
                Limb(c, f, (-9.6f, 7.9f), (-16.2f, 6.7f), 2.6f, 2.3f, pantsShade, l.Pants);
                Shoe(-17.4f, 7.0f);
                break;
            case HumanPart.Arm1: // flung out above the head
                Limb(c, f, (6.8f, -8.2f), (2.4f, -13.6f), 2.5f * limb, 2.3f * limb, armShade, arm);
                Limb(c, f, (2.4f, -13.6f), (6.9f, -17.3f), 2.3f * limb, 2.0f * limb, armShade, arm);
                Hand(c, l, f.X(7.5f), f.Y(-17.9f), hr);
                break;
            case HumanPart.Arm2: // lying along the side
                Limb(c, f, (6.8f, 8.2f), (0.6f, 11.6f), 2.5f * limb, 2.3f * limb, armShade, arm);
                Limb(c, f, (0.6f, 11.6f), (-5.6f, 12.4f), 2.3f * limb, 2.0f * limb, armShade, arm);
                Hand(c, l, f.X(-6.3f), f.Y(12.6f), hr);
                break;
            case HumanPart.Torso:
                // pelvis
                c.Fill(Shape.RoundRect(f.X(-4.3f), cy, f.R(7.5f), f.R(12.4f) * l.Bulk, f.R(3.5f)), pantsShade);
                c.Fill(Shape.RoundRect(f.X(-4.5f), cy - 0.4f, f.R(6.3f), f.R(11.2f) * l.Bulk, f.R(3f)), l.Pants);
                // chest
                if (l.BareArms)
                {
                    c.Fill(Shape.RoundRect(f.X(2f), cy, f.R(14.5f), f.R(18.6f) * l.Bulk, f.R(6.5f)), l.SkinShade);
                    c.Fill(Shape.RoundRect(f.X(1.8f), cy - 0.45f, f.R(13.1f), f.R(17.2f) * l.Bulk, f.R(5.8f)), l.Skin);
                    c.Fill(Shape.RoundRect(f.X(1.2f), cy, f.R(13f), f.R(13.4f) * l.Bulk, f.R(5f)), l.BodyShade);
                    c.Fill(Shape.RoundRect(f.X(1.0f), cy - 0.45f, f.R(11.8f), f.R(12.2f) * l.Bulk, f.R(4.5f)), l.Body);
                }
                else
                {
                    c.Fill(Shape.RoundRect(f.X(2f), cy, f.R(14.5f), f.R(18.6f) * l.Bulk, f.R(6.5f)), l.BodyShade);
                    c.Fill(Shape.RoundRect(f.X(1.8f), cy - 0.45f, f.R(13.1f), f.R(17.2f) * l.Bulk, f.R(5.8f)), l.Body);
                }
                c.Fill(Shape.Ellipse(f.X(3.2f), f.Y(-4.2f), f.R(4f), f.R(3f) * l.Bulk), l.BodyLight.Fade(0.45f));
                if (l.Collar is { } collar)
                    c.Fill(Shape.Ellipse(f.X(8.4f), cy + f.R(0.4f), f.R(3f), f.R(5.6f)), collar);
                break;
            case HumanPart.Head:
                HeadFaceUp(c, l, f.X(13.2f), cy + f.R(0.7f), HeadRadius(l));
                break;
        }
    }

    /// <summary>Head lying face-up with the top of the head towards +X.</summary>
    static void HeadFaceUp(Canvas c, HumanLook l, float hx, float hy, float r)
    {
        foreach (int s in Sides) c.Fill(Shape.Circle(hx - 0.3f, hy + s * r * 0.95f, r * 0.24f), l.SkinShade);
        c.Fill(Shape.Circle(hx, hy, r), l.SkinShade);
        c.Fill(Shape.Circle(hx - 0.25f, hy - 0.35f, r - 0.6f), l.Skin);
        if (l.Hair is { } hair)
            c.Fill(Shape.Circle(hx + r * 0.62f, hy, r * 0.95f).And(Shape.Circle(hx, hy, r + 0.05f)), hair);
        else
            c.Fill(Shape.Ellipse(hx + r * 0.35f, hy - r * 0.3f, r * 0.35f, r * 0.25f), Rgba.White.Fade(0.3f));
        // closed eyes and nose, very faint
        var mark = l.SkinShade.Shade(0.7f).Fade(0.75f);
        foreach (int s in Sides) c.Fill(Shape.Capsule(hx - 0.2f, hy + s * r * 0.28f, hx - 0.2f, hy + s * r * 0.52f, 0.35f), mark);
        c.Fill(Shape.Circle(hx - r * 0.42f, hy, 0.55f), mark);
    }

    // ------------------------------------------------------------------ wounded, crawling face down

    /// <summary>
    /// Wounded human crawling face down, head towards +X, at the same place as <see cref="HumanDown"/>.
    /// Four frames: one arm reaches ahead while the other pulls, then the other way round; a leg pushes.
    /// </summary>
    public static void HumanCrawl(Canvas c, HumanLook l, float cx, float cy, int frame)
    {
        var f = new Frame(cx, cy, l.Scale, l.Bulk);
        float limb = l.BareArms ? 1.2f : 1f;
        Rgba pantsShade = l.Pants.Shade(0.72f);
        Rgba arm = l.BareArms ? l.Skin : l.Body, armShade = l.BareArms ? l.SkinShade : l.BodyShade;

        // legs dragged behind, one knee bent outwards to push
        foreach (int s in Sides)
        {
            bool pushing = (frame == 0 && s < 0) || (frame == 2 && s > 0);
            (float, float) hip = (-4f, 3.2f * s), knee = pushing ? (-9.5f, 8.6f * s) : (-10.8f, 4.2f * s), foot = pushing ? (-15.8f, 7.4f * s) : (-17f, 4.6f * s);
            Limb(c, f, hip, knee, 2.9f, 2.6f, pantsShade, l.Pants);
            Limb(c, f, knee, foot, 2.6f, 2.3f, pantsShade, l.Pants);
            c.Fill(Shape.Ellipse(f.X(foot.Item1 - 1.4f), f.Y(foot.Item2), f.R(2.1f), f.R(1.8f)), l.Shoes);
        }

        // pelvis
        c.Fill(Shape.RoundRect(f.X(-4.3f), cy, f.R(7.5f), f.R(12.4f) * l.Bulk, f.R(3.5f)), pantsShade);
        c.Fill(Shape.RoundRect(f.X(-4.5f), cy - 0.4f, f.R(6.3f), f.R(11.2f) * l.Bulk, f.R(3f)), l.Pants);

        // arms: reach (far ahead of the head), pull (elbow out, hand beside the head) or in between
        var hands = new List<(float X, float Y)>();
        foreach (int s in Sides)
        {
            int pose = frame switch
            {
                0 => s < 0 ? 2 : 0,
                2 => s < 0 ? 0 : 2,
                _ => 1,
            };
            (float, float) shoulder = (7f, 7.6f * s);
            (float X, float Y) elbow = pose switch { 2 => (12.5f, 9.6f * s), 1 => (9.5f, 12.4f * s), _ => (5.2f, 13.2f * s) };
            (float X, float Y) hand = pose switch { 2 => (18.4f, 6.2f * s), 1 => (15.2f, 10.2f * s), _ => (11.4f, 11.6f * s) };
            Limb(c, f, shoulder, elbow, 2.5f * limb, 2.3f * limb, armShade, arm);
            Limb(c, f, elbow, hand, 2.3f * limb, 2.0f * limb, armShade, arm);
            hands.Add(hand);
        }

        // back of the torso, a crease along the spine
        c.Fill(Shape.RoundRect(f.X(2f), cy, f.R(14.5f), f.R(18.6f) * l.Bulk, f.R(6.5f)), l.BareArms ? l.SkinShade : l.BodyShade);
        if (l.BareArms)
        {
            c.Fill(Shape.RoundRect(f.X(1.8f), cy - 0.45f, f.R(13.1f), f.R(17.2f) * l.Bulk, f.R(5.8f)), l.Skin);
            c.Fill(Shape.RoundRect(f.X(1.2f), cy, f.R(13f), f.R(13.4f) * l.Bulk, f.R(5f)), l.BodyShade);
            c.Fill(Shape.RoundRect(f.X(1.0f), cy - 0.45f, f.R(11.8f), f.R(12.2f) * l.Bulk, f.R(4.5f)), l.Body);
        }
        else
        {
            c.Fill(Shape.RoundRect(f.X(1.8f), cy - 0.45f, f.R(13.1f), f.R(17.2f) * l.Bulk, f.R(5.8f)), l.Body);
        }
        c.Fill(Shape.Capsule(f.X(-3.5f), cy, f.X(7f), cy, f.R(0.55f)), l.BodyShade.Fade(0.8f));
        c.Fill(Shape.Ellipse(f.X(3f), f.Y(-4.4f), f.R(3.6f), f.R(2.6f) * l.Bulk), l.BodyLight.Fade(0.35f));

        // back of the head: hair (or a bald scalp) and ears
        float hx = f.X(13.2f), r = HeadRadius(l);
        foreach (int s in Sides) c.Fill(Shape.Circle(hx - 0.3f, cy + s * r * 0.95f, r * 0.24f), l.SkinShade);
        c.Fill(Shape.Circle(hx, cy, r), l.SkinShade);
        c.Fill(Shape.Circle(hx - 0.25f, cy - 0.35f, r - 0.6f), l.Skin);
        if (l.Hair is { } hair)
        {
            c.Fill(Shape.Circle(hx - 0.2f, cy, r * 0.97f), hair);
            c.Fill(Shape.Ellipse(hx - r * 0.3f, cy - r * 0.35f, r * 0.4f, r * 0.24f, -0.4f), hair.Tone(1.35f).Fade(0.45f));
        }
        else
        {
            c.Fill(Shape.Ellipse(hx - r * 0.25f, cy - r * 0.3f, r * 0.38f, r * 0.26f, -0.4f), Rgba.White.Fade(0.3f));
        }

        float hr = f.R(2.05f) * (l.BareArms ? 1.15f : 1f);
        foreach (var (x, y) in hands) Hand(c, l, f.X(x), f.Y(y), hr);
    }

    // ------------------------------------------------------------------ kneeling astride (executions)

    /// <summary>
    /// Kneeling astride a body lying along X, leaning towards +X: knees on both sides of the body, shins
    /// folded back flat on the floor, both hands forward around the victim's head.
    /// </summary>
    public static void HumanMount(Canvas c, HumanLook l, float cx, float cy)
    {
        var f = new Frame(cx, cy, l.Scale, l.Bulk);
        float limb = l.BareArms ? 1.12f : 1f;
        Rgba pantsShade = l.Pants.Shade(0.72f);
        Rgba arm = l.BareArms ? l.Skin : l.Body, armShade = l.BareArms ? l.SkinShade : l.BodyShade;
        Rgba top = l.BareArms ? l.Skin : l.Body, topShade = l.BareArms ? l.SkinShade : l.BodyShade;

        foreach (int s in Sides)
        {
            // knees spread on both sides of the body, shins folded back under the thighs
            Limb(c, f, (-1f, 11.6f * s), (-9f, 10.4f * s), 2.5f, 2.2f, pantsShade, l.Pants);
            c.Fill(Shape.Ellipse(f.X(-10.2f), f.Y(10f * s), f.R(2.2f), f.R(1.8f)), l.Shoes);
            Limb(c, f, (-5.4f, 4.6f * s), (-1f, 11.6f * s), 3.3f, 3f, pantsShade, l.Pants);
        }

        // hips, then the back leaning forward
        c.Fill(Shape.Ellipse(f.X(-5.4f), cy, f.R(3.8f), f.R(6.4f) * l.Bulk), pantsShade);
        c.Fill(Shape.Ellipse(f.X(-5.6f), cy - 0.4f, f.R(3.1f), f.R(5.6f) * l.Bulk), l.Pants);
        c.Fill(Shape.Ellipse(f.X(-1.6f), cy, f.R(5.4f), f.R(7.4f) * l.Bulk), topShade);
        c.Fill(Shape.Ellipse(f.X(-1.8f), cy - 0.4f, f.R(4.6f), f.R(6.6f) * l.Bulk), top);

        // arms from the ends of the shoulders, reaching down and forward to the head
        foreach (int s in Sides)
        {
            (float, float) shoulder = (2.2f, 9.8f * s), elbow = (7.2f, 9.6f * s), hand = (11f, 4.4f * s);
            Limb(c, f, shoulder, elbow, 2.6f * limb, 2.35f * limb, armShade, arm);
            Limb(c, f, elbow, hand, 2.35f * limb, 2.0f * limb, armShade, arm);
        }

        // shoulders: a wide bar across the body
        c.Fill(Shape.Ellipse(f.X(2f), cy, f.R(4.4f), f.R(11.6f) * l.Bulk), topShade);
        c.Fill(Shape.Ellipse(f.X(1.8f), cy - 0.45f, f.R(3.7f), f.R(10.8f) * l.Bulk), top);
        if (l.BareArms)
        {
            c.Fill(Shape.Ellipse(f.X(1.2f), cy, f.R(3.4f), f.R(7f) * l.Bulk), l.BodyShade);
            c.Fill(Shape.Ellipse(f.X(1f), cy - 0.4f, f.R(2.8f), f.R(6.3f) * l.Bulk), l.Body);
        }
        c.Fill(Shape.Ellipse(f.X(0.6f), f.Y(-5.4f), f.R(1.8f), f.R(3.6f) * l.Bulk), l.BodyLight.Fade(0.5f));
        if (l.Collar is { } collar)
            c.Fill(Shape.Ellipse(f.X(3.4f), cy, f.R(2.6f), f.R(5.2f)), collar);

        foreach (int s in Sides) Hand(c, l, f.X(11f), f.Y(4.4f * s), f.R(2.1f) * (l.BareArms ? 1.1f : 1f));
        HeadFromAbove(c, l, f.X(5.4f), cy, HeadRadius(l) * 0.92f);
    }

    // ------------------------------------------------------------------ dog

    public static void Dog(Canvas c, DogLook d, float cx, float cy)
    {
        c.Fill(Shape.Capsule(cx - 9.5f, cy + 0.3f, cx - 14f, cy + 1.8f, 1.4f, 0.7f), d.FurDark); // tail
        foreach (float x in new[] { 5.2f, -6.4f })
            foreach (int s in Sides)
                c.Fill(Shape.Ellipse(cx + x, cy + 4.8f * s, 1.9f, 1.35f), d.FurDark);                // paws
        c.Fill(Shape.Ellipse(cx - 1.5f, cy, 9.2f, 5.0f), d.FurDark);
        c.Fill(Shape.Ellipse(cx - 1.6f, cy - 0.35f, 8.4f, 4.3f), d.Fur);
        c.Fill(Shape.Ellipse(cx - 2.6f, cy - 0.1f, 6.6f, 1.7f), d.FurDark.Fade(0.5f));             // darker back
        c.Fill(Shape.Ellipse(cx + 5f, cy, 3.6f, 3.9f), d.Fur);                                     // chest
        foreach (int s in Sides)
            c.Fill(Shape.Ellipse(cx + 6.4f, cy + 3.4f * s, 2.2f, 1.3f, 0.5f * s), d.FurDark);       // ears
        c.Fill(Shape.Ellipse(cx + 8.4f, cy, 3.8f, 3.5f), d.FurDark);
        c.Fill(Shape.Ellipse(cx + 8.3f, cy - 0.3f, 3.2f, 3.0f), d.Fur);
        c.Fill(Shape.Ellipse(cx + 12f, cy, 2.7f, 2.0f), d.Belly);                                  // snout
        c.Fill(Shape.Ellipse(cx + 14.2f, cy, 0.95f, 1.05f), d.Nose);
        foreach (int s in Sides) c.Fill(Shape.Circle(cx + 10f, cy + 1.75f * s, 0.6f), d.Nose);       // eyes
    }

    /// <summary>Parts of a dead dog that can be drawn separately (torn off by executions).</summary>
    public enum DogPart { Leg1, Leg2, Leg3, Leg4, Torso, Head }

    static readonly (float X0, float Y0, float X1, float Y1)[] DogLegs =
        [(3.4f, 3.4f, 5.0f, 11.0f), (-3.6f, 3.6f, -5.2f, 11.2f), (5.8f, 2.8f, 9.4f, 10.2f), (-5.8f, 3f, -9.8f, 10f)];

    /// <summary>Dog lying on its side, legs sticking out towards +Y, head towards +X.</summary>
    public static void DogDead(Canvas c, DogLook d, float cx, float cy)
    {
        foreach (DogPart part in Enum.GetValues<DogPart>()) DogDeadPart(c, d, cx, cy, part);
    }

    /// <summary>One part of <see cref="DogDead"/>, at the same place as in the whole body.</summary>
    public static void DogDeadPart(Canvas c, DogLook d, float cx, float cy, DogPart part)
    {
        switch (part)
        {
            case DogPart.Torso:
                c.Fill(Shape.Capsule(cx - 9.8f, cy + 0.6f, cx - 15.2f, cy + 3.8f, 1.4f, 0.7f), d.FurDark);
                c.Fill(Shape.Ellipse(cx - 1f, cy, 9.6f, 5.6f), d.FurDark);
                c.Fill(Shape.Ellipse(cx - 1.1f, cy - 0.4f, 8.8f, 4.9f), d.Fur);
                c.Fill(Shape.Ellipse(cx - 1f, cy + 2.8f, 6.5f, 2.0f), d.Belly.Fade(0.8f));
                break;
            case DogPart.Head:
                c.Fill(Shape.Ellipse(cx + 9.8f, cy - 0.6f, 4f, 3.4f, -0.15f), d.FurDark);
                c.Fill(Shape.Ellipse(cx + 9.7f, cy - 0.9f, 3.4f, 2.8f, -0.15f), d.Fur);
                c.Fill(Shape.Ellipse(cx + 13.6f, cy + 0.2f, 2.6f, 1.8f), d.Belly);
                c.Fill(Shape.Ellipse(cx + 15.8f, cy + 0.4f, 0.9f, 1.0f), d.Nose);
                c.Fill(Shape.Ellipse(cx + 8.2f, cy - 3.7f, 2.4f, 1.4f, 0.4f), d.FurDark);                   // ear flop
                c.Fill(Shape.Capsule(cx + 10.4f, cy - 1.6f, cx + 11.6f, cy - 1.3f, 0.35f), d.Nose);          // closed eye
                break;
            default:
                var (x0, y0, x1, y1) = DogLegs[(int)part];
                c.Fill(Shape.Capsule(cx + x0, cy + y0, cx + x1, cy + y1, 1.6f, 1.2f), d.FurDark);
                c.Fill(Shape.Ellipse(cx + x1, cy + y1 + 0.4f, 1.5f, 1.2f), d.FurDark.Shade(0.8f));
                break;
        }
    }

    // ------------------------------------------------------------------ creature

    /// <summary>Standing creature: irregular body, six spindly clawed limbs, a cluster of eyes near the front.
    /// Eyes are drawn on <paramref name="eyes"/> so the caller can make them glow.</summary>
    public static void Creature(Canvas c, Canvas eyes, CreatureLook l, float cx, float cy, int seed)
    {
        var rng = new Random(seed);
        // (root, joint, tip, claw) for the -Y side; mirrored with a little asymmetry for +Y
        var limbs = new (float X, float Y)[][]
        {
            [(6f, -4f), (12.5f, -9f), (18.5f, -6.5f), (22f, -2.6f)],
            [(1f, -9f), (5.5f, -16.5f), (13f, -19f), (17f, -17f)],
            [(-6.5f, -8f), (-11f, -14f), (-9f, -19.6f), (-5.6f, -21.2f)],
        };
        foreach (int s in Sides)
        {
            foreach (var limb in limbs)
            {
                var p = new (float X, float Y)[4];
                for (int i = 0; i < 4; i++)
                {
                    float jx = i == 0 ? 0f : (rng.NextSingle() - 0.5f) * 1.6f, jy = i == 0 ? 0f : (rng.NextSingle() - 0.5f) * 1.6f;
                    var (x, y) = limb[i];
                    float rx = limb[0].X + (x - limb[0].X) * l.Reach, ry = limb[0].Y + (y - limb[0].Y) * l.Reach;
                    p[i] = (cx + rx + jx, cy + (ry + jy) * s);
                }
                SpindlyLimb(c, l, p[0], p[1], p[2], p[3]);
            }
        }

        CreatureBody(c, l, cx, cy, 9.2f, 10.8f, 1f, rng);

        // maw with a few teeth, then the eye cluster
        c.Fill(Shape.Ellipse(cx + 8.3f, cy, 1.5f, 3.2f), l.Maw);
        foreach (float ty in new[] { -2.2f, -0.8f, 0.7f, 2.1f })
            c.Fill(Shape.Circle(cx + 9.1f, cy + ty, 0.42f), l.Claw);
        foreach (var (x, y, r) in new[] { (6.2f, -4.7f, 1.5f), (7.0f, 4.2f, 1.4f), (4.4f, -7.4f, 1.0f), (5.1f, 7.0f, 0.95f) })
            eyes.Fill(Shape.Circle(cx + x, cy + y, r), l.Eye);
    }

    /// <summary>Dead creature: flattened body with limbs splayed and curled in every direction.</summary>
    public static void CreatureDead(Canvas c, CreatureLook l, float cx, float cy, int seed)
    {
        var rng = new Random(seed);
        float[] angles = [-2.7f, -1.85f, -0.95f, -0.1f, 0.8f, 1.7f, 2.55f];
        foreach (float a in angles)
        {
            float bend = (rng.NextSingle() * 0.25f + 0.3f) * (rng.Next(2) == 0 ? -1f : 1f);
            float r1 = (16.5f + rng.NextSingle() * 3f) * l.Reach, r2 = (23.5f + rng.NextSingle() * 3f) * l.Reach;
            (float, float) P(float ang, float rad) => (cx + MathF.Cos(ang) * rad, cy + MathF.Sin(ang) * rad);
            var root = P(a, 7.5f);
            var joint = P(a + bend * 0.6f, r1);
            var tip = P(a + bend * 1.5f, r2);
            var claw = P(a + bend * 2.7f, r2 - 3f);
            SpindlyLimb(c, l, root, joint, tip, claw);
        }
        CreatureBody(c, l, cx, cy, 10.5f, 9.2f, 1.35f, rng);
        c.Fill(Shape.Ellipse(cx + 8f, cy + 0.5f, 1.3f, 2.6f, 0.3f), l.Maw);
        foreach (var (x, y, r) in new[] { (5.8f, -4.2f, 1.1f), (6.6f, 4.4f, 1.0f), (4.2f, -6.6f, 0.75f) })
            c.Fill(Shape.Circle(cx + x, cy + y, r), l.Eye);
    }

    static void SpindlyLimb(Canvas c, CreatureLook l, (float X, float Y) a, (float X, float Y) j, (float X, float Y) t, (float X, float Y) claw)
    {
        float k = l.Thickness;
        c.Fill(Shape.Capsule(a.X, a.Y, j.X, j.Y, 1.9f * k, 1.25f * k), l.Limb);
        c.Fill(Shape.Capsule(j.X, j.Y, t.X, t.Y, 1.25f * k, 0.85f * k), l.Limb);
        c.Fill(Shape.Circle(j.X, j.Y, 1.45f * k), l.Limb);
        c.Fill(Shape.Capsule(a.X, a.Y - 0.45f, j.X, j.Y - 0.45f, 0.6f * k, 0.4f * k), l.LimbLight.Fade(0.8f));
        c.Fill(Shape.Capsule(j.X, j.Y - 0.35f, t.X, t.Y - 0.3f, 0.4f * k, 0.3f * k), l.LimbLight.Fade(0.6f));
        c.Fill(Shape.Capsule(t.X, t.Y, claw.X, claw.Y, 0.95f * k, 0.05f), l.Claw);
    }

    static void CreatureBody(Canvas c, CreatureLook l, float cx, float cy, float rx, float ry, float wobble, Random rng)
    {
        float p1 = rng.NextSingle() * 6.283f, p2 = rng.NextSingle() * 6.283f, p3 = rng.NextSingle() * 6.283f;
        Shape Body(float scale, float ox, float oy) => Shape.Blob(cx + ox, cy + oy, (MathF.Max(rx, ry) + 3f * wobble) * scale,
            a => scale * (Shape.EllipseRadius(rx, ry, a)
                          + wobble * (1.1f * MathF.Sin(3 * a + p1) + 0.7f * MathF.Sin(5 * a + p2) + 0.45f * MathF.Sin(8 * a + p3))));

        int nseed = rng.Next();
        c.Fill(Body(1f, 0f, 0f), l.Rim);
        c.Fill(Body(0.9f, 0f, 0f), l.BodyDark);
        c.Fill(Body(0.74f, -0.6f, -0.5f), (x, y) => l.BodyDark.Lerp(l.Body, Noise.Tile(x / 64f, y / 64f, 10, 10, 2, nseed)));
        // ridge of plates along the back
        for (int i = 0; i < 4; i++)
            c.Fill(Shape.Ellipse(cx - 6.5f + i * 3.1f, cy + (i % 2 == 0 ? -0.7f : 0.7f), 1.6f, 1.05f), l.LimbLight.Fade(0.45f));
    }

    // ------------------------------------------------------------------ legs

    /// <summary>32x32 legs kicking forward: the -Y foot planted, the +Y leg stretched out towards +X.</summary>
    public static void LegsKick(Canvas c, Rgba pants, Rgba shoes)
    {
        float cx = 16f, cy = 16f;
        c.Fill(Shape.Capsule(cx - 0.5f, cy - 3.6f, cx - 3.2f, cy - 4.6f, 2.5f, 2.3f), pants);
        c.Fill(Shape.Ellipse(cx - 2.2f, cy - 4.6f, 3.4f, 2.1f), shoes);
        c.Fill(Shape.Capsule(cx - 0.5f, cy + 3.6f, cx + 10.5f, cy + 2.2f, 2.7f, 2.2f), pants);
        c.Fill(Shape.Ellipse(cx + 12.2f, cy + 2.1f, 3.4f, 2.2f), shoes);
        c.Fill(Shape.Ellipse(cx + 13.2f, cy + 1.6f, 1.5f, 0.8f), shoes.Tone(1.35f));
    }

    /// <summary>One 32x32 walk frame of feet seen from above, drawn at horizontal offset frame * 32.</summary>
    public static void LegsFrame(Canvas c, int frame, Rgba pants, Rgba shoes)
    {
        float cx = 16f + 32f * frame, cy = 16f;
        float[] left = [1.5f, 6.5f, -1.5f, -5.5f];   // foot on the -Y side
        float[] right = [-1.5f, -5.5f, 1.5f, 6.5f];  // foot on the +Y side
        foreach (var (dx, s) in new[] { (left[frame], -1), (right[frame], 1) })
        {
            float fx = cx + dx, fy = cy + 4.4f * s;
            c.Fill(Shape.Capsule(cx - 0.5f, cy + 3.6f * s, fx - 1.2f, fy, 2.5f, 2.3f), pants);
            c.Fill(Shape.Ellipse(fx + 0.6f, fy, 3.4f, 2.1f), shoes);
            c.Fill(Shape.Ellipse(fx + 1.6f, fy - 0.5f, 1.5f, 0.8f), shoes.Tone(1.35f));
        }
    }
}
