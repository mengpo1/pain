using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Finishing moves: eligibility, sequences per weapon, reservation, interruptions.</summary>
public class ExecutionTests
{
    private static GameWorld Arena(string? playerWeapon = null, DefinitionSet? definitions = null, params LevelActor[] actors) =>
        World(new LevelDefinitionBuilder(Room(14, 7), new Vector2(3, 3), playerWeapon: playerWeapon).With(actors).Build(), definitions);

    private static PlayerCommands Press(bool interact = false, bool attack = false) =>
        new() { InteractPressed = interact, AttackPressed = attack, AimTarget = TileCenter(10, 3) };

    [Fact]
    public void A_knocked_down_enemy_can_be_executed_with_fists()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        Actor target = world.Actors[1];
        target.KnockDown(3f);

        Run(world, 0.02f, Press(interact: true));
        Assert.Equal(ActorState.Executing, world.Player.State);
        Assert.Equal(ActorState.BeingExecuted, target.State);
        Assert.Same(world.Player, target.Executioner);

        // Each attack press is one blow; only the third kills.
        Run(world, 0.13f, Press(attack: true));
        Run(world, 0.13f, Press(attack: true));
        Assert.True(target.IsAlive);
        Assert.Equal(2, world.Events.OfType<ExecutionStrike>().Count());
        Run(world, 0.13f, Press(attack: true));

