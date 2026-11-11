namespace PlaceholderArt;

/// <summary>Base pack sprites: vibrant, saturated 80s-neon-ish colours with a dark ink outline.</summary>
public static class BaseArt
{
    static readonly Rgba Ink = Rgba.Hex("#140c1c").Fade(0.9f);

    public static readonly BloodColors Blood = new(Rgba.Hex("#5a0710"), Rgba.Hex("#870f1c"), Rgba.Hex("#d0404e"));

    static readonly HumanLook Player = new()
    {
        Body = Rgba.Hex("#6a7c36"), BodyShade = Rgba.Hex("#45531f"), BodyLight = Rgba.Hex("#a6b860"),
        Skin = Rgba.Hex("#e8b48a"), SkinShade = Rgba.Hex("#b57f5a"), Hair = Rgba.Hex("#2a1d18"),
        Pants = Rgba.Hex("#2d3a5c"), Shoes = Rgba.Hex("#1c1a1e"), Collar = Rgba.Hex("#33281c"),
    };

    static readonly HumanLook Guard = new()
    {
        Body = Rgba.Hex("#eeeef4"), BodyShade = Rgba.Hex("#b4b8c8"), BodyLight = Rgba.Hex("#ffffff"),
        Skin = Rgba.Hex("#d9a27a"), SkinShade = Rgba.Hex("#a8714f"), Hair = Rgba.Hex("#111114"),
        Pants = Rgba.Hex("#dedee6"), Shoes = Rgba.Hex("#141416"),
    };

    static readonly HumanLook Thug = new()
    {
        Body = Rgba.Hex("#3d414b"), BodyShade = Rgba.Hex("#262930"), BodyLight = Rgba.Hex("#68707e"),
        Skin = Rgba.Hex("#c98d68"), SkinShade = Rgba.Hex("#94603f"), Hair = null,
        Pants = Rgba.Hex("#30333b"), Shoes = Rgba.Hex("#121214"),
    };

    static readonly HumanLook Brute = new()
    {
        Body = Rgba.Hex("#931e2e"), BodyShade = Rgba.Hex("#5e101b"), BodyLight = Rgba.Hex("#d04a5a"),
        Skin = Rgba.Hex("#d49a70"), SkinShade = Rgba.Hex("#9c6544"), Hair = Rgba.Hex("#3a2a22").Fade(0.3f),
        Pants = Rgba.Hex("#3e3a2c"), Shoes = Rgba.Hex("#1a1410"), BareArms = true, Scale = 1.35f, Bulk = 1.08f,
    };

    static readonly DogLook Dog = new(Rgba.Hex("#8a5530"), Rgba.Hex("#5a341a"), Rgba.Hex("#c49a6c"), Rgba.Hex("#161010"));

    static readonly CreatureLook Creature = new()
    {
        Body = Rgba.Hex("#4c2672"), BodyDark = Rgba.Hex("#2a1240"), Rim = Rgba.Hex("#9a5ad0"),
        Limb = Rgba.Hex("#34184e"), LimbLight = Rgba.Hex("#8250b4"), Claw = Rgba.Hex("#e8dccc"),
        Eye = Rgba.Hex("#f4fff2"), Maw = Rgba.Hex("#0c0412"),
    };

