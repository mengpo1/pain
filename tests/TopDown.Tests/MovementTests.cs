using System.Numerics;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class MovementTests
{
    [Fact]
    public void Player_moves_at_its_speed()
    {
        GameWorld world = World(Level(Room(30, 5), new Vector2(2, 2)));
        Vector2 start = world.Player.Position;

        Run(world, 1f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = start + new Vector2(100, 0) });

        float travelled = world.Player.Position.X - start.X;
        // 190 units/s, minus the few milliseconds spent accelerating.
        Assert.InRange(travelled, 180f, 190.5f);
        Assert.Equal(start.Y, world.Player.Position.Y, 3);
    }

    [Fact]
    public void Diagonal_movement_is_not_faster()
    {
        GameWorld straight = World(Level(Room(30, 30), new Vector2(5, 5)));
        GameWorld diagonal = World(Level(Room(30, 30), new Vector2(5, 5)));

        Run(straight, 0.5f, new PlayerCommands { Move = Vector2.UnitX });
        Run(diagonal, 0.5f, new PlayerCommands { Move = new Vector2(1, 1) });

        float straightDistance = Vector2.Distance(straight.Player.Position, TileCenter(5, 5));
        float diagonalDistance = Vector2.Distance(diagonal.Player.Position, TileCenter(5, 5));
        Assert.Equal(straightDistance, diagonalDistance, 1);
    }

    [Fact]
    public void Walls_stop_the_player()
    {
        GameWorld world = World(Level(Room(6, 5), new Vector2(2, 2)));

        Run(world, 2f, new PlayerCommands { Move = Vector2.UnitX });

        // Inner face of the right wall is at x = 5 tiles.
        float wallFace = 5 * Tile;
        Assert.True(world.Player.Position.X <= wallFace - world.Player.Radius + 0.01f, $"x = {world.Player.Position.X}");
        Assert.True(world.Player.Position.X > wallFace - world.Player.Radius - 2f);
    }

    [Fact]
    public void Player_slides_along_a_wall()
    {
        GameWorld world = World(Level(Room(20, 5), new Vector2(2, 2)));
        float startX = world.Player.Position.X;

        // Pushing up and right: the top wall blocks Y, X keeps moving.
        Run(world, 1f, new PlayerCommands { Move = new Vector2(1, -1) });

        Assert.True(world.Player.Position.X - startX > 100f);
        Assert.True(world.Player.Position.Y >= Tile + world.Player.Radius - 0.01f);
    }

    [Fact]
    public void Player_passes_through_a_one_tile_corridor()
    {
        string[] map =
        [
            "##########",
            "#...#....#",
            "#........#",
            "#...#....#",
            "##########",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 2)));

        Run(world, 2f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.True(world.Player.Position.X > 6 * Tile, $"x = {world.Player.Position.X}");
    }

    [Fact]
    public void Player_never_ends_up_inside_a_wall_corner()
    {
        string[] map =
        [
            "#######",
            "#.....#",
            "#..#..#",
            "#.....#",
            "#######",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 1)));

        // Run diagonally into the corner of the centre block.
        Run(world, 2f, new PlayerCommands { Move = new Vector2(1, 1) });

        Vector2 position = world.Player.Position;
        var block = world.Map.TileRect(3, 2);
        Vector2 closest = new(Math.Clamp(position.X, block.Left, block.Right), Math.Clamp(position.Y, block.Top, block.Bottom));
        Assert.True(Vector2.Distance(closest, position) >= world.Player.Radius - 0.05f);
    }

    [Fact]
    public void Characters_do_not_overlap()
    {
        LevelDefinitionBuilder builder = new(Room(12, 5), new Vector2(2, 2));
        GameWorld world = World(builder.With(Enemy("dummy", 4, 2)).Build());

        Run(world, 1.5f, new PlayerCommands { Move = Vector2.UnitX });

        Actor dummy = world.Actors.Single(a => !a.IsPlayer);
        float distance = Vector2.Distance(dummy.Position, world.Player.Position);
        Assert.True(distance >= dummy.Radius + world.Player.Radius - 0.5f, $"distance = {distance}");
    }

    [Fact]
    public void Speed_modifier_changes_player_speed()
    {
        var definitions = Definitions();
        GameWorld normal = World(Level(Room(40, 5), new Vector2(2, 2)), definitions);
        GameWorld swift = World(Level(Room(40, 5), new Vector2(2, 2)), definitions, definitions.Modifiers["swift"]);

        Run(normal, 1f, new PlayerCommands { Move = Vector2.UnitX });
        Run(swift, 1f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.True(swift.Player.Position.X - normal.Player.Position.X > 80f);
    }
}
