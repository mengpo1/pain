using Game.Core.Battle;
using Game.Core.Battle.Ai;
using Game.Core.Battle.Combat;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Tests;

public class ReactionFireTests
{
    /// <summary>A hostile guard watching a corridor, a soldier walking across its view.</summary>
    private static (Battle Battle, BattleUnit Walker, BattleUnit Guard) Corridor(int guardReactions, int walkerReactions)
    {
        Battle battle = TestWorld.Battle(width: 20, length: 10);
        BattleUnit walker = battle.Soldier(Faction.Player, 2, 8, direction: 0, weapon: "rifle");
        BattleUnit guard = battle.Soldier(Faction.Hostile, 2, 1, direction: 4, weapon: "rifle");
        guard.Stats[StatIds.Reactions] = guardReactions;
        walker.Stats[StatIds.Reactions] = walkerReactions;
        battle.Start();
        return (battle, walker, guard);
    }

    [Fact]
    public void Faster_enemy_interrupts_a_walk_with_a_snap_shot()
    {
        (Battle battle, BattleUnit walker, BattleUnit guard) = Corridor(guardReactions: 90, walkerReactions: 30);
        battle.Do(new MoveCommand(walker.Id, new Position(2, 4, 0)));
        Assert.Contains(battle.Events, e => e is ReactionTriggered r && r.ReactorId == guard.Id);
        Assert.Contains(battle.Events, e => e is ShotFired { Reaction: true, Mode: AttackMode.Snap });
        Assert.True(guard.Tu < 60);
    }

    [Fact]
    public void Slower_enemy_does_not_react()
    {
        (Battle battle, BattleUnit walker, _) = Corridor(guardReactions: 20, walkerReactions: 90);
        battle.Do(new MoveCommand(walker.Id, new Position(2, 4, 0)));
        Assert.DoesNotContain(battle.Events, e => e is ReactionTriggered);
    }

    [Fact]
    public void Reaction_score_is_reactions_times_remaining_tu_ratio()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit unit = battle.Soldier(Faction.Player, 1, 1);
        unit.Tu = 30;
        Assert.Equal(25.0, CombatRules.ReactionScore(unit));
    }

    [Fact]
    public void A_reactor_fires_again_while_its_score_stays_higher()
    {
        (Battle battle, BattleUnit walker, BattleUnit guard) = Corridor(guardReactions: 100, walkerReactions: 10);
        walker.Stats[StatIds.Health] = 1000; // survive the shots
        walker.Health = 1000;
        battle.Do(new KneelCommand(walker.Id));
        // Guard score 100 vs walker 10 × 56/60 ≈ 9.3: each snap (15 TU) lowers the guard's score
        // by 25, so it fires 4 times on this single trigger.
        Assert.Equal(4, battle.Events.Count(e => e is ReactionTriggered));
        Assert.Equal(0, guard.Tu);
    }
}

public class TurnAndObjectiveTests
{
    [Fact]
    public void Sides_play_in_order_and_tu_come_back_at_the_start_of_their_turn()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit soldier = battle.Soldier(Faction.Player, 1, 1);
        battle.Soldier(Faction.Hostile, 18, 18);
        battle.Start();
        battle.Do(new MoveCommand(soldier.Id, new Position(3, 1, 0)));
        Assert.Equal(52, soldier.Tu);

