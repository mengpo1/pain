namespace PlaceholderArt;

/// <summary>Pack A, "Horizon": near-future town, field operatives against grey visitors.</summary>
public static class HorizonArt
{
    public static void Generate(Pack p)
    {
        Rgba brick = Rgba.Hex("#9a5a42"), mortar = Rgba.Hex("#c8b8a0"), wood = Rgba.Hex("#8a6038");
        Rgba hull = Rgba.Hex("#6c6880"), hullSeam = Rgba.Hex("#3e3a50"), frame = Rgba.Hex("#d8d4cc");
        Material brickWall = IsoDraw.Bricks(brick, mortar);
        Material frameMat = IsoDraw.Solid(frame, 0.04f);
        Material doorMat = IsoDraw.Planks(wood);
        Material hullMat = IsoDraw.Panels(hull, hullSeam);
        Material hatchMat = IsoDraw.Panels(Rgba.Hex("#8a7ab0"), hullSeam, 6f);
        Material glass = IsoDraw.Glass(new Rgba(0.55f, 0.75f, 0.9f, 0.55f));
        Material fence = IsoDraw.Solid(Rgba.Hex("#b0b4b8"), 0.05f);

        p.Png("tiles/floor_grass.png", TileArt.Floor(IsoDraw.Organic(Rgba.Hex("#4f7a34"), Rgba.Hex("#6a9440"), 1f, 21)));
        p.Png("tiles/floor_dirt.png", TileArt.Floor(IsoDraw.Organic(Rgba.Hex("#6e5638"), Rgba.Hex("#8a6e48"), 1.2f, 22)));
        p.Png("tiles/floor_road.png", TileArt.Floor(IsoDraw.Organic(Rgba.Hex("#46484c"), Rgba.Hex("#55585c"), 2f, 23, 0.16f)));
        p.Png("tiles/floor_concrete.png", TileArt.Floor(IsoDraw.Tiles(Rgba.Hex("#9a9a94"), Rgba.Hex("#808078"), 8f, 24)));
        p.Png("tiles/floor_tiles.png", TileArt.Floor(IsoDraw.Tiles(Rgba.Hex("#c8c0a8"), Rgba.Hex("#8a8270"), 4f, 25)));
        p.Png("tiles/floor_roof.png", TileArt.Floor(IsoDraw.Tiles(Rgba.Hex("#7a4a3a"), Rgba.Hex("#5a3428"), 3f, 26)));
        p.Png("tiles/floor_hull.png", TileArt.Floor(IsoDraw.Panels(Rgba.Hex("#5c5870"), hullSeam, 8f, 27)));

        foreach (bool west in new[] { true, false })
        {
            string s = west ? "w" : "n";
            p.Png($"tiles/wall_brick_{s}.png", TileArt.Wall(west, brickWall));
            p.Png($"tiles/wall_brick_ruin_{s}.png", TileArt.Ruin(west, brickWall));
            p.Png($"tiles/window_{s}.png", TileArt.Window(west, frameMat, glass));
            p.Png($"tiles/window_broken_{s}.png", TileArt.Window(west, frameMat, null));
            p.Png($"tiles/door_{s}.png", TileArt.Door(west, frameMat, doorMat, open: false));
            p.Png($"tiles/door_open_{s}.png", TileArt.Door(west, frameMat, doorMat, open: true));
            p.Png($"tiles/fence_{s}.png", TileArt.Fence(west, fence));
            p.Png($"tiles/hull_{s}.png", TileArt.Wall(west, hullMat));
            p.Png($"tiles/hatch_{s}.png", TileArt.Hatch(west, hullMat, hatchMat, open: false));
            p.Png($"tiles/hatch_open_{s}.png", TileArt.Hatch(west, hullMat, hatchMat, open: true));
        }

        p.Png("tiles/crate.png", TileArt.Crate(IsoDraw.Planks(Rgba.Hex("#a07848"), 3f, 31)));
        p.Png("tiles/barrel.png", TileArt.Barrel(Rgba.Hex("#c04030"), Rgba.Hex("#e0d040")));
        p.Png("tiles/tree.png", TileArt.Tree(Rgba.Hex("#5a4030"), Rgba.Hex("#3e7030"), conifer: false, 32));
        p.Png("tiles/bush.png", TileArt.Bush(Rgba.Hex("#4a7a30"), 33));
        p.Png("tiles/rock.png", TileArt.Rock(Rgba.Hex("#8a8680"), 34));
        p.Png("tiles/table.png", TileArt.Table(IsoDraw.Planks(Rgba.Hex("#7a5232")), IsoDraw.Solid(Rgba.Hex("#4a3420"))));
        p.Png("tiles/console.png", TileArt.Console(IsoDraw.Solid(Rgba.Hex("#505868")), Rgba.Hex("#40e0c0")));
        foreach (bool west in new[] { true, false })
        {
            string s = west ? "w" : "n";
            p.Png($"tiles/stairs_low_{s}.png", TileArt.Stairs(west, high: false, IsoDraw.Solid(Rgba.Hex("#a8a49c"), 0.05f)));
            p.Png($"tiles/stairs_high_{s}.png", TileArt.Stairs(west, high: true, IsoDraw.Solid(Rgba.Hex("#a8a49c"), 0.05f)));
        }

        p.Png("tiles/debris.png", TileArt.Debris(Rgba.Hex("#8a6a50"), 35));
        p.Png("tiles/pod.png", TileArt.Console(IsoDraw.Panels(hull, hullSeam, 5f), Rgba.Hex("#c060ff")));

        var operative = new FigureStyle(Rgba.Hex("#3a5a7a"), Rgba.Hex("#7aa0c0"), Rgba.Hex("#e0b090"), Rgba.Hex("#2a2a2e"), HeadKind.Helmet);
        var heavy = operative with { Body = Rgba.Hex("#4a5a48"), Accent = Rgba.Hex("#90a070"), Bulk = 1.2f, WeaponLength = 17f };
        var gaunt = new FigureStyle(Rgba.Hex("#7a7a88"), Rgba.Hex("#a8a8b8"), Rgba.Hex("#b0b4c0"), Rgba.Hex("#6040a0"), HeadKind.Alien, Height: 40f, Bulk: 0.8f, WeaponLength: 11f, HeadSize: 1.25f);
        var warden = new FigureStyle(Rgba.Hex("#4a3a60"), Rgba.Hex("#a070d0"), Rgba.Hex("#8a7aa0"), Rgba.Hex("#c060ff"), HeadKind.Helmet, Height: 46f, Bulk: 1.3f, WeaponLength: 15f);
        p.Png("units/operative.png", Figures.Sheet(operative));
        p.Png("units/operative_heavy.png", Figures.Sheet(heavy));
        p.Png("units/gaunt.png", Figures.Sheet(gaunt));
        p.Png("units/warden.png", Figures.Sheet(warden));

        p.Png("items/rifle.png", ItemArt.Gun(Rgba.Hex("#2e3034"), Rgba.Hex("#5a6068")));
        p.Png("items/pistol.png", ItemArt.Gun(Rgba.Hex("#2e3034"), Rgba.Hex("#606870"), 11f, 2.2f));
        p.Png("items/pulse_carbine.png", ItemArt.Gun(Rgba.Hex("#504070"), Rgba.Hex("#c070ff"), 18f, 2.8f));
        p.Png("items/clip.png", ItemArt.Clip(Rgba.Hex("#505050")));
        p.Png("items/cell.png", ItemArt.Clip(Rgba.Hex("#9060d0")));
        p.Png("items/knife.png", ItemArt.Blade(Rgba.Hex("#b8bcc0"), Rgba.Hex("#303030")));
        p.Png("items/grenade.png", ItemArt.Grenade(Rgba.Hex("#506040"), Rgba.Hex("#c0c0c0")));
        p.Png("items/corpse_operative.png", Corpse(operative));
        p.Png("items/corpse_gaunt.png", Corpse(gaunt));
        p.Png("items/corpse_warden.png", Corpse(warden));

        p.Png("fx/bullet.png", ItemArt.Projectile(Rgba.Hex("#fff0b0"), Rgba.Hex("#ffb040")));
        p.Png("fx/grenade.png", ItemArt.SmallGrenade(Rgba.Hex("#506040"), Rgba.Hex("#c0c0c0")));
        p.Png("fx/pulse.png", ItemArt.Projectile(Rgba.Hex("#f0d0ff"), Rgba.Hex("#b040ff"), 8f));
        p.Png("fx/explosion.png", ItemArt.Explosion(Rgba.Hex("#ffe080"), Rgba.Hex("#603020")));

        Ui(p, Rgba.Hex("#f0d040"));

        p.Wav("audio/rifle.wav", Sounds.Gunshot(Sounds.Rifle, 1));
        p.Wav("audio/pistol.wav", Sounds.Gunshot(Sounds.Pistol, 2));
        p.Wav("audio/pulse.wav", Sounds.Zap(3));
        p.Wav("audio/explosion.wav", Sounds.Explosion(4));
        p.Wav("audio/footstep.wav", Sounds.Footstep(5), peak: 0.6);
        p.Wav("audio/door.wav", Sounds.Door(6, creak: true));
        p.Wav("audio/hatch.wav", Sounds.Door(7, creak: false));
        p.Wav("audio/death_human.wav", Sounds.Death(8, 160));
        p.Wav("audio/death_alien.wav", Sounds.Death(9, 320));
        p.Wav("audio/click.wav", Sounds.Click(10), peak: 0.5);
        p.Wav("audio/turn.wav", Sounds.Jingle([69, 76], 0.12, Wave.Square, 0.15), peak: 0.5);
        p.Wav("audio/victory.wav", Sounds.Jingle([60, 64, 67, 72], 0.15, Wave.Square), peak: 0.6);
        p.Wav("audio/defeat.wav", Sounds.Jingle([67, 63, 60, 55], 0.22, Wave.Triangle, 0.4), peak: 0.6);
        p.Wav("audio/ambience.wav", Sounds.Ambience(12, 45, 11).Take(), peak: 0.35, loop: true);
    }

