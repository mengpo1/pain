namespace PlaceholderArt;

/// <summary>Pack B, "Mistvale": a misty medieval valley, village militia against raiders and revenants.</summary>
public static class MistvaleArt
{
    public static void Generate(Pack p)
    {
        Rgba stone = Rgba.Hex("#8a8a82"), mortar = Rgba.Hex("#5a5a54"), timber = Rgba.Hex("#6a4a2a"), daub = Rgba.Hex("#d8c8a0");
        Material stoneWall = IsoDraw.Bricks(stone, mortar, 5f, 4f, 41);
        Material timberWall = TimberFrame(timber, daub);
        Material wood = IsoDraw.Planks(Rgba.Hex("#7a5030"), 3f, 42);
        Material dark = IsoDraw.Solid(Rgba.Hex("#4a3420"), 0.08f);
        Material shutter = IsoDraw.Planks(Rgba.Hex("#5a6a3a"), 2.5f, 43);
        Material palisade = IsoDraw.Planks(Rgba.Hex("#7a6040"), 2f, 44);

        p.Png("tiles/floor_grass.png", TileArt.Floor(IsoDraw.Organic(Rgba.Hex("#56703e"), Rgba.Hex("#6c8048"), 1.4f, 51)));
        p.Png("tiles/floor_mud.png", TileArt.Floor(IsoDraw.Organic(Rgba.Hex("#5a4630"), Rgba.Hex("#6e5a3e"), 1.6f, 52, 0.14f)));
        p.Png("tiles/floor_cobble.png", TileArt.Floor(IsoDraw.Bricks(Rgba.Hex("#7c7a74"), Rgba.Hex("#4c4a44"), 3f, 3f, 53)));
        p.Png("tiles/floor_planks.png", TileArt.Floor(IsoDraw.Planks(Rgba.Hex("#8a6440"), 2.5f, 54)));
        p.Png("tiles/floor_thatch.png", TileArt.Floor(IsoDraw.Organic(Rgba.Hex("#b09040"), Rgba.Hex("#8a7030"), 3f, 55, 0.2f)));

        foreach (bool west in new[] { true, false })
        {
            string s = west ? "w" : "n";
            p.Png($"tiles/wall_stone_{s}.png", TileArt.Wall(west, stoneWall, 24f, 3f));
            p.Png($"tiles/wall_stone_ruin_{s}.png", TileArt.Ruin(west, stoneWall));
            p.Png($"tiles/wall_timber_{s}.png", TileArt.Wall(west, timberWall));
            p.Png($"tiles/wall_timber_ruin_{s}.png", TileArt.Ruin(west, timberWall));
            p.Png($"tiles/shutter_{s}.png", TileArt.Window(west, dark, shutter));
            p.Png($"tiles/shutter_broken_{s}.png", TileArt.Window(west, dark, null));
            p.Png($"tiles/door_{s}.png", TileArt.Door(west, dark, wood, open: false));
            p.Png($"tiles/door_open_{s}.png", TileArt.Door(west, dark, wood, open: true));
            p.Png($"tiles/palisade_{s}.png", TileArt.Fence(west, palisade, 14f));
        }

        p.Png("tiles/haystack.png", TileArt.Bush(Rgba.Hex("#c8a848"), 61));
        p.Png("tiles/barrel.png", TileArt.Barrel(Rgba.Hex("#7a5232"), Rgba.Hex("#404040")));
        p.Png("tiles/tree.png", TileArt.Tree(Rgba.Hex("#4a3424"), Rgba.Hex("#2e5a3a"), conifer: true, 62));
        p.Png("tiles/bush.png", TileArt.Bush(Rgba.Hex("#3e6a38"), 63));
        p.Png("tiles/rock.png", TileArt.Rock(Rgba.Hex("#7a7a76"), 64));
        p.Png("tiles/table.png", TileArt.Table(IsoDraw.Planks(Rgba.Hex("#6a4a2a")), dark));
        p.Png("tiles/cart.png", TileArt.Crate(IsoDraw.Planks(Rgba.Hex("#8a6a3a"), 2f, 65)));
        p.Png("tiles/stairs_n.png", TileArt.Stairs(west: false, IsoDraw.Planks(Rgba.Hex("#7a5a38"), 2f, 66)));
        p.Png("tiles/stairs_w.png", TileArt.Stairs(west: true, IsoDraw.Planks(Rgba.Hex("#7a5a38"), 2f, 67)));
        p.Png("tiles/debris.png", TileArt.Debris(Rgba.Hex("#7a6a5a"), 68));
        p.Png("tiles/shrine.png", TileArt.Console(IsoDraw.Bricks(stone, mortar, 4f, 3f, 69), Rgba.Hex("#80e0ff")));

        var warden = new FigureStyle(Rgba.Hex("#3a5a3a"), Rgba.Hex("#c8b070"), Rgba.Hex("#e0b090"), Rgba.Hex("#5a3a20"), HeadKind.Hood, WeaponLength: 16f);
        var veteran = warden with { Body = Rgba.Hex("#5a5a60"), Accent = Rgba.Hex("#a0a0a8"), Head = HeadKind.Helmet, Bulk = 1.15f, WeaponLength = 12f };
        var raider = new FigureStyle(Rgba.Hex("#6a3a2a"), Rgba.Hex("#a06040"), Rgba.Hex("#d0a080"), Rgba.Hex("#606060"), HeadKind.Bare, WeaponLength: 12f);
        var revenant = new FigureStyle(Rgba.Hex("#4a5048"), Rgba.Hex("#7a8a70"), Rgba.Hex("#c8d0b8"), Rgba.Hex("#8a8a7a"), HeadKind.Skull, Height: 45f, Bulk: 0.9f, WeaponLength: 13f);
        p.Png("units/warden.png", Figures.Sheet(warden));
        p.Png("units/veteran.png", Figures.Sheet(veteran));
        p.Png("units/raider.png", Figures.Sheet(raider));
        p.Png("units/revenant.png", Figures.Sheet(revenant));

        p.Png("items/crossbow.png", ItemArt.Gun(Rgba.Hex("#6a4a2a"), Rgba.Hex("#404040"), 16f, 2.4f));
        p.Png("items/bow.png", ItemArt.Bow(Rgba.Hex("#8a6038"), Rgba.Hex("#e0e0d0")));
        p.Png("items/sword.png", ItemArt.Blade(Rgba.Hex("#c0c4c8"), Rgba.Hex("#5a3a20")));
        p.Png("items/bolts.png", ItemArt.Clip(Rgba.Hex("#6a5030")));
        p.Png("items/firepot.png", ItemArt.Grenade(Rgba.Hex("#a05a30"), Rgba.Hex("#f0d070")));
        p.Png("items/corpse_warden.png", HorizonArt.Corpse(warden));
        p.Png("items/corpse_raider.png", HorizonArt.Corpse(raider));
        p.Png("items/corpse_revenant.png", HorizonArt.Corpse(revenant));

        p.Png("fx/bolt.png", ItemArt.Projectile(Rgba.Hex("#e0d0b0"), Rgba.Hex("#806040"), 9f));
        p.Png("fx/arrow.png", ItemArt.Projectile(Rgba.Hex("#e0e0d0"), Rgba.Hex("#60a0ff"), 10f));
        p.Png("fx/explosion.png", ItemArt.Explosion(Rgba.Hex("#ffc040"), Rgba.Hex("#402010"), seed: 9));

        HorizonArt.Ui(p, Rgba.Hex("#e0c080"));

        p.Wav("audio/crossbow.wav", Sounds.Twang(21, 180, 0.9));
        p.Wav("audio/bow.wav", Sounds.Twang(22, 260, 0.3));
        p.Wav("audio/sword.wav", Sounds.Clang(23));
        p.Wav("audio/firepot.wav", Sounds.Explosion(24, 1.1));
        p.Wav("audio/footstep.wav", Sounds.Footstep(25, 0.7), peak: 0.6);
        p.Wav("audio/door.wav", Sounds.Door(26, creak: true));
        p.Wav("audio/death_human.wav", Sounds.Death(27, 150));
        p.Wav("audio/death_revenant.wav", Sounds.Death(28, 90));
        p.Wav("audio/click.wav", Sounds.Click(29), peak: 0.5);
        p.Wav("audio/turn.wav", Sounds.Jingle([57, 64], 0.2, Wave.Saw, 0.3), peak: 0.5);
        p.Wav("audio/victory.wav", Sounds.Jingle([57, 61, 64, 69], 0.2, Wave.Saw, 0.35), peak: 0.6);
        p.Wav("audio/defeat.wav", Sounds.Jingle([64, 60, 57, 52], 0.3, Wave.Triangle, 0.5), peak: 0.6);
        p.Wav("audio/ambience.wav", Sounds.Ambience(12, 38, 12).Take(), peak: 0.35, loop: true);
    }

    /// <summary>Half-timbered wall: dark beams on a pale infill.</summary>
    private static Material TimberFrame(Rgba timber, Rgba daub) => (face, u, v) =>
    {
        if (face == Face.Top) return timber.Tone(IsoDraw.Light(face));
        bool beam = v < 2f || v > 22f || (u % 8f) < 1.5f || MathF.Abs((u % 8f) - (v % 16f) * 0.5f) < 0.8f;
        float n = (Noise.Hash((int)u, (int)v, 71) - 0.5f) * 0.08f;
        return (beam ? timber : daub).Tone(IsoDraw.Light(face) + n);
    };
}