    public static void Generate(Pack pack)
    {
        Humans(pack, "player", Player, 32, 48, 101);
        Humans(pack, "guard", Guard, 32, 48, 102);
        Humans(pack, "thug", Thug, 32, 48, 103);
        Humans(pack, "brute", Brute, 40, 56, 104);
        Dogs(pack);
        Creatures(pack);
        Legs(pack);

        foreach (var (name, sprite) in Weapons.All())
        {
            sprite.Outline(Ink);
            pack.Png($"sprites/weapons/{name}.png", sprite);
        }

        Fx(pack);

        pack.Png("sprites/tiles/floor_wood.png", Tiles.FloorWood());
        pack.Png("sprites/tiles/floor_tile.png", Tiles.FloorTile());
        pack.Png("sprites/tiles/floor_concrete.png", Tiles.FloorConcrete());
        pack.Png("sprites/tiles/floor_carpet.png", Tiles.FloorCarpet());
        pack.Png("sprites/tiles/wall_brick.png", Tiles.WallBrick());
        pack.Png("sprites/tiles/wall_plaster.png", Tiles.WallPlaster());
        pack.Png("sprites/tiles/glass.png", Tiles.Glass());
        pack.Png("sprites/tiles/glass_broken.png", Tiles.GlassBroken());
        pack.Png("sprites/tiles/door_wood.png", Tiles.DoorWood());
        pack.Png("sprites/tiles/door_metal.png", Tiles.DoorMetal());

        pack.Png("sprites/props/crate.png", Props.Crate());
        pack.Png("sprites/props/table.png", Props.Table());
        pack.Png("sprites/props/plant.png", Props.Plant());
        pack.Png("sprites/props/car.png", Props.Car());

        pack.Png("sprites/ui/crosshair.png", Crosshair());
        pack.Png("sprites/ui/arrow.png", Arrow());
    }

    static void Humans(Pack pack, string name, HumanLook look, int size, int downSize, int seed)
    {
        var stand = new Canvas(size, size);
        Actors.HumanStanding(stand, look, size / 2f, size / 2f);
        stand.Outline(Ink);
        pack.Png($"sprites/actors/{name}.png", stand);

        var down = new Canvas(downSize, downSize);
        Actors.HumanDown(down, look, downSize / 2f, downSize / 2f);
        down.Outline(Ink);
        pack.Png($"sprites/actors/{name}_down.png", down);

        var dead = new Canvas(downSize, downSize);
        float k = downSize / 48f;
        Effects.BloodPool(dead, downSize / 2f + 6f * k, downSize / 2f + 1.5f * k, 15f * k, Blood, seed);
        dead.Draw(down);
        pack.Png($"sprites/actors/{name}_dead.png", dead);

        int mountSize = size + 8;
        var mount = new Canvas(mountSize, mountSize);
        Actors.HumanMount(mount, look, mountSize / 2f, mountSize / 2f);
        mount.Outline(Ink);
        pack.Png($"sprites/actors/{name}_mount.png", mount);

        var crawl = new Canvas(downSize * 4, downSize);
        for (int frame = 0; frame < 4; frame++) Actors.HumanCrawl(crawl, look, downSize * (frame + 0.5f), downSize / 2f, frame);
        crawl.Outline(Ink);
        pack.Png($"sprites/actors/{name}_crawl.png", crawl);
    }

    static void Dogs(Pack pack)
    {
        var dog = new Canvas(32, 32);
        Actors.Dog(dog, Dog, 16f, 16f);
        dog.Outline(Ink);
        pack.Png("sprites/actors/dog.png", dog);

        var body = new Canvas(40, 40);
        Actors.DogDead(body, Dog, 19f, 18f);
        body.Outline(Ink);
        var dead = new Canvas(40, 40);
        Effects.BloodPool(dead, 24f, 20f, 11.5f, Blood, 105);
        dead.Draw(body);
        pack.Png("sprites/actors/dog_dead.png", dead);

        // The same corpse in pieces, each at its place on a canvas of the same size.
        foreach (Actors.DogPart part in Enum.GetValues<Actors.DogPart>())
        {
            var piece = new Canvas(40, 40);
            Actors.DogDeadPart(piece, Dog, 19f, 18f, part);
            piece.Outline(Ink);
            pack.Png($"sprites/actors/dog_{part.ToString().ToLowerInvariant()}.png", piece);
        }
    }

