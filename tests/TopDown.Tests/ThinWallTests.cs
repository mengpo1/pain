using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>
/// Thin walls (TileDefinition.Thickness): a band through the tile joined to the neighbouring walls and
/// doorways, the rest of the tile open. Collisions, rays and paths all use the same solid parts.
/// </summary>
public class ThinWallTests
{
    private const float Band = 8f;
    private const float Margin = (Tile - Band) / 2f; // 12

    /// <summary>'.' floor, 'T' thin wall, '#' full wall, 'D' door (positions in tiles).</summary>
    private static GameWorld World(string[] map, Vector2 player, params LevelActor[] actors)
    {
        var level = new LevelDefinition
        {
            Id = "thin",
            TileSize = Tile,
            Legend = new() { ["."] = "floor", ["T"] = "thin_wall", ["#"] = "wall" },
            DoorSymbols = new() { ["D"] = new LevelDoorSymbol { Tile = "floor", Door = "door" } },
            Map = map.ToList(),
            Player = new LevelPlayer { Definition = "player", Position = player, WeaponId = "none" },
            Actors = actors.ToList(),
        };
        return TestContent.World(level);
    }

    private static readonly string[] Rooms =
    [
        "TTTTTTTTTT",
        "T....T...T",
        "T....T...T",
        "T....D...T",
        "T....T...T",
        "TTTTTTTTTT",
    ];

    private static RectF Rect(float x, float y, float w, float h) => new(x, y, w, h);

    // ------------------------------------------------------------------ shapes

    [Fact]
    public void A_wall_line_is_a_band_through_the_middle_of_its_tiles()
    {
        TileMap map = World(Rooms, new Vector2(2, 2)).Map;

        // Top wall, tile (2, 0): neighbours left and right are walls, above is outside, below is floor.
        Assert.Equal([Rect(2 * Tile, Margin, Tile, Band)], map.Solids(2, 0));
        // Inner vertical wall, tile (5, 1): wall above (top line) and below.
        Assert.Equal([Rect(5 * Tile + Margin, Tile, Band, Tile)], map.Solids(5, 1));
    }

    [Fact]
    public void A_corner_is_an_L_and_a_junction_a_T()
    {
        TileMap map = World(Rooms, new Vector2(2, 2)).Map;

        // Top-left corner: centre, right arm and lower arm only.
        Assert.Equal([Rect(Margin, Margin, Tile - Margin, Band), Rect(Margin, Margin + Band, Band, Margin)], map.Solids(0, 0));
        // Where the inner wall meets the top line: band across, plus the arm going down.
        Assert.Equal([Rect(5 * Tile, Margin, Tile, Band), Rect(5 * Tile + Margin, Margin + Band, Band, Margin)], map.Solids(5, 0));
    }

    [Fact]
    public void A_mass_of_walls_stays_solid_inside()
    {
        string[] block =
        [
            "......",
            ".TT...",
            ".TT...",
            "......",
        ];
        TileMap map = World(block, new Vector2(4, 1)).Map;

        // Each tile of a 2 x 2 block keeps the quarter that faces the others, joined at the middle.
        Assert.Equal([Rect(Tile + Margin, Tile + Margin, Tile - Margin, Tile - Margin)], map.Solids(1, 1));
        Assert.Equal([Rect(2 * Tile, 2 * Tile, Tile - Margin, Tile - Margin)], map.Solids(2, 2));
    }

    [Fact]
    public void Walls_reach_into_a_doorway_to_meet_the_door()
    {
        TileMap map = World(Rooms, new Vector2(2, 2)).Map;

        // Above and below the door at (5, 3), the band runs to the edge shared with the doorway.
        Assert.Equal([Rect(5 * Tile + Margin, 2 * Tile, Band, Tile)], map.Solids(5, 2));
        Assert.Equal([Rect(5 * Tile + Margin, 4 * Tile, Band, Tile)], map.Solids(5, 4));
        Assert.Empty(map.Solids(5, 3));
    }

    [Fact]
    public void Full_walls_are_unchanged()
    {
        string[] map = ["####", "#..#", "####"];
        TileMap tiles = World(map, new Vector2(1, 1)).Map;

        Assert.False(tiles.IsThin(0, 0));
        Assert.Equal([tiles.TileRect(0, 0)], tiles.Solids(0, 0));
        Assert.Empty(tiles.Solids(1, 1));
    }

    [Fact]
    public void A_tile_that_changes_reshapes_its_neighbours()
    {
        GameWorld world = World(Rooms, new Vector2(2, 2));
        TileMap map = world.Map;
        Assert.Equal(2, map.Solids(5, 0).Count);

        map.Set(5, 1, world.Definitions.Tiles["floor"]);

        // The T-junction above lost its lower arm.
        Assert.Equal([Rect(5 * Tile, Margin, Tile, Band)], map.Solids(5, 0));
    }

    // ------------------------------------------------------------------ movement and rays

    [Fact]
    public void A_character_walks_into_the_free_part_of_a_wall_tile()
    {
        GameWorld world = World(Rooms, new Vector2(2, 1));
        float radius = world.Player.Radius;

        Run(world, 1.5f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = TileCenter(8, 1) });

