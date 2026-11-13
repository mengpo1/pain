using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class DoorTests
{
    // A vertical wall at x = 5 with a door at (5, 2).
    private static readonly string[] Map =
    [
        "###########",
        "#....#....#",
        "#....D....#",
        "#....#....#",
        "###########",
    ];

    [Fact]
    public void Door_axis_is_deduced_from_the_walls()
    {
        GameWorld world = World(Level(Map, new Vector2(2, 2)));
        Door door = Assert.Single(world.Doors);
        // Vertical door: closed along +Y from its top hinge.
        Assert.Equal(new Vector2(5.5f * Tile, 2 * Tile), door.Hinge);
        Assert.Equal(MathF.PI / 2f, door.ClosedAngle, 3);
    }

    [Fact]
    public void Walking_into_a_door_opens_it_and_lets_the_player_through()
    {
        GameWorld world = World(Level(Map, new Vector2(2, 2)));

        Run(world, 2f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.True(world.Player.Position.X > 6 * Tile, $"x = {world.Player.Position.X}");
        Assert.NotEqual(0f, world.Doors[0].Angle);
    }

    [Fact]
    public void A_door_wide_open_against_the_way_is_walked_round_instead_of_blocking()
    {
        // Door swung flat into the east room, along the top of row 2; the player stands under it
        // and walks north, so pushing would open it further than it goes.
        GameWorld world = World(Level(Map, new Vector2(5.6f, 2)));
        Door door = Assert.Single(world.Doors);
        door.Angle = -MathF.PI / 2f;
        door.PreviousAngle = door.Angle;

        Run(world, 1.5f, new PlayerCommands { Move = -Vector2.UnitY, AimTarget = TileCenter(6, 1) });

        Assert.True(world.Player.Position.Y < 2 * Tile - world.Player.Radius, $"y = {world.Player.Position.Y}");
    }

    [Fact]
    public void A_closed_door_blocks_vision_and_bullets()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Map, new Vector2(2, 2), playerWeapon: "pistol").With(Enemy("dummy", 8, 2)).Build());

        Assert.False(world.Physics.HasLineOfSight(world.Player.Position, TileCenter(8, 2), Blocking.Vision));
        Run(world, 0.5f, Aim(world, TileCenter(8, 2), attack: true));
        Assert.True(world.Actors.First(a => !a.IsPlayer).IsAlive);
    }

    [Fact]
    public void A_door_kicked_open_knocks_down_whoever_stands_behind()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Map, new Vector2(3.5f, 2)).With(Enemy("dummy", 5.9f, 2.3f)).Build());
        Actor victim = world.Actors.First(a => !a.IsPlayer);

        Run(world, 1f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.Equal(ActorState.KnockedDown, victim.State);
        Assert.NotNull(Event<DoorHit>(world));
    }

    [Fact]
    public void A_locked_door_does_not_open()
    {
        LevelDefinition level = Level(Map, new Vector2(2, 2));
        level.DoorSymbols["D"].Locked = true;
        GameWorld world = World(level);

        Run(world, 2f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.Equal(0f, world.Doors[0].Angle);
        Assert.True(world.Player.Position.X < 5.5f * Tile);
    }

    [Fact]
    public void Unlock_trigger_opens_the_way()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Map, new Vector2(2, 2))
            .WithInteractable("switch", 1, 1)
            .WithTrigger(TriggerCondition.Activated, "switch", new TriggerAction { Type = TriggerActionType.UnlockDoor, Door = "gate" })
            .Build();
        level.Doors.Add(new LevelDoor { Id = "gate", Definition = "door", Tile = new Vector2(5, 3), Locked = true });
        level.Map[3] = "#....#....#";
        GameWorld world = World(level);
        Door gate = world.Doors.Single(d => d.LevelId == "gate");
        Assert.True(gate.Locked);

        // Walk to the switch and use it.
        Run(world, 0.6f, new PlayerCommands { Move = new Vector2(-1, -1) });
        Run(world, 0.05f, new PlayerCommands { InteractPressed = true });

        Assert.False(gate.Locked);
    }
}
