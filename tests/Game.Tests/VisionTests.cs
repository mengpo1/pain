using Game.Core.Battle;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Tests;

public class VisionTests
{
    [Theory]
    [InlineData(0, 0, -5, true)]   // north, straight ahead
    [InlineData(0, 3, -3, true)]   // north, on the 45° edge
    [InlineData(0, 1, 0, false)]   // north, tile right beside: excluded (OXCE)
    [InlineData(0, 0, 4, false)]   // north, behind
    [InlineData(1, 5, 0, true)]    // north-east covers the whole quadrant, axes included
    [InlineData(1, -1, -1, false)]
    [InlineData(4, -2, 3, true)]
    [InlineData(6, -4, 4, true)]
    public void View_sector_matches_oxce(int facing, int dx, int dy, bool inside)
    {
        var from = new Position(10, 10, 0);
        Assert.Equal(inside, VisionSystem.InViewSector(facing, from, from.Offset(dx, dy)));
    }

    [Fact]
    public void Walls_hide_the_terrain_behind_them()
    {
        Battle battle = TestWorld.Battle();
        for (int y = 0; y < 20; y++) battle.Set(8, y, 0, TilePartSlot.WestWall, "wallW");
        battle.Soldier(Faction.Player, 5, 10, direction: 2);
        battle.Start();
        Assert.True(battle.Map[7, 10, 0]!.IsDiscovered(Faction.Player));
        Assert.True(battle.Map[8, 10, 0]!.IsDiscovered(Faction.Player) is false);
        Assert.False(battle.Map[12, 10, 0]!.IsDiscovered(Faction.Player));
    }

    [Fact]
    public void Windows_let_sight_through()
    {
        Battle battle = TestWorld.Battle();
        for (int y = 0; y < 20; y++) battle.Set(8, y, 0, TilePartSlot.WestWall, "windowW");
        battle.Soldier(Faction.Player, 5, 10, direction: 2);
        battle.Start();
        Assert.True(battle.Map[12, 10, 0]!.IsDiscovered(Faction.Player));
    }

    [Fact]
    public void Units_are_seen_within_twenty_tiles_in_daylight()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 5);
        BattleUnit watcher = battle.Soldier(Faction.Player, 1, 2, direction: 2);
        BattleUnit near = battle.Soldier(Faction.Hostile, 15, 2);
        BattleUnit far = battle.Soldier(Faction.Hostile, 25, 2);
        battle.Start();
        Assert.Contains(near, watcher.VisibleUnits);
        Assert.DoesNotContain(far, watcher.VisibleUnits);
    }

    [Fact]
    public void In_the_dark_the_player_sees_units_only_within_nine_tiles_unless_they_are_lit()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 15);
        battle.Map.GlobalLight = 0;
        BattleUnit watcher = battle.Soldier(Faction.Player, 1, 2, direction: 2);
        // The watcher's own light (15) lights about 9 tiles: put targets beyond it, and the lamp
        // far enough from the first one (light fades by 1 per tile).
        BattleUnit inDark = battle.Soldier(Faction.Hostile, 14, 2);
        BattleUnit lit = battle.Soldier(Faction.Hostile, 14, 12);
        battle.Set(14, 12, 0, TilePartSlot.Object, "lamp");
        battle.Start();
        Assert.DoesNotContain(inDark, watcher.VisibleUnits);
        Assert.Contains(lit, watcher.VisibleUnits);
    }

    [Fact]
    public void Hostiles_see_in_the_dark_at_full_range()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 5);
        battle.Map.GlobalLight = 0;
        BattleUnit alien = battle.Soldier(Faction.Hostile, 1, 2, direction: 2);
        BattleUnit soldier = battle.Soldier(Faction.Player, 18, 2, direction: 6);
        battle.Start();
        // The soldier carries a light, but even unlit, hostiles have 20 tiles of dark vision.
        Assert.Contains(soldier, alien.VisibleUnits);
    }

    [Fact]
    public void A_wall_between_two_units_blocks_sight()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(8, 10, 0, TilePartSlot.WestWall, "wallW");
        BattleUnit watcher = battle.Soldier(Faction.Player, 5, 10, direction: 2);
        BattleUnit hidden = battle.Soldier(Faction.Hostile, 10, 10);
        battle.Start();
        Assert.DoesNotContain(hidden, watcher.VisibleUnits);
    }

    [Fact]
    public void Smoke_shortens_sight()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 5);
        BattleUnit watcher = battle.Soldier(Faction.Player, 1, 2, direction: 2);
        BattleUnit target = battle.Soldier(Faction.Hostile, 12, 2);
        for (int x = 4; x < 10; x++) battle.Map[x, 2, 0]!.Smoke = 15;
        battle.Start();
        Assert.DoesNotContain(target, watcher.VisibleUnits);
    }

    [Fact]
    public void Spotting_a_new_enemy_stops_a_walk()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 9);
        for (int y = 0; y < 9; y++)
            if (y != 4) battle.Set(10, y, 0, TilePartSlot.WestWall, "wallW");
        BattleUnit walker = battle.Soldier(Faction.Player, 2, 4, direction: 2);
        battle.Soldier(Faction.Hostile, 14, 1, direction: 6);
        battle.Start();
        battle.Do(new MoveCommand(walker.Id, new Position(16, 4, 0)));
        Assert.NotEqual(new Position(16, 4, 0), walker.Position);
        Assert.Contains(battle.Events, e => e is ActionInterrupted { ReasonKey: "info.enemy_spotted" });
    }

    [Fact]
    public void The_ai_side_only_knows_enemies_it_has_seen()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 5);
        BattleUnit alien = battle.Soldier(Faction.Hostile, 1, 2, direction: 6);
        BattleUnit soldier = battle.Soldier(Faction.Player, 10, 2, direction: 6);
        battle.Start();
        Assert.Contains(alien, soldier.VisibleUnits);
        Assert.Empty(battle.KnownEnemies(Faction.Hostile, 2));

        battle.Do(new EndTurnCommand());
        battle.Do(new TurnCommand(alien.Id, 2));
        Assert.Contains(soldier, battle.KnownEnemies(Faction.Hostile, 2));
        Assert.Equal(soldier.Position, alien.LastKnownPosition(soldier));
    }
}