    static void Creatures(Pack pack)
    {
        var body = new Canvas(48, 48);
        var eyes = new Canvas(48, 48);
        Actors.Creature(body, eyes, Creature, 21f, 24f, 106);
        body.Outline(Ink);
        eyes.Glow(Rgba.Hex("#9dffc8"), 1.3f, 2.2f);
        body.Draw(eyes);
        pack.Png("sprites/actors/creature.png", body);

        var corpse = new Canvas(64, 64);
        Actors.CreatureDead(corpse, Creature with { Eye = Rgba.Hex("#6a6478") }, 32f, 32f, 107);
        corpse.Outline(Ink);
        var dead = new Canvas(64, 64);
        Effects.BloodPool(dead, 33f, 33f, 17f, new BloodColors(Rgba.Hex("#1c0a26"), Rgba.Hex("#2e1440"), Rgba.Hex("#7a4aa0")), 108);
        dead.Draw(corpse);
        pack.Png("sprites/actors/creature_dead.png", dead);
    }

    static void Legs(Pack pack)
    {
        var strip = new Canvas(128, 32);
        for (int f = 0; f < 4; f++) Actors.LegsFrame(strip, f, Rgba.Hex("#2b2f3c"), Rgba.Hex("#141418"));
        strip.Outline(Ink);
        pack.Png("sprites/actors/legs.png", strip);

        var kick = new Canvas(32, 32);
        Actors.LegsKick(kick, Rgba.Hex("#2b2f3c"), Rgba.Hex("#141418"));
        kick.Outline(Ink);
        pack.Png("sprites/actors/legs_kick.png", kick);
    }

    static void Fx(Pack pack)
    {
        pack.Png("sprites/fx/bullet.png", Effects.Bullet(Rgba.Hex("#ff9a30"), Rgba.Hex("#fffbe6"), Rgba.Hex("#ffd060")));
        pack.Png("sprites/fx/muzzle_flash.png", Effects.MuzzleFlash(Rgba.Hex("#ff8418"), Rgba.Hex("#ffd84a"), Rgba.Hex("#fffbe8")));
        for (int i = 1; i <= 3; i++) pack.Png($"sprites/fx/blood_{i}.png", Effects.Splat(i, Blood, 110 + i));
        pack.Png("sprites/fx/blood_drop.png", Effects.Drop(Blood));
        pack.Png("sprites/fx/gore.png", Effects.Gore(Blood, Rgba.Hex("#d9707a"), Rgba.Hex("#efe2d0"), 120));
        pack.Png("sprites/fx/gib.png", Effects.Gib(Blood, Rgba.Hex("#c4505c"), 121));
        pack.Png("sprites/fx/spark.png", Effects.Spark(Rgba.Hex("#ffffff"), Rgba.Hex("#ffd84a")));
        pack.Png("sprites/fx/shard.png", Effects.Shard(new Rgba(0.68f, 0.9f, 1f, 0.85f), new Rgba(1f, 1f, 1f, 0.95f)));
        pack.Png("sprites/fx/smoke.png", Effects.Smoke(Rgba.Hex("#c4c4cc")));
    }

    /// <summary>White cross with a centre gap and a dark outline, readable on any background.</summary>
    static Canvas Crosshair()
    {
        var c = new Canvas(16, 16);
        foreach (var r in new[] { Shape.Rect(7f, 1f, 2f, 4.5f), Shape.Rect(7f, 10.5f, 2f, 4.5f), Shape.Rect(1f, 7f, 4.5f, 2f), Shape.Rect(10.5f, 7f, 4.5f, 2f) })
            c.Fill(r, Rgba.White);
        c.Outline(Rgba.Hex("#101018"));
        return c;
    }

    static Canvas Arrow()
    {
        var c = new Canvas(16, 16);
        c.Fill(Shape.Rect(2f, 6.5f, 7.5f, 3f).Or(Shape.Polygon((8.5f, 2.5f), (14.5f, 8f), (8.5f, 13.5f))), Rgba.White);
        c.Outline(Rgba.Hex("#101018"));
        return c;
    }
}