    public static Canvas Corpse(FigureStyle style)
    {
        var sheet = Figures.Sheet(style);
        var c = TileArt.New();
        // Frame 16 of the sheet is the lying pose.
        for (int y = 0; y < IsoDraw.FrameHeight; y++)
            for (int x = 0; x < IsoDraw.FrameWidth; x++)
                c[x, y] = sheet[16 * IsoDraw.FrameWidth + x, y];
        return c;
    }

    /// <summary>Map cursors and markers, shared look with a pack-specific accent.</summary>
    public static void Ui(Pack p, Rgba accent)
    {
        p.Png("ui/cursor.png", ItemArt.Cursor(accent));
        p.Png("ui/cursor_target.png", ItemArt.Cursor(Rgba.Hex("#ff4030"), cross: true));
        p.Png("ui/cursor_blocked.png", ItemArt.Cursor(Rgba.Hex("#909090")));
        p.Png("ui/path.png", ItemArt.Marker(Rgba.Hex("#60e070").WithAlpha(0.8f)));
        p.Png("ui/path_short.png", ItemArt.Marker(Rgba.Hex("#ff7040").WithAlpha(0.8f)));
        p.Png("ui/path_reserve.png", ItemArt.Marker(Rgba.Hex("#f0d040").WithAlpha(0.8f)));
        p.Png("ui/zone.png", ItemArt.Marker(Rgba.Hex("#40c0ff").WithAlpha(0.28f), 0.95f));
        p.Png("ui/arrow.png", ItemArt.Arrow(accent));
    }
}
