using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class GeometryTests
{
    [Theory]
    [InlineData(0f, 0.5f, 0.5f)]
    [InlineData(3f, -3f, 0.2831853f)]
    [InlineData(-3f, 3f, -0.2831853f)]
    public void Angle_difference_wraps(float from, float to, float expected) =>
        Assert.Equal(expected, Geo.AngleDifference(from, to), 4);

    [Fact]
    public void Raycast_finds_the_first_wall()
    {
        GameWorld world = World(Level(["##########", "#....#...#", "##########"], new Vector2(1, 1)));

        bool hit = world.Map.Raycast(TileCenter(1, 1), TileCenter(8, 1), Blocking.Movement, out TileHit result);

        Assert.True(hit);
        Assert.Equal(5, result.X);
        Assert.Equal(5 * Tile, result.Point.X, 2);
        Assert.Equal(new Vector2(-1, 0), result.Normal);
    }

    [Fact]
    public void Raycast_ignores_tiles_that_do_not_match_the_mask()
    {
        GameWorld world = World(Level(["##########", "#....=...#", "##########"], new Vector2(1, 1)));

        Assert.False(world.Map.Raycast(TileCenter(1, 1), TileCenter(8, 1), Blocking.Vision, out _));
        Assert.True(world.Map.Raycast(TileCenter(1, 1), TileCenter(8, 1), Blocking.Movement, out _));
    }

    [Fact]
    public void Diagonal_ray_through_a_wall_corner_is_blocked()
    {
        string[] map =
        [
            "######",
            "#..#.#",
            "#.#..#",
            "######",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 1)));

        // From (2,1) to (3,2): passes exactly through the corner shared by two walls.
        Vector2 from = new(2.5f * Tile, 1.5f * Tile);
        Vector2 to = new(3.5f * Tile, 2.5f * Tile);
        Assert.True(world.Map.Raycast(from, to, Blocking.Movement, out _));
    }

    [Fact]
    public void Segment_circle_reports_the_entry_point()
    {
        Assert.True(Geo.SegmentCircle(Vector2.Zero, new Vector2(10, 0), new Vector2(5, 0), 1f, out float t));
        Assert.Equal(0.4f, t, 4);
        Assert.False(Geo.SegmentCircle(Vector2.Zero, new Vector2(10, 0), new Vector2(5, 3), 1f, out _));
    }

    [Fact]
    public void Pathfinder_goes_around_walls()
    {
        string[] map =
        [
            "##########",
            "#........#",
            "#.######.#",
            "#........#",
            "##########",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 3)));
        var path = new List<Vector2>();

        Assert.True(world.Pathfinder.FindPath(TileCenter(2, 1), TileCenter(2, 3), path));

        Assert.Equal(TileCenter(2, 3), path[^1]);
        Assert.All(path, point =>
        {
            (int x, int y) = world.Map.TileAt(point);
            Assert.False(world.Map.Blocks(x, y, Blocking.Movement));
        });
    }

    [Fact]
    public void Pathfinder_does_not_cut_corners()
    {
        string[] map =
        [
            "#####",
            "#..##",
            "##..#",
            "#####",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 1)));
        var path = new List<Vector2>();

        // (2,1) → (2,2) is the only legal way: a diagonal from (1,1) to (2,2) would cut a corner.
        Assert.True(world.Pathfinder.FindPath(TileCenter(1, 1), TileCenter(3, 2), path));
        Assert.Contains(TileCenter(2, 1), path);
    }

    [Fact]
    public void Pathfinder_reports_unreachable_goals()
    {
        string[] map =
        [
            "#######",
            "#..#..#",
            "#..#..#",
            "#######",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 1)));

        Assert.False(world.Pathfinder.FindPath(TileCenter(1, 1), TileCenter(5, 1), new List<Vector2>()));
    }

    [Fact]
    public void Pathfinder_avoids_solid_props()
    {
        LevelDefinition level = new LevelDefinitionBuilder(["#####", "#...#", "#...#", "#...#", "#####"], new Vector2(1, 1)).WithProp(2, 2, 1, 1).Build();
        GameWorld world = World(level);

        Assert.False(world.Pathfinder.IsWalkable(2, 2));
        Assert.True(world.Pathfinder.IsWalkable(1, 2));
    }
}