        battle.Do(new EndTurnCommand());
        Assert.Equal(Faction.Hostile, battle.CurrentFaction);
        Assert.Equal(52, soldier.Tu); // TU left serve for reaction fire during the enemy turn
        battle.Do(new EndTurnCommand());
        Assert.Equal(Faction.Neutral, battle.CurrentFaction);
        battle.Do(new EndTurnCommand());
        Assert.Equal(Faction.Player, battle.CurrentFaction);
        Assert.Equal(2, battle.Turn);
        Assert.Equal(60, soldier.Tu);
    }

    [Fact]
    public void Commands_for_the_other_side_are_refused()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit alien = battle.Soldier(Faction.Hostile, 5, 5);
        battle.Soldier(Faction.Player, 1, 1);
        battle.Start();
        Assert.Equal("error.not_your_turn", battle.Execute(new MoveCommand(alien.Id, new Position(6, 5, 0))).ReasonKey);
    }

    [Fact]
    public void Fatal_wounds_bleed_one_health_each_per_turn()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit soldier = battle.Soldier(Faction.Player, 1, 1);
        battle.Soldier(Faction.Hostile, 18, 18);
        battle.Start();
        soldier.AddWound(BodyPart.Torso, 2);
        for (int i = 0; i < 3; i++) battle.Do(new EndTurnCommand());
        Assert.Equal(38, soldier.Health);
    }

    [Fact]
    public void Losing_every_unit_is_a_defeat()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit soldier = battle.Soldier(Faction.Player, 5, 5);
        BattleUnit alien = battle.Soldier(Faction.Hostile, 6, 5, direction: 6, weapon: "laser");
        battle.Start();
        battle.Do(new EndTurnCommand());
        battle.Do(new AttackCommand(alien.Id, alien.ItemInSlot(Inventory.RightHand)!.Id, AttackMode.Snap, soldier.Position));
        Assert.True(battle.IsOver);
        Assert.False(battle.Outcome!.Victory);
        Assert.Equal("mission.defeat.all_down", battle.Outcome.ReasonKey);
    }

    [Fact]
    public void Survive_objective_is_won_when_the_turn_after_the_last_one_begins()
    {
        Battle battle = TestWorld.Battle(mission: TestWorld.Mission(new ObjectiveDefinition { Kind = ObjectiveKind.Survive, Turns = 2 }));
        battle.Soldier(Faction.Player, 1, 1);
        battle.Soldier(Faction.Hostile, 18, 18);
        battle.Start();
        for (int i = 0; i < 3; i++) battle.Do(new EndTurnCommand());
        Assert.False(battle.IsOver);
        for (int i = 0; i < 3; i++) battle.Do(new EndTurnCommand());
        Assert.True(battle.Outcome!.Victory);
        Assert.Equal("mission.victory.survived", battle.Outcome.ReasonKey);
    }

    [Fact]
    public void Reach_zone_objective_is_won_when_enough_units_stand_in_the_zone()
    {
        Battle battle = TestWorld.Battle(mission: TestWorld.Mission(new ObjectiveDefinition { Kind = ObjectiveKind.ReachZone, Zone = "exit", Units = 1 }));
        battle.Map[6, 1, 0]!.Zone = "exit";
        BattleUnit soldier = battle.Soldier(Faction.Player, 1, 1);
        battle.Soldier(Faction.Hostile, 18, 18);
        battle.Start();
        battle.Do(new MoveCommand(soldier.Id, new Position(6, 1, 0)));
        Assert.True(battle.Outcome!.Victory);
    }

    [Fact]
    public void Turn_limit_ends_the_mission_in_defeat()
    {
        Battle battle = TestWorld.Battle(mission: TestWorld.Mission(turnLimit: 1));
        battle.Soldier(Faction.Player, 1, 1);
        battle.Soldier(Faction.Hostile, 18, 18);
        battle.Start();
        for (int i = 0; i < 3; i++) battle.Do(new EndTurnCommand());
        Assert.False(battle.Outcome!.Victory);
    }
}

public class AiTests
{
    private static void PlayAiTurn(Battle battle, AiController ai, int maxCommands = 200)
    {
        Faction side = battle.CurrentFaction;
        for (int i = 0; i < maxCommands && battle.CurrentFaction == side && !battle.IsOver; i++)
        {
            BattleCommand command = ai.NextCommand();
            CommandResult result = battle.Execute(command);
            if (!result.Accepted) ai.Refused(command);
            battle.RunUntilIdle();
        }
    }

    [Fact]
    public void Ai_shoots_an_enemy_it_sees()
    {
        Battle battle = TestWorld.Battle();
        battle.Soldier(Faction.Player, 5, 5, direction: 6);
        BattleUnit alien = battle.Soldier(Faction.Hostile, 10, 5, direction: 6, weapon: "rifle");
        battle.Start();
        battle.Do(new EndTurnCommand());
        PlayAiTurn(battle, new AiController(battle));
        Assert.Contains(battle.Events, e => e is ShotFired shot && shot.ShooterId == alien.Id);
    }

