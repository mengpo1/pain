using Game.Core.Battle;
using Game.Core.Battle.Combat;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Tests;

public class ExplosionTests
{
    private static (Battle Battle, BattleUnit Thrower, BattleItem Grenade) Setup()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit thrower = battle.Soldier(Faction.Player, 2, 10, direction: 2);
        BattleItem grenade = battle.Give(thrower, "grenade", Inventory.RightHand);
        return (battle, thrower, grenade);
    }

    [Fact]
    public void Primed_grenade_explodes_at_the_end_of_the_turn_and_kills_nearby_units()
    {
        (Battle battle, BattleUnit thrower, BattleItem grenade) = Setup();
        BattleUnit victim = battle.Soldier(Faction.Hostile, 9, 10, direction: 6);
        victim.Health = 5;
        battle.Soldier(Faction.Hostile, 18, 18).Health = 40; // keeps the battle going
        battle.Start();

        battle.Do(new PrimeCommand(thrower.Id, grenade.Id, 0));
        Assert.Equal(60 - 30, thrower.Tu); // priming: 50 % of max TU
        battle.Do(new ThrowCommand(thrower.Id, grenade.Id, new Position(8, 10, 0)));
        Assert.Equal(30 - 15, thrower.Tu); // throwing: 25 %
        Assert.NotNull(grenade.GroundPosition);
        Assert.Equal(UnitStatus.Active, victim.Status);

        battle.Do(new EndTurnCommand());
        Assert.Contains(battle.Events, e => e is ExplosionStarted);
        Assert.Equal(UnitStatus.Dead, victim.Status);
        Assert.DoesNotContain(grenade, battle.Items);
    }

    [Fact]
    public void Fuse_counts_down_only_at_the_end_of_player_and_hostile_turns()
    {
        (Battle battle, BattleUnit thrower, BattleItem grenade) = Setup();
        battle.Soldier(Faction.Hostile, 18, 18);
        battle.Start();
        battle.Do(new PrimeCommand(thrower.Id, grenade.Id, 1));
        battle.Do(new ThrowCommand(thrower.Id, grenade.Id, new Position(6, 10, 0)));

        battle.Do(new EndTurnCommand()); // end of player turn: 1 → 0
        Assert.Contains(grenade, battle.Items);
        Assert.Equal(0, grenade.Fuse);
        battle.Do(new EndTurnCommand()); // end of hostile turn: explodes
        Assert.DoesNotContain(grenade, battle.Items);
    }

    [Fact]
    public void Strong_walls_stop_the_blast()
    {
        Battle battle = TestWorld.Battle();
        for (int y = 0; y < 20; y++) battle.Set(10, y, 0, TilePartSlot.WestWall, "hullW");
        BattleUnit near = battle.Soldier(Faction.Hostile, 9, 10);
        BattleUnit behind = battle.Soldier(Faction.Hostile, 10, 10);
        int before = behind.Health;
        Explosions.Detonate(battle, new Position(8, 10, 0), battle.Rules.Items["grenade"].Damage!, null);
        Assert.True(near.Health < near.MaxHealth);
        Assert.Equal(before, behind.Health);
    }

    [Fact]
    public void Blast_power_fades_with_distance()
    {
        Battle battle = TestWorld.Battle();
        var damage = new DamageDefinition { Power = 30, Type = "he", Radius = 6 };
        BattleUnit far = battle.Soldier(Faction.Hostile, 14, 10);
        Explosions.Detonate(battle, new Position(8, 10, 0), damage, null);
        // 30 power loses 10 per tile: nothing reaches 6 tiles away.
        Assert.Equal(far.MaxHealth, far.Health);
    }

    [Fact]
    public void Blast_destroys_weak_terrain_and_items_on_the_ground()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(9, 10, 0, TilePartSlot.Object, "crate"); // armour 20
        BattleItem clip = battle.CreateItem(battle.Rules.Items["clip"]);
        battle.DropItem(clip, new Position(8, 11, 0));
        Explosions.Detonate(battle, new Position(8, 10, 0), new DamageDefinition { Power = 120, Type = "he", Radius = 4 }, null);
        Assert.Null(battle.Map[9, 10, 0]!.Object);
        Assert.DoesNotContain(clip, battle.Items);
    }

    [Fact]
    public void Explosion_radius_comes_from_the_item_or_from_power()
    {
        var rules = new BattleRules();
        var he = new DamageTypeDefinition { RadiusEffectiveness = 0.05 };
        Assert.Equal(5, Explosions.Radius(rules, new DamageDefinition { Power = 100 }, he));
        Assert.Equal(3, Explosions.Radius(rules, new DamageDefinition { Power = 100, Radius = 3 }, he));
        Assert.Equal(11, Explosions.Radius(rules, new DamageDefinition { Power = 1000 }, he));
    }

    [Fact]
    public void Heavy_items_and_weak_throwers_do_not_throw_far()
    {
        int light = Throwing.MaxDistanceVoxels(3, 40, 20);
        int heavy = Throwing.MaxDistanceVoxels(40, 40, 20);
        int strong = Throwing.MaxDistanceVoxels(40, 80, 20);
        Assert.True(light > heavy);
        Assert.True(strong > heavy);

        (Battle battle, BattleUnit thrower, _) = Setup();
        BattleItem anvil = battle.CreateItem(battle.Rules.Items["anvil"]);
        Assert.True(Inventory.TryPlace(battle, thrower, anvil, Inventory.LeftHand));
        Assert.Equal("error.out_of_range", battle.Execute(new ThrowCommand(thrower.Id, anvil.Id, new Position(15, 10, 0))).ReasonKey);
    }

    [Fact]
    public void Throws_arc_over_obstacles_unless_a_ceiling_is_in_the_way()
    {
        Battle battle = TestWorld.Battle(levels: 2);
        BattleUnit thrower = battle.Soldier(Faction.Player, 2, 10, direction: 2);
        BattleItem grenade = battle.Give(thrower, "grenade", Inventory.RightHand);
        battle.Set(4, 10, 0, TilePartSlot.Object, "fenceW");
        Assert.Null(Throwing.Validate(battle, thrower, grenade, new Position(7, 10, 0), out _));

        // A full wall can be thrown over in the open...
        for (int y = 0; y < 20; y++) battle.Set(5, y, 0, TilePartSlot.WestWall, "wallW");
        Assert.Null(Throwing.Validate(battle, thrower, grenade, new Position(7, 10, 0), out _));

        // ...but not under a roof.
        for (int x = 0; x < 12; x++)
            for (int y = 0; y < 20; y++)
                battle.Set(x, y, 1, TilePartSlot.Floor, "ground");
        Assert.Equal("error.cannot_throw_there", Throwing.Validate(battle, thrower, grenade, new Position(7, 10, 0), out _));
    }
}

