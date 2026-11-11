using Game.Core.Battle;
using Game.Core.Battle.Movement;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Tests;

public class MovementTests
{
    [Fact]
    public void Orthogonal_step_costs_floor_tu_and_half_as_much_energy()
    {
        Battle battle = TestWorld.Battle();
        Assert.True(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 2, out Step step));
        Assert.Equal(4, step.Tu);
        Assert.Equal(2, step.Energy);
    }

    [Fact]
    public void Diagonal_step_costs_one_and_a_half_truncated()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(6, 4, 0, TilePartSlot.Floor, "mud"); // 6 TU floor → 9 diagonally
        Assert.True(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 1, out Step step));
        Assert.Equal(9, step.Tu);
        Assert.True(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 3, out Step plain));
        Assert.Equal(6, plain.Tu);
    }

    [Fact]
    public void Walls_block_the_edge_they_stand_on()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(6, 5, 0, TilePartSlot.WestWall, "wallW"); // between (5,5) and (6,5)
        Assert.False(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 2, out _));
        Assert.False(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(6, 5, 0), 6, out _));
        Assert.True(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 4, out _));
    }

    [Fact]
    public void Diagonal_cannot_cut_a_wall_corner_but_can_pass_beside_an_object()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(6, 5, 0, TilePartSlot.WestWall, "wallW"); // wall on the east edge of (5,5)
        Assert.False(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 3, out _)); // SE
        battle.Set(6, 5, 0, TilePartSlot.WestWall, null);
        battle.Set(6, 5, 0, TilePartSlot.Object, "crate");
        // OXCE: an ordinary solid object beside the corner does not block the diagonal.
        Assert.True(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 3, out _));
    }

    [Fact]
    public void Doors_are_crossed_orthogonally_and_reported()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(6, 5, 0, TilePartSlot.WestWall, "doorW");
        Assert.True(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 2, out Step step));
        Assert.Equal((new Position(6, 5, 0), TilePartSlot.WestWall), step.Door);
    }

    [Fact]
    public void Walking_through_a_door_opens_it()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(6, 5, 0, TilePartSlot.WestWall, "doorW");
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5);
        battle.Do(new MoveCommand(unit.Id, new Position(7, 5, 0)));
        Assert.Equal(new Position(7, 5, 0), unit.Position);
        Assert.Equal("doorOpenW", battle.Map[6, 5, 0]!.WestWall!.Id);
    }

    [Fact]
    public void Stairs_lead_to_the_level_above_and_back()
    {
        Battle battle = TestWorld.Battle(levels: 2);
        // Ramp going east: 8 voxels, 16 voxels, then the upper floor at (7,5,1).
        battle.Set(5, 5, 0, TilePartSlot.Object, "step8");
        battle.Set(6, 5, 0, TilePartSlot.Object, "step16");
        battle.Set(7, 5, 1, TilePartSlot.Floor, "ground");
        battle.Set(7, 5, 0, TilePartSlot.Floor, null);
        BattleUnit unit = battle.Soldier(Faction.Player, 4, 5);

        battle.Do(new MoveCommand(unit.Id, new Position(7, 5, 1)));
        Assert.Equal(new Position(7, 5, 1), unit.Position);
        Assert.Equal(60 - 4 - 4 - 4 - 1, unit.Tu); // three steps of 4 TU, +1 to climb

        battle.Do(new MoveCommand(unit.Id, new Position(4, 5, 0)));
        Assert.Equal(new Position(4, 5, 0), unit.Position);
    }

    [Fact]
    public void A_step_cannot_rise_more_than_eight_voxels()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(6, 5, 0, TilePartSlot.Object, "step16");
        Assert.False(MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(5, 5, 0), 2, out _));
    }

    [Fact]
    public void Path_goes_around_obstacles_and_costs_the_sum_of_its_steps()
    {
        Battle battle = TestWorld.Battle();
        for (int y = 0; y < 9; y++) battle.Set(6, y, 0, TilePartSlot.WestWall, "wallW");
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5);
        PathResult path = battle.FindPath(unit, new Position(7, 5, 0));
        Assert.True(path.Found);
        Assert.Equal(path.Steps.Sum(s => s.Tu), path.TotalTu);
        Assert.True(path.Steps.Count > 2);
    }

    [Fact]
    public void Unreachable_destination_gives_no_path()
    {
        Battle battle = TestWorld.Battle();
        for (int y = 0; y < 20; y++) battle.Set(6, y, 0, TilePartSlot.WestWall, "wallW");
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5);
        Assert.False(battle.FindPath(unit, new Position(8, 5, 0)).Found);
        Assert.False(battle.Execute(new MoveCommand(unit.Id, new Position(8, 5, 0))).Accepted);
    }

    [Fact]
    public void Walk_stops_on_the_last_affordable_tile()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 0, 0);
        unit.Tu = 10;
        battle.Do(new MoveCommand(unit.Id, new Position(10, 0, 0)));
        Assert.Equal(new Position(2, 0, 0), unit.Position);
        Assert.Equal(2, unit.Tu);
        Assert.Contains(battle.Events, e => e is ActionInterrupted { ReasonKey: "error.not_enough_tu" });
    }

    [Fact]
    public void Kneeling_unit_stands_up_first_and_the_path_preview_counts_it()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 0, 0);
        battle.Do(new KneelCommand(unit.Id));
        Assert.True(unit.Kneeling);
        Assert.Equal(56, unit.Tu);
        PathResult path = battle.FindPath(unit, new Position(2, 0, 0));
        Assert.Equal(8 + 8, path.TotalTu);
        battle.Do(new MoveCommand(unit.Id, new Position(2, 0, 0)));
        Assert.False(unit.Kneeling);
        Assert.Equal(56 - 16, unit.Tu);
    }

    [Fact]
    public void Turning_costs_one_tu_per_eighth_by_the_shortest_way()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5, direction: 0);
        battle.Do(new TurnCommand(unit.Id, 6));
        Assert.Equal(58, unit.Tu);
        Assert.Equal(6, unit.Direction);
    }

    [Fact]
    public void Known_units_are_walked_around_but_not_through()
    {
        Battle battle = TestWorld.Battle(width: 10, length: 3);
        BattleUnit walker = battle.Soldier(Faction.Player, 0, 1);
        battle.Soldier(Faction.Player, 1, 1);
        PathResult path = battle.FindPath(walker, new Position(2, 1, 0));
        Assert.True(path.Found);
        Assert.DoesNotContain(path.Steps, s => s.To == new Position(1, 1, 0));
    }
}

