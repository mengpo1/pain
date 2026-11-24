using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Timing guarantees behind the game feel: no hidden delays, no double hits, no lock-ups.</summary>
public class ResponsivenessTests
{
    [Fact]
    public void A_melee_hit_lands_exactly_at_its_hit_delay()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(TestContent.Enemy("dummy", 3, 2)).Build());
        float hitDelay = world.Definitions.Weapon("bat").HitDelay;

        world.Step(Dt, Aim(world, TileCenter(6, 2), attack: true));
        float swingTime = world.Time;
        float? hitTime = null;
        for (int i = 0; i < 60 && hitTime is null; i++)
        {
            world.Step(Dt, new PlayerCommands { AimTarget = TileCenter(6, 2) });
            if (world.Events.OfType<ActorHit>().Any()) hitTime = world.Time;
        }

        Assert.NotNull(hitTime);
        Assert.InRange(hitTime!.Value - swingTime, hitDelay - Dt, hitDelay + Dt);
    }

    [Fact]
    public void One_swing_hits_once()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .With(TestContent.Enemy("brute", 2.9f, 2)).Build());

        Run(world, 0.25f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.Single(world.Events.OfType<MeleeSwing>());
        Assert.Single(world.Events.OfType<ActorHit>());
    }

    [Theory]
    [InlineData(15f)]
    [InlineData(4f)]
    [InlineData(8f)]
    public void A_cadence_of_whole_steps_is_kept_exactly(float fireRate)
    {
        DefinitionSet definitions = Definitions();
        WeaponDefinition gun = definitions.Weapons["railgun"];
        gun.FireRate = fireRate;
        gun.Automatic = true;
        GameWorld world = World(Level(Room(30, 5), new Vector2(2, 2), playerWeapon: "railgun"), definitions);
        int shots = 0;
        var hold = new PlayerCommands { AttackHeld = true, AttackPressed = true, AimTarget = TileCenter(28, 2) };
        for (int i = 0; i < 120; i++)
        {
            world.ClearEvents();
            world.Step(Dt, hold);
            hold.AttackPressed = false;
            shots += world.Events.OfType<ShotFired>().Count();
        }

        // 1/rate is a whole number of 1/120 s steps: exactly `rate` shots in one second, none lost to rounding.
        Assert.Equal((int)fireRate, shots);
    }

    [Fact]
    public void Stun_ends_exactly_on_time()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).With(TestContent.Enemy("dummy", 8, 2)).Build());
        Actor target = world.Actors[1];
        target.Stun(0.25f);
        int steps = 0;
        while (target.State == ActorState.Stunned && steps < 120)
        {
            world.Step(Dt, default);
            steps++;
        }

        Assert.InRange(steps * Dt, 0.25f - Dt, 0.25f + Dt);
    }

    [Fact]
    public void The_player_keeps_moving_while_attacking()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "bat").Build());
        Run(world, 0.2f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = TileCenter(19, 2) });
        float before = world.Player.Position.X;

        world.Step(Dt, new PlayerCommands { Move = Vector2.UnitX, AttackPressed = true, AimTarget = TileCenter(19, 2) });
        Assert.Equal(ActorState.Attacking, world.Player.State);
        Run(world, 0.15f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = TileCenter(19, 2) });

        float speed = (world.Player.Position.X - before) / (0.15f + Dt);
        Assert.True(speed > world.Player.Definition.Speed * 0.95f, $"speed {speed}");
    }

    [Fact]
    public void A_click_during_the_recovery_attacks_as_soon_as_possible()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat").Build());
        Run(world, 0.3f, Aim(world, TileCenter(6, 2), attack: true));
        float cooldownEnd = 1f / world.Definitions.Weapon("bat").FireRate;

        // Click 0.1 s early: the second swing starts on the first step it is allowed.
        Run(world, 0.2f, Aim(world, TileCenter(6, 2), attack: true));
        MeleeSwing[] swings = world.Events.OfType<MeleeSwing>().ToArray();
        Assert.Equal(2, swings.Length);
        Assert.True(world.Player.LastAttackTime <= cooldownEnd + 2 * Dt, $"second swing at {world.Player.LastAttackTime}");
    }

    [Fact]
    public void Combat_states_never_lock_a_character()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(TestContent.Enemy("dummy", 2.8f, 2)).Build());
        Actor target = world.Actors[1];

        // Swing, execute, and check the player is free again within a bounded time.
        Run(world, 0.05f, Aim(world, TileCenter(6, 2), attack: true));
        target.KnockDown(3f);
        Run(world, 0.05f, new PlayerCommands { InteractPressed = true, AimTarget = target.Position });
        Run(world, 1f);

        Assert.Equal(ActorState.Active, world.Player.State);
        Assert.Null(world.Player.ExecutionTarget);
    }

    [Fact]
    public void A_knocked_down_character_cannot_be_frozen_by_repeated_executions()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).With(TestContent.Enemy("dummy", 2.8f, 2)).Build(),
            Definitions(rules => rules.Execution.CancelByMoving = true));
        Actor target = world.Actors[1];
        target.KnockDown(1f);

        // Start and cancel an execution many times: the target always ends back in a valid state.
        for (int i = 0; i < 5; i++)
        {
            Run(world, 0.02f, new PlayerCommands { InteractPressed = true, AimTarget = target.Position });
            Run(world, 0.02f, new PlayerCommands { Move = Vector2.UnitY });
            Assert.True(target.State is ActorState.KnockedDown or ActorState.GettingUp or ActorState.Active or ActorState.BeingExecuted);
        }

        Run(world, 0.5f, new PlayerCommands { Move = -Vector2.UnitY });
        Assert.NotEqual(ActorState.BeingExecuted, target.State);
        Assert.Null(target.Executioner);
    }
}
