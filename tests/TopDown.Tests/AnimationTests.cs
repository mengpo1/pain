using System.Numerics;
using TopDown.Content;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.Presentation;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>
/// Which animation a character shows (ActorPose), how animation data is timed, and how the shipped sprite
/// sheets match their descriptions. None of this can change the simulation: poses only read it.
/// </summary>
public class AnimationTests
{
    // ------------------------------------------------------------------ frame timing

    private static AnimationDefinition Strip(bool loop) => new()
    {
        Texture = "t", FrameWidth = 8, FrameHeight = 8, Loop = loop,
        Durations = [0.1f, 0.05f, 0.2f],
        Events = new() { ["1"] = "impact" },
    };

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.099f, 0)]
    [InlineData(0.1f, 1)]
    [InlineData(0.149f, 1)]
    [InlineData(0.2f, 2)]
    [InlineData(5f, 2)]
    public void Frames_last_their_own_duration_and_a_one_shot_holds_its_last_frame(float time, int frame) =>
        Assert.Equal(frame, Strip(loop: false).FrameAt(time, 3));

    [Fact]
    public void A_loop_wraps_around_its_total_length() => Assert.Equal(1, Strip(loop: true).FrameAt(0.35f + 0.12f, 3));

    [Fact]
    public void The_impact_event_starts_where_its_frame_starts()
    {
        Assert.Equal(0.1f, Strip(loop: false).EventTime("impact", 3)!.Value, 4);
        Assert.Null(Strip(loop: false).EventTime("footstep", 3));
    }

    // ------------------------------------------------------------------ pose selection

    private static GameWorld Room(string? weapon = null) => World(Level(TestContent.Room(14, 9), new Vector2(3, 4), playerWeapon: weapon));

    [Fact]
    public void A_still_character_idles_with_its_weapon_family()
    {
        GameWorld world = Room("bat");
        Run(world, 0.1f);

        Pose pose = ActorPose.Of(world.Player, world);

        Assert.Equal("idle.club", pose.Keys[0]);
        Assert.Contains("idle", pose.Keys);
    }

    [Fact]
    public void Walking_plays_with_the_distance_walked()
    {
        GameWorld world = Room();
        Run(world, 0.4f, new PlayerCommands { Move = Vector2.UnitX });

        Pose pose = ActorPose.Of(world.Player, world);

        Assert.Equal("walk.unarmed", pose.Keys[0]);
        Assert.Equal(world.Player.DistanceWalked / ActorPose.WalkUnitsPerSecond, pose.Time, 4);
    }

    [Fact]
    public void An_attack_is_timed_from_the_moment_its_hit_lands()
    {
        GameWorld world = Room("bat");
        Run(world, 0.05f);
        WeaponDefinition bat = world.Definitions.Weapon("bat");

        Run(world, 0.02f, new PlayerCommands { AttackPressed = true, AttackHeld = true, AimTarget = world.Player.Position + new Vector2(50f, 0f) });
        Pose pose = ActorPose.Of(world.Player, world);

        Assert.Equal(ActorState.Attacking, world.Player.State);
        Assert.StartsWith("attack.club", pose.Keys[0]);
        Assert.True(pose.FromImpact);
        Assert.Equal(world.Player.AttackElapsed - bat.HitDelay, pose.Time, 4);
    }

    [Theory]
    [InlineData("bullet", "death.shot")]
    [InlineData("sharp", "death.cut")]
    [InlineData("blunt", "death.blunt")]
    public void A_death_is_drawn_from_the_damage_that_caused_it(string damageType, string key)
    {
        (GameWorld world, Actor enemy) = WithEnemy();
        world.Combat.ApplyHit(enemy, new Hit(1000f, damageType, false, 0f, Vector2.UnitX, 0f, null, HitMethod.Melee));

        Pose pose = ActorPose.Of(enemy, world);

        Assert.Equal(ActorState.Dead, enemy.State);
        Assert.Equal(key, pose.Keys[0]);
        Assert.Equal(enemy.StateElapsed, pose.Time);
    }

    [Fact]
    public void A_body_finished_on_the_floor_does_not_fall_again()
    {
        (GameWorld world, Actor enemy) = WithEnemy();
        world.Combat.ApplyHit(enemy, new Hit(0f, "blunt", true, 2f, Vector2.UnitX, 0f, null, HitMethod.Melee));
        Assert.Equal(ActorState.KnockedDown, enemy.State);
        Assert.Equal(["knockdown"], ActorPose.Of(enemy, world).Keys);

        world.Combat.ApplyHit(enemy, new Hit(1000f, "blunt", false, 0f, Vector2.UnitX, 0f, null, HitMethod.Melee));

        Assert.Equal(ActorPose.Settled, ActorPose.Of(enemy, world).Time);
    }

    [Fact]
    public void A_waking_creature_plays_its_awakening()
    {
        DefinitionSet definitions = Definitions();
        ActorDefinition sleeper = definitions.Actors["brawler"];
        sleeper.Ai.Behavior = AiBehavior.Ambush;
        sleeper.Ai.WakeRadius = 200f;
        sleeper.Ai.WakeTime = 0.8f;
        LevelDefinition level = Level(TestContent.Room(14, 9), new Vector2(3, 4));
        level.Actors.Add(Enemy("brawler", 6, 4, id: "sleeper"));
        GameWorld world = World(level, definitions);
        Actor actor = world.FindActor("sleeper")!;

        Run(world, 0.3f);

        Assert.Equal(AiState.Awakening, actor.Brain!.State);
        Assert.Equal("awaken", ActorPose.Of(actor, world).Keys[0]);
    }

    [Fact]
    public void Asking_for_poses_never_changes_the_game()
    {
        static GameWorld Fight()
        {
            LevelDefinition level = Level(TestContent.Room(14, 9), new Vector2(3, 4), playerWeapon: "bat");
            level.Actors.Add(Enemy("thug", 7, 4));
            return World(level);
        }

        GameWorld watched = Fight(), plain = Fight();
        var commands = new PlayerCommands { Move = Vector2.UnitX, AttackHeld = true, AimTarget = new Vector2(400f, 140f) };
        for (int i = 0; i < 240; i++)
        {
            commands.AttackPressed = i % 30 == 0;
            watched.Step(Dt, commands);
            plain.Step(Dt, commands);
            foreach (Actor actor in watched.Actors) ActorPose.Of(actor, watched);
        }

        Assert.Equal(plain.Actors.Select(a => (a.Position, a.State, a.Health)), watched.Actors.Select(a => (a.Position, a.State, a.Health)));
    }

    /// <summary>A room with an idle enemy far enough not to react.</summary>
    private static (GameWorld, Actor) WithEnemy()
    {
        LevelDefinition level = Level(TestContent.Room(14, 9), new Vector2(2, 4));
        level.Actors.Add(Enemy("thug", 11, 4, id: "target", behavior: AiBehavior.Dormant));
        GameWorld world = World(level);
        return (world, world.FindActor("target")!);
    }
}