public class InventoryTests
{
    [Fact]
    public void Moving_an_item_costs_the_slot_cost_from_the_content()
    {
        Battle battle = TestWorld.Battle(rules: InventoryRules());
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5);
        BattleItem grenade = battle.Give(unit, "grenade", "belt");
        battle.Do(new MoveItemCommand(unit.Id, grenade.Id, Inventory.LeftHand));
        Assert.Equal(Inventory.LeftHand, grenade.Slot);
        Assert.Equal(60 - 4, unit.Tu);
    }

    [Fact]
    public void Items_can_be_dropped_and_picked_up_on_the_unit_tile()
    {
        Battle battle = TestWorld.Battle(rules: InventoryRules());
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5);
        BattleItem grenade = battle.Give(unit, "grenade", Inventory.RightHand);
        battle.Do(new MoveItemCommand(unit.Id, grenade.Id, Inventory.Ground));
        Assert.Equal(new Position(5, 5, 0), grenade.GroundPosition);
        Assert.DoesNotContain(grenade, unit.Inventory);
        battle.Do(new MoveItemCommand(unit.Id, grenade.Id, Inventory.RightHand));
        Assert.Same(unit, grenade.Owner);
        Assert.Equal(60 - 2 - 8, unit.Tu);
    }

    [Fact]
    public void An_occupied_hand_refuses_another_item_without_cost()
    {
        Battle battle = TestWorld.Battle(rules: InventoryRules());
        BattleUnit unit = battle.Soldier(Faction.Player, 5, 5, weapon: "rifle");
        BattleItem grenade = battle.Give(unit, "grenade", "belt");
        Assert.Equal("error.no_room", battle.Execute(new MoveItemCommand(unit.Id, grenade.Id, Inventory.RightHand)).ReasonKey);
        Assert.Equal("belt", grenade.Slot);
        Assert.Equal(60, unit.Tu);
    }

    private static Game.Core.Content.Ruleset InventoryRules()
    {
        Game.Core.Content.Ruleset rules = TestWorld.Rules();
        var slots = new Dictionary<string, InventorySlotDefinition>
        {
            [Inventory.RightHand] = new() { Id = Inventory.RightHand, Kind = InventorySlotKind.Hand, Costs = new Dictionary<string, int> { [Inventory.Ground] = 2, [Inventory.LeftHand] = 4, ["belt"] = 8 } },
            [Inventory.LeftHand] = new() { Id = Inventory.LeftHand, Kind = InventorySlotKind.Hand, Costs = new Dictionary<string, int> { [Inventory.Ground] = 2, [Inventory.RightHand] = 4, ["belt"] = 8 } },
            ["belt"] = new() { Id = "belt", Kind = InventorySlotKind.Grid, Width = 4, Height = 1, Costs = new Dictionary<string, int> { [Inventory.Ground] = 6, [Inventory.RightHand] = 4, [Inventory.LeftHand] = 4 } },
            [Inventory.Ground] = new() { Id = Inventory.Ground, Kind = InventorySlotKind.Ground, Costs = new Dictionary<string, int> { [Inventory.RightHand] = 8, [Inventory.LeftHand] = 8, ["belt"] = 12 } },
        };
        return new Game.Core.Content.Ruleset
        {
            Stats = rules.Stats, DamageTypes = rules.DamageTypes, Shapes = rules.Shapes, TileParts = rules.TileParts,
            Items = rules.Items, Armors = rules.Armors, Units = rules.Units, InventorySlots = slots, Battle = rules.Battle,
        };
    }
}