    [Fact]
    public void Ai_does_not_shoot_at_an_enemy_it_has_never_seen()
    {
        Battle battle = TestWorld.Battle(width: 30, length: 5);
        for (int y = 0; y < 5; y++) battle.Set(15, y, 0, TilePartSlot.WestWall, "wallW");
        battle.Soldier(Faction.Player, 3, 2, direction: 2);
        BattleUnit alien = battle.Soldier(Faction.Hostile, 25, 2, direction: 6, weapon: "rifle");
        battle.Start();
        battle.Do(new EndTurnCommand());
        PlayAiTurn(battle, new AiController(battle));
        Assert.DoesNotContain(battle.Events, e => e is ShotFired shot && shot.ShooterId == alien.Id);
        Assert.Equal(Faction.Neutral, battle.CurrentFaction);
    }

    [Fact]
    public void Ai_always_ends_its_turn()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit soldier = battle.Soldier(Faction.Player, 2, 2, direction: 2, weapon: "rifle");
        soldier.Stats[StatIds.Health] = 10_000; // the battle must not end before the AI is done
        soldier.Health = 10_000;
        for (int i = 0; i < 4; i++) battle.Soldier(Faction.Hostile, 15, 3 + i * 3, direction: 6, weapon: "rifle");
        battle.Start();
        battle.Do(new EndTurnCommand());
        PlayAiTurn(battle, new AiController(battle));
        Assert.NotEqual(Faction.Hostile, battle.CurrentFaction);
    }
}

public class MoraleTests
{
    /// <summary>A demoralised soldier facing an alien at the start of the second turn.</summary>
    private static (Battle Battle, BattleUnit Soldier) Shaken(ulong seed, int morale)
    {
        Battle battle = TestWorld.Battle(seed: seed);
        BattleUnit soldier = battle.Soldier(Faction.Player, 5, 5, direction: 2, weapon: "rifle");
        BattleUnit alien = battle.Soldier(Faction.Hostile, 12, 5, direction: 2);
        alien.Stats[StatIds.Health] = 10_000;
        alien.Health = 10_000;
        battle.Start();
        soldier.Morale = morale;
        for (int i = 0; i < 3; i++) battle.Do(new EndTurnCommand());
        return (battle, soldier);
    }

    [Fact]
    public void High_morale_never_breaks()
    {
        (Battle battle, BattleUnit soldier) = Shaken(1, 50);
        Assert.DoesNotContain(battle.Events, e => e is UnitPanicked);
        Assert.Equal(60, soldier.Tu);
    }

    [Fact]
    public void Broken_units_panic_or_go_berserk_then_rest_with_no_tu()
    {
        var states = new HashSet<MoraleState>();
        for (ulong seed = 1; seed <= 30; seed++)
        {
            (Battle battle, BattleUnit soldier) = Shaken(seed, 0);
            UnitPanicked panic = Assert.Single(battle.Events.OfType<UnitPanicked>(), e => e.UnitId == soldier.Id);
            states.Add(panic.State);
            Assert.Equal(0, soldier.Tu);
            Assert.Equal(15, soldier.Morale);
            Assert.Equal(MoraleState.None, soldier.MoraleState);
            if (panic.State == MoraleState.Berserk) Assert.Contains(battle.Events, e => e is ShotFired shot && shot.ShooterId == soldier.Id);
        }

        Assert.Contains(MoraleState.Berserk, states);
        Assert.Contains(MoraleState.Panicking, states);
    }

    [Fact]
    public void Morale_loss_from_a_death_depends_on_bravery()
    {
        Battle battle = TestWorld.Battle();
        BattleUnit brave = battle.Soldier(Faction.Player, 1, 1);
        BattleUnit timid = battle.Soldier(Faction.Player, 2, 1);
        BattleUnit victim = battle.Soldier(Faction.Player, 3, 1);
        brave.Stats[StatIds.Bravery] = 100;
        timid.Stats[StatIds.Bravery] = 10;
        Game.Core.Battle.Combat.DamageRules.Damage(battle, null, victim, 0, -6, 10, 1000, battle.Rules.DamageTypes["flat"]);
        Assert.Equal(100 - 2, brave.Morale);   // (110 − 100) × 10 / 100 = 1 → 2
        Assert.Equal(100 - 20, timid.Morale);  // (110 − 10) × 10 / 100 = 10 → 20
    }
}