public class ReserveTests
{
    [Fact]
    public void Reserved_snap_shot_stops_the_walk_but_not_the_shot()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 0, 0, weapon: "rifle");
        battle.PlayerReserve.Mode = ReserveMode.Snap; // 15 TU
        battle.Do(new MoveCommand(unit.Id, new Position(19, 0, 0)));
        Assert.Equal(new Position(11, 0, 0), unit.Position); // 11 steps × 4 = 44 TU, 16 left ≥ 15
        Assert.Contains(battle.Events, e => e is ActionInterrupted { ReasonKey: "error.tu_reserved" });
        Assert.True(battle.Execute(new AttackCommand(unit.Id, unit.ItemInSlot(Inventory.RightHand)!.Id, AttackMode.Snap, new Position(15, 0, 0))).Accepted);
    }

    [Fact]
    public void Units_that_cannot_pay_the_reserve_are_not_restricted()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 0, 0, weapon: "rifle");
        battle.PlayerReserve.Mode = ReserveMode.Aimed; // 48 TU
        unit.Tu = 20;
        battle.Do(new MoveCommand(unit.Id, new Position(3, 0, 0)));
        Assert.Equal(new Position(3, 0, 0), unit.Position);
    }

    [Fact]
    public void Missing_mode_falls_back_to_snap()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 0, 0, weapon: "laser");
        battle.PlayerReserve.Mode = ReserveMode.Auto;
        Assert.Equal(10, battle.PlayerReserve.For(battle, unit));
        battle.PlayerReserve.Kneel = true;
        Assert.Equal(14, battle.PlayerReserve.For(battle, unit));
    }
}