        Assert.False(target.IsAlive);
        ExecutionStrike final = world.Events.OfType<ExecutionStrike>().Last();
        Assert.True(final.Final);
        Assert.Equal(3, final.Index);
    }

    [Fact]
    public void Fists_execution_never_stalls_without_input()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        world.Actors[1].KnockDown(5f);

        Run(world, 0.02f, Press(interact: true));
        Run(world, 2f);

        Assert.False(world.Actors[1].IsAlive);
        Assert.Equal(ActorState.Active, world.Player.State);
    }

    [Fact]
    public void A_bat_finishes_in_one_heavy_blow()
    {
        GameWorld world = Arena("bat", actors: TestContent.Enemy("dummy", 3.8f, 3));
        Actor target = world.Actors[1];
        target.KnockDown(3f);

        Run(world, 0.02f, Press(interact: true));
        Run(world, 0.2f);

        Assert.False(target.IsAlive);
        ExecutionStrike strike = Assert.Single(world.Events.OfType<ExecutionStrike>());
        Assert.True(strike.Final);
        Assert.Equal("bat", strike.Weapon.Id);
    }

    [Fact]
    public void A_weapon_with_several_blows_follows_its_profile()
    {
        DefinitionSet definitions = Definitions();
        definitions.Weapons["bat"].Execution = new ExecutionDefinition { Strikes = 3, WindUp = 0.05f, StrikeInterval = 0.1f, Recovery = 0.05f };
        GameWorld world = Arena("bat", definitions, TestContent.Enemy("dummy", 3.8f, 3));
        world.Actors[1].KnockDown(3f);

        Run(world, 0.02f, Press(interact: true));
        Run(world, 0.5f);

        Assert.Equal(3, world.Events.OfType<ExecutionStrike>().Count());
        Assert.False(world.Actors[1].IsAlive);
    }

    [Fact]
    public void A_gun_executes_with_a_point_blank_shot()
    {
        GameWorld world = Arena("pistol", actors: TestContent.Enemy("dummy", 3.8f, 3));
        world.Actors[1].KnockDown(3f);

        Run(world, 0.02f, Press(interact: true));
        Run(world, 0.3f);

        Assert.False(world.Actors[1].IsAlive);
        Assert.True(Assert.Single(world.Events.OfType<ExecutionStrike>()).Shot);
        Assert.Equal(2, world.Player.Weapon!.Ammo);
        Assert.Contains(world.Events, e => e is NoiseMade);
    }

    [Fact]
    public void An_empty_gun_is_used_as_a_club()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(14, 7), new Vector2(3, 3)).WithWeapon("pistol", 3.3f, 3, ammo: 0)
            .With(TestContent.Enemy("dummy", 3.8f, 3)).Build());
        Run(world, 0.05f, new PlayerCommands { PickupOrThrowPressed = true });
        Assert.True(world.Player.Weapon!.IsEmpty);
        world.Actors[1].KnockDown(3f);

        Run(world, 0.02f, Press(interact: true));
        Run(world, 0.5f);

        Assert.False(world.Actors[1].IsAlive);
        Assert.All(world.Events.OfType<ExecutionStrike>(), strike => Assert.False(strike.Shot));
        Assert.Equal(2, world.Events.OfType<ExecutionStrike>().Count());
    }

    [Fact]
    public void A_dead_character_cannot_be_executed()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        Actor target = world.Actors[1];
        world.Combat.ApplyHit(target, new Hit(100f, "bullet", false, 0f, Vector2.UnitX, 0f, world.Player, HitMethod.Firearm));

        Assert.Null(world.Combat.FindExecutionTarget(world.Player));
        Run(world, 0.05f, Press(interact: true));
        Assert.NotEqual(ActorState.Executing, world.Player.State);
    }

    [Fact]
    public void Targets_out_of_reach_cannot_be_executed()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 5f, 3));
        world.Actors[1].KnockDown(3f);

        Assert.Null(world.Combat.FindExecutionTarget(world.Player));
    }

    [Fact]
    public void A_wall_between_prevents_an_execution()
    {
        string[] map =
        [
            "#######",
            "#..#..#",
            "#..#..#",
            "#######",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2.2f, 1)).With(TestContent.Enemy("dummy", 3.8f, 1)).Build());
        world.Actors[1].KnockDown(3f);

        Assert.Null(world.Combat.FindExecutionTarget(world.Player));
    }

    [Fact]
    public void A_stunned_enemy_recovers_after_its_stun()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 8f, 3));
        Actor target = world.Actors[1];
        target.Stun(0.3f);
        Assert.Equal(ActorState.Stunned, target.State);

        Run(world, 0.25f);
        Assert.Equal(ActorState.Stunned, target.State);
        Run(world, 0.1f);
        Assert.NotEqual(ActorState.Stunned, target.State);
        Assert.True(target.IsAlive);
    }

    [Fact]
    public void Dazed_standing_enemies_are_executable_only_when_the_rules_allow_it()
    {
        GameWorld strict = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        strict.Actors[1].Stun(1f);
        Assert.Null(strict.Combat.FindExecutionTarget(strict.Player));

        GameWorld lenient = Arena(definitions: Definitions(rules => rules.Execution.AllowOnStunned = true), actors: TestContent.Enemy("dummy", 3.8f, 3));
        lenient.Actors[1].Stun(1f);
        Assert.Same(lenient.Actors[1], lenient.Combat.FindExecutionTarget(lenient.Player));
    }

    [Fact]
    public void A_hit_on_a_dazed_enemy_knocks_it_down()
    {
        GameWorld world = Arena("bat", actors: TestContent.Enemy("brute", 8f, 3));
        Actor brute = world.Actors[1];
        Assert.True(brute.Definition.KnockdownImmune);

        // Immune to knockdowns: a punch only staggers.
        world.Combat.ApplyHit(brute, new Hit(0f, "unarmed", true, 2f, Vector2.UnitX, 100f, world.Player, HitMethod.Melee));
        Assert.Equal(ActorState.Stunned, brute.State);

        var definitions = Definitions();
        GameWorld other = Arena(definitions: definitions, actors: TestContent.Enemy("dummy", 8f, 3));
        Actor dummy = other.Actors[1];
        dummy.Stun(1f);
        other.Combat.ApplyHit(dummy, new Hit(0f, "unarmed", false, 0f, Vector2.UnitX, 100f, other.Player, HitMethod.Melee));
        Assert.Equal(ActorState.KnockedDown, dummy.State);
    }

    [Fact]
    public void An_execution_interrupts_getting_up()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        Actor target = world.Actors[1];
        target.KnockDown(0.05f);
        Run(world, 0.1f);
        Assert.Equal(ActorState.GettingUp, target.State);

        Run(world, 0.02f, Press(interact: true));
        Assert.Equal(ActorState.BeingExecuted, target.State);

        // Longer than the get-up duration: it stays pinned until the end.
        Run(world, 1.5f);
        Assert.False(target.IsAlive);
    }

    [Fact]
    public void An_interrupted_execution_releases_its_target()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        Actor target = world.Actors[1];
        target.KnockDown(3f);
        Run(world, 0.02f, Press(interact: true));
        Assert.Equal(ActorState.BeingExecuted, target.State);

        // The executioner is knocked down mid-execution.
        world.Combat.ApplyHit(world.Player, new Hit(0f, "door", true, 2f, Vector2.UnitY, 50f, null, HitMethod.Door));

        Assert.Equal(ActorState.KnockedDown, target.State);
        Assert.Null(target.Executioner);
        Assert.True(target.IsAlive);
    }

    [Fact]
    public void Two_executioners_cannot_share_a_target_and_it_dies_only_once()
    {
        DefinitionSet definitions = Definitions();
        GameWorld world = World(new LevelDefinitionBuilder(Room(14, 7), new Vector2(3, 3)).Build(), definitions);
        Actor first = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4.3f, 3), MathF.PI);
        Actor second = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(3, 2.4f), MathF.PI / 2f);
        world.Player.KnockDown(5f);

        Assert.True(world.Combat.TryExecute(first));
        Assert.False(world.Combat.TryExecute(second));
        Assert.Same(first, world.Player.Executioner);

        Run(world, 1.5f);
        Assert.Single(world.Events.OfType<ActorKilled>(), k => k.Victim == world.Player);
        Assert.Same(first, world.Events.OfType<ActorKilled>().Single().Killer);
    }

    [Fact]
    public void Target_selection_is_deterministic_nearest_then_facing()
    {
        GameWorld world = Arena(actors: [TestContent.Enemy("dummy", 3.8f, 3, id: "front"), TestContent.Enemy("dummy", 2.2f, 3, id: "behind")]);
        foreach (Actor actor in world.Actors.Where(a => !a.IsPlayer)) actor.KnockDown(5f);
        world.Player.Facing = 0f;

        Assert.Equal("front", world.Combat.FindExecutionTarget(world.Player)!.LevelId);
        world.Player.Facing = MathF.PI;
        Assert.Equal("behind", world.Combat.FindExecutionTarget(world.Player)!.LevelId);
    }

    [Fact]
    public void Several_characters_on_the_floor_stay_consistent()
    {
        GameWorld world = Arena("bat", actors: [
            TestContent.Enemy("dummy", 3.8f, 3, id: "a"),
            TestContent.Enemy("dummy", 3, 3.8f, id: "b"),
            TestContent.Enemy("dummy", 3, 2.2f, id: "c")]);
        foreach (Actor actor in world.Actors.Where(a => !a.IsPlayer)) actor.KnockDown(3f);

        Run(world, 0.02f, Press(interact: true));
        Run(world, 0.4f);

        Assert.Single(world.Actors, a => !a.IsAlive);
        Assert.All(world.Actors.Where(a => !a.IsPlayer && a.IsAlive), a =>
        {
            Assert.Equal(ActorState.KnockedDown, a.State);
            Assert.Null(a.Executioner);
        });

        // The next ones can be finished one after the other (stepping back between them, as the
        // executioner moves onto each victim).
        for (int i = 0; i < 2; i++)
        {
            world.Player.Position = TileCenter(3, 3);
            Run(world, 0.02f, Press(interact: true));
            Run(world, 0.4f);
        }
        Assert.All(world.Actors.Where(a => !a.IsPlayer), a => Assert.False(a.IsAlive));
        Assert.Equal(3, world.Events.OfType<ActorKilled>().Count());
    }

    [Fact]
    public void Execution_immune_enemies_are_never_targeted()
    {
        DefinitionSet definitions = Definitions();
        definitions.Actors["dummy"].DamageMultipliers["execution"] = 0f;
        GameWorld world = Arena(definitions: definitions, actors: TestContent.Enemy("dummy", 3.8f, 3));
        world.Actors[1].KnockDown(3f);

        Assert.Null(world.Combat.FindExecutionTarget(world.Player));
    }

    [Fact]
    public void Executions_can_be_disabled_by_the_rules()
    {
        GameWorld world = Arena(definitions: Definitions(rules => rules.Execution.Enabled = false), actors: TestContent.Enemy("dummy", 3.8f, 3));
        world.Actors[1].KnockDown(3f);

        Run(world, 0.05f, Press(interact: true));

        Assert.NotEqual(ActorState.Executing, world.Player.State);
    }

    [Fact]
    public void Shooting_people_on_the_floor_is_configurable_separately()
    {
        GameWorld allowed = Arena("revolver", actors: TestContent.Enemy("dummy", 9f, 3));
        allowed.Actors[1].KnockDown(5f);
        Run(allowed, 0.1f, Aim(allowed, TileCenter(9, 3), attack: true));
        Assert.False(allowed.Actors[1].IsAlive);

        GameWorld forbidden = Arena("revolver", Definitions(rules => rules.Combat.BulletsHitDowned = false), TestContent.Enemy("dummy", 9f, 3));
        forbidden.Actors[1].KnockDown(5f);
        Run(forbidden, 0.1f, Aim(forbidden, TileCenter(9, 3), attack: true));
        Assert.True(forbidden.Actors[1].IsAlive);
    }

    [Fact]
    public void Space_pressed_just_before_the_enemy_falls_still_executes()
    {
        GameWorld world = Arena(actors: TestContent.Enemy("dummy", 3.8f, 3));
        Actor target = world.Actors[1];

        // Space a few steps before the punch lands: the press is kept briefly.
        Run(world, 0.02f, Press(interact: true, attack: true));
        Run(world, 0.1f, Press());

        Assert.Equal(ActorState.BeingExecuted, target.State);
    }
}
