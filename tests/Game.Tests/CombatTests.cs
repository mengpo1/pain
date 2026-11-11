using Game.Core.Battle;
using Game.Core.Battle.Combat;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Tests;

public class CombatTests
{
    [Fact]
    public void Attack_cost_is_a_percentage_of_max_tu_or_flat()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 1, 1);
        ItemDefinition rifle = battle.Rules.Items["rifle"];
        Assert.Equal(15, CombatRules.AttackTu(unit, rifle, AttackMode.Snap));  // 25 % of 60
        Assert.Equal(48, CombatRules.AttackTu(unit, rifle, AttackMode.Aimed)); // 80 % of 60
        Assert.Equal(21, CombatRules.AttackTu(unit, rifle, AttackMode.Auto));  // 35 % of 60
        Assert.Equal(10, CombatRules.AttackTu(unit, battle.Rules.Items["laser"], AttackMode.Snap));
        Assert.Equal(0, CombatRules.AttackTu(unit, battle.Rules.Items["laser"], AttackMode.Auto));
    }

    [Fact]
    public void Accuracy_combines_skill_weapon_kneeling_hands_and_health_with_integer_steps()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 1, 1);
        BattleItem rifle = battle.Give(unit, "rifle", Inventory.RightHand);
        Assert.Equal(36, CombatRules.Accuracy(battle, unit, rifle, AttackMode.Snap)); // 60 × 60 / 100

        unit.Kneeling = true;
        Assert.Equal(41, CombatRules.Accuracy(battle, unit, rifle, AttackMode.Snap)); // 36 × 115 / 100

        battle.Give(unit, "knife", Inventory.LeftHand);
        Assert.Equal(32, CombatRules.Accuracy(battle, unit, rifle, AttackMode.Snap)); // 41 × 80 / 100

        unit.Health = 20; // half health: modifier 25 × 20 / 40 + 75 = 87
        Assert.Equal(32 * 87 / 100, CombatRules.Accuracy(battle, unit, rifle, AttackMode.Snap));
    }

    [Fact]
    public void Accuracy_drops_beyond_the_aimed_range()
    {
        var rules = new BattleRules();
        var weapon = new ItemDefinition { Range = new RangeDefinition { Aimed = 10, Dropoff = 5 } };
        Assert.Equal(0.60, CombatRules.AccuracyAtDistance(rules, weapon, AttackMode.Aimed, 60, 10 * 16), 6);
        Assert.Equal(0.50, CombatRules.AccuracyAtDistance(rules, weapon, AttackMode.Aimed, 60, 12 * 16), 6);
        // Snap range only matters with the extended option, as in OXCE.
        Assert.Equal(0.50, CombatRules.AccuracyAtDistance(rules, weapon, AttackMode.Snap, 60, 12 * 16), 6);
    }

    [Fact]
    public void Perfect_accuracy_keeps_the_shot_on_the_aimed_voxel()
    {
        var random = new GameRandom(3);
        var origin = new Voxel(8, 8, 20);
        var target = new Voxel(8 + 160, 8, 12);
        for (int i = 0; i < 50; i++)
        {
            Voxel shot = Ballistics.Deviate(origin, target, 1.2, random);
            Assert.InRange(shot.X, target.X - 1, target.X + 1);
            Assert.InRange(shot.Y, target.Y - 1, target.Y + 1);
        }
    }

    [Fact]
    public void Poor_accuracy_spreads_shots_more_along_y_than_along_x()
    {
        // OXCE's deliberate anisotropy: firing along X spreads half as much as firing along Y.
        var random = new GameRandom(5);
        var origin = new Voxel(0, 0, 20);
        double alongX = 0, alongY = 0;
        for (int i = 0; i < 400; i++)
        {
            alongX += Math.Abs(Ballistics.Deviate(origin, new Voxel(160, 0, 20), 0.2, random).Y);
            alongY += Math.Abs(Ballistics.Deviate(origin, new Voxel(0, 160, 20), 0.2, random).X);
        }

        Assert.True(alongY > alongX * 1.5, $"spread along x {alongX}, along y {alongY}");
    }

    [Fact]
    public void Point_blank_aimed_shot_hits_and_spends_one_round()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit shooter = battle.Soldier(Faction.Player, 5, 5, direction: 2);
        BattleItem rifle = battle.Give(shooter, "rifle", Inventory.RightHand);
        shooter.Stats[StatIds.Firing] = 120;
        BattleUnit target = battle.Soldier(Faction.Hostile, 7, 5, direction: 6);
        battle.Start();

        battle.Do(new AttackCommand(shooter.Id, rifle.Id, AttackMode.Aimed, target.Position));
        Assert.Equal(19, rifle.LoadedAmmo!.Rounds);
        Assert.Equal(60 - 48, shooter.Tu);
        Assert.Contains(battle.Events, e => e is ProjectileHit hit && hit.UnitId == target.Id);
    }

    [Fact]
    public void Auto_fire_pays_once_and_spends_a_round_per_shot()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit shooter = battle.Soldier(Faction.Player, 5, 5, direction: 2);
        BattleItem rifle = battle.Give(shooter, "rifle", Inventory.RightHand);
        battle.Start();

        battle.Do(new AttackCommand(shooter.Id, rifle.Id, AttackMode.Auto, new Position(12, 5, 0)));
        Assert.Equal(17, rifle.LoadedAmmo!.Rounds);
        Assert.Equal(60 - 21, shooter.Tu);
        Assert.Equal(3, battle.Events.Count(e => e is ShotFired));
    }

    [Fact]
    public void Shot_through_a_wall_is_refused_without_cost()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(7, 5, 0, TilePartSlot.WestWall, "wallW");
        BattleUnit shooter = battle.Soldier(Faction.Player, 5, 5, direction: 2);
        BattleItem rifle = battle.Give(shooter, "rifle", Inventory.RightHand);
        battle.Start();
        CommandResult result = battle.Execute(new AttackCommand(shooter.Id, rifle.Id, AttackMode.Snap, new Position(9, 5, 0)));
        Assert.False(result.Accepted);
        Assert.Equal("error.no_line_of_fire", result.ReasonKey);
        Assert.Equal(60, shooter.Tu);
    }

    [Fact]
    public void Empty_weapon_cannot_fire_and_reloads_from_the_belt()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit shooter = battle.Soldier(Faction.Player, 5, 5);
        BattleItem rifle = battle.Give(shooter, "rifle", Inventory.RightHand);
        rifle.LoadedAmmo!.Rounds = 0;
        BattleItem clip = battle.CreateItem(battle.Rules.Items["clip"]);
        Assert.True(Inventory.TryPlace(battle, shooter, clip, "belt"));

        Assert.Equal("error.no_ammo", battle.Execute(new AttackCommand(shooter.Id, rifle.Id, AttackMode.Snap, new Position(9, 5, 0))).ReasonKey);
        battle.Do(new ReloadCommand(shooter.Id, rifle.Id));
        Assert.Same(clip, rifle.LoadedAmmo);
        Assert.Equal(45, shooter.Tu);
        Assert.DoesNotContain(clip, shooter.Inventory);
    }

    [Fact]
    public void Armour_of_the_side_hit_is_subtracted_then_worn_down()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit target = battle.Soldier(Faction.Hostile, 5, 5, direction: 0); // facing north
        var ap = new DamageTypeDefinition { Id = "ap2", Random = DamageRandom.Flat, ToStun = 0, ToWound = 0 };

        // Impact from the north, in front: front armour 5.
        int loss = DamageRules.Damage(battle, null, target, 0, -6, 10, 20, ap);
        Assert.Equal(15, loss);
        Assert.Equal(25, target.Health);
        Assert.Equal(5 - 2, target.ArmorValue(ArmorSide.Front)); // round(15 × 0.1) = 2
    }

    [Theory]
    [InlineData(0, -6, 0, ArmorSide.Front)]
    [InlineData(6, 0, 0, ArmorSide.Right)]
    [InlineData(-6, 0, 0, ArmorSide.Left)]
    [InlineData(0, 6, 0, ArmorSide.Rear)]
    public void Side_hit_depends_on_where_the_impact_comes_from(int rx, int ry, int facing, ArmorSide expected)
    {
        Assert.Equal(expected, DamageRules.SideHit(rx, ry, 10, facing, new GameRandom(1)));
    }

    [Fact]
    public void Impact_right_under_the_unit_hits_the_underside()
    {
        Assert.Equal(ArmorSide.Under, DamageRules.SideHit(0, 0, 0, 3, new GameRandom(1)));
    }

    [Fact]
    public void Lethal_hit_kills_drops_items_and_leaves_a_corpse()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit shooter = battle.Soldier(Faction.Player, 5, 5, direction: 2, weapon: "laser");
        BattleUnit target = battle.Soldier(Faction.Hostile, 6, 5, direction: 6, weapon: "rifle");
        battle.Start();
        BattleItem laser = shooter.ItemInSlot(Inventory.RightHand)!;

        battle.Do(new AttackCommand(shooter.Id, laser.Id, AttackMode.Snap, target.Position));
        Assert.Equal(UnitStatus.Dead, target.Status);
        Assert.Null(battle.Map[6, 5, 0]!.Unit);
        Assert.Contains(battle.Map[6, 5, 0]!.Items, i => i.Definition.Id == "corpse" && i.Body == target);
        Assert.Contains(battle.Map[6, 5, 0]!.Items, i => i.Definition.Id == "rifle");
        Assert.True(battle.IsOver);
        Assert.True(battle.Outcome!.Victory);
    }

    [Fact]
    public void Melee_needs_an_adjacent_target_and_hits_with_a_percentage_roll()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit attacker = battle.Soldier(Faction.Player, 5, 5, direction: 2, weapon: "knife");
        BattleUnit target = battle.Soldier(Faction.Hostile, 7, 5);
        battle.Start();
        BattleItem knife = attacker.ItemInSlot(Inventory.RightHand)!;
        Assert.Equal("error.out_of_reach", battle.Execute(new AttackCommand(attacker.Id, knife.Id, AttackMode.Melee, target.Position)).ReasonKey);

        attacker.Stats[StatIds.Melee] = 200; // accuracy 200 %: always hits
        battle.PlaceUnit(attacker, new Position(6, 5, 0));
        battle.Do(new AttackCommand(attacker.Id, knife.Id, AttackMode.Melee, target.Position));
        Assert.Equal(UnitStatus.Dead, target.Status);
    }

    [Fact]
    public void Destroying_a_wall_leaves_its_remains()
    {
        Battle battle = TestWorld.Battle();
        battle.Set(8, 5, 0, TilePartSlot.WestWall, "wallW");
        DamageRules.HitTilePart(battle, new Position(8, 5, 0), TilePartSlot.WestWall, 1000, 1000, battle.Rules.DamageTypes["ap"]);
        Assert.Equal("ruinW", battle.Map[8, 5, 0]!.WestWall!.Id);
        Assert.True(MovementRules(battle));
    }

    private static bool MovementRules(Battle battle) =>
        Game.Core.Battle.Movement.MovementRules.TryStep(battle.Map, battle.Rules.Battle, new Position(7, 5, 0), 2, out _);

    [Fact]
    public void Same_seed_same_battle()
    {
        static string Run(ulong seed)
        {
            Battle battle = TestWorld.Battle(seed: seed);
            BattleUnit shooter = battle.Soldier(Faction.Player, 2, 5, direction: 2);
            BattleItem rifle = battle.Give(shooter, "rifle", Inventory.RightHand);
            BattleUnit target = battle.Soldier(Faction.Hostile, 14, 5, direction: 6);
            battle.Start();
            battle.Do(new AttackCommand(shooter.Id, rifle.Id, AttackMode.Auto, target.Position));
            return string.Join(";", battle.Events.OfType<ShotFired>().Select(s => s.Impact)) + target.Health;
        }

        Assert.Equal(Run(42), Run(42));
    }
}