        // Stopped by the band of the inner wall (x = 5 * 32 + 12), not by the edge of its tile.
        Assert.Equal(5 * Tile + Margin - radius, world.Player.Position.X, 1);
    }

    [Fact]
    public void An_outer_wall_leaves_its_outer_half_open()
    {
        GameWorld world = World(Rooms, new Vector2(2, 2));

        Run(world, 1.5f, new PlayerCommands { Move = -Vector2.UnitY, AimTarget = TileCenter(2, 0) });

        Assert.Equal(Margin + Band + world.Player.Radius, world.Player.Position.Y, 1);
    }

    [Fact]
    public void A_ray_stops_on_the_band_and_passes_beside_it()
    {
        TileMap map = World(Rooms, new Vector2(2, 2)).Map;
        Vector2 from = TileCenter(2, 1), to = TileCenter(8, 1);

        Assert.True(map.Raycast(from, to, Blocking.Projectiles, out TileHit hit));
        Assert.Equal((5, 1), (hit.X, hit.Y));
        Assert.Equal(5 * Tile + Margin, hit.Point.X, 2);
        Assert.Equal(-Vector2.UnitX, hit.Normal);

        // Along the inner wall, inside its tiles but beside the band: nothing in the way.
        Vector2 alongFrom = new(5 * Tile + 4f, 1.2f * Tile), alongTo = new(5 * Tile + 4f, 2.8f * Tile);
        Assert.False(map.Raycast(alongFrom, alongTo, Blocking.Projectiles, out _));
    }

    [Fact]
    public void Characters_see_each_other_past_a_wall_end()
    {
        string[] map =
        [
            "TTTTTTTTT",
            "T.......T",
            "T..T....T",
            "T..T....T",
            "T.......T",
            "TTTTTTTTT",
        ];
        GameWorld world = World(map, new Vector2(2, 3));

        // The wall stub at (3, 2)-(3, 3) ends at the middle of its tile: a ray just above that end is clear.
        Assert.True(world.Physics.HasLineOfSight(new Vector2(2.5f * Tile, 2.2f * Tile), new Vector2(4.5f * Tile, 2.2f * Tile), Blocking.Vision));
        Assert.False(world.Physics.HasLineOfSight(new Vector2(2.5f * Tile, 3.5f * Tile), new Vector2(4.5f * Tile, 3.5f * Tile), Blocking.Vision));
    }

    [Fact]
    public void A_path_starts_from_inside_a_wall_tile()
    {
        GameWorld world = World(Rooms, new Vector2(2, 2));
        var path = new List<Vector2>();

        // A point in the open part of the inner wall's tile, left of the band.
        Vector2 start = new(5 * Tile + 4f, 1.5f * Tile);
        Assert.True(world.Pathfinder.FindPath(start, TileCenter(7, 2), path));
        Assert.Equal(TileCenter(7, 2), path[^1]);
    }

    [Fact]
    public void The_validator_warns_about_thin_walls_meeting_at_a_corner()
    {
        // A throw-away mod with one level, loaded on top of the shipped action pack (thin walls).
        string root = Path.Combine(Path.GetTempPath(), "thin-walls-" + Guid.NewGuid().ToString("N"));
        string mod = Path.Combine(root, "corner");
        Directory.CreateDirectory(Path.Combine(mod, "levels"));
        File.WriteAllText(Path.Combine(mod, "manifest.json"),
            """{ "id": "corner", "name": "Corner", "version": "1.0.0", "formatVersion": 1, "dependencies": ["base"] }""");
        File.WriteAllText(Path.Combine(mod, "levels", "corner.json"), """
            {
              "id": "corner", "nameKey": "level.arena.name", "tileSize": 32,
              "legend": { "#": "wall_white", ".": "floor_wood" },
              "map": ["#####", "#...#", "#.#.#", "#..##", "#####"],
              "player": { "definition": "player", "position": [1, 1] },
              "objectives": [{ "type": "eliminateAll", "textKey": "objective.eliminateAll" }]
            }
            """);
        try
        {
            TopDown.Content.ContentDatabase content = TopDown.Content.ContentLoader.Load(new TopDown.Content.ContentLoadOptions
            {
                ContentRoot = Path.Combine(RepositoryRoot, "Content"),
                ModsRoot = root,
                EnabledMods = ["corner"],
            });

            // (2, 2) and (3, 3) touch only at a corner.
            Assert.Contains(content.Diagnostics.Items, d => d.ToString().Contains("(2, 2) and (3, 3) only touch at a corner"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string RepositoryRoot
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null && !File.Exists(Path.Combine(directory, "TopDown.slnx")))
                directory = Path.GetDirectoryName(directory);
            return directory ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Fact]
    public void The_player_goes_through_a_door_in_a_thin_wall()
    {
        GameWorld world = World(Rooms, new Vector2(2, 3));

        Run(world, 2f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = TileCenter(8, 3) });

        Assert.True(world.Player.Position.X > 6 * Tile, $"x = {world.Player.Position.X}");
    }
}
