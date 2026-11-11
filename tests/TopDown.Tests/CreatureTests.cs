using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Creature behaviours built from data: the leap attack and the ambush.</summary>
public class CreatureTests
{
    private static readonly LeapDefinition Leap = new()
    {
        MinRange = 40f, Range = 200f, WindUp = 0.4f, Speed = 600f, Distance = 240f, Recovery = 0.5f, Cooldown = 1f,
    };

    private static DefinitionSet Leapers()
    {
        DefinitionSet definitions = Definitions();
        ActorDefinition leaper = definitions.Actor("brawler");
        leaper.Ai.Leap = Leap;
        leaper.Ai.ReactionTime = 0.05f;
        return definitions;
    }

    private static GameWorld Room(DefinitionSet definitions, string[]? map = null, params LevelActor[] actors) =>
        World(new LevelDefinitionBuilder(map ?? TestContent.Room(20, 9), new Vector2(3, 4)).With(actors).Build(), definitions);

    private static void RunUntil(GameWorld world, Func<bool> done, float maxSeconds, PlayerCommands commands = default)
    {
        for (float t = 0f; t < maxSeconds && !done(); t += Dt) world.Step(Dt, commands);
    }

    [Fact]
    public void A_leaper_crouches_first_then_jumps_at_its_target()
    {
        GameWorld world = Room(Leapers(), actors: TestContent.Enemy("brawler", 8, 4, facing: 180));
        Actor leaper = world.Actors[1];

        RunUntil(world, () => leaper.LeapPhase == LeapPhase.Crouch, 2f);
        Assert.Single(world.Events.OfType<LeapStarted>());
        Vector2 crouchedAt = leaper.Position;

        // The crouch is the warning: it does not move.
        Run(world, 0.3f);
        Assert.Equal(LeapPhase.Crouch, leaper.LeapPhase);
        Assert.True(Vector2.Distance(crouchedAt, leaper.Position) < 1f);

        RunUntil(world, () => leaper.LeapPhase == LeapPhase.Airborne, 0.3f);
        Run(world, 0.05f);
        Assert.True(leaper.Velocity.Length() > Leap.Speed * 0.9f, "full speed at once");

        RunUntil(world, () => leaper.LeapPhase == LeapPhase.Landing, 0.6f);
        ActorHit hit = Assert.Single(world.Events.OfType<ActorHit>());
        Assert.True(hit.Target.IsPlayer);
        Assert.Same(leaper, hit.Source);
        Assert.True(Assert.Single(world.Events.OfType<LeapLanded>()).HitSomeone);
    }

    [Fact]
    public void A_quick_sidestep_after_the_launch_dodges_the_leap()
    {
        GameWorld world = Room(Leapers(), actors: TestContent.Enemy("brawler", 8, 4, facing: 180));
        Actor leaper = world.Actors[1];
        RunUntil(world, () => leaper.LeapPhase == LeapPhase.Airborne, 2f);

        RunUntil(world, () => leaper.LeapPhase == LeapPhase.Landing, 1f,
            new PlayerCommands { Move = Vector2.UnitY, AimTarget = leaper.Position });

        Assert.Empty(world.Events.OfType<ActorHit>());
        Assert.False(Assert.Single(world.Events.OfType<LeapLanded>()).HitSomeone);
    }

    [Fact]
    public void A_leap_is_stopped_by_walls()
    {
        string[] map =
        [
            "####################",
            "#..................#",
            "#..................#",
            "#..................#",
            "#.......#..........#",
            "#..................#",
            "####################",
        ];
        GameWorld world = Room(Leapers(), map, TestContent.Enemy("brawler", 11, 4, facing: 180));
        Actor leaper = world.Actors[1];
        leaper.Brain!.AlertTo(world.Player);
        float wallRight = 9f * 32f;

        // Forced through the wall (the AI itself only leaps with a clear way).
        Assert.True(world.Combat.TryLeap(leaper, world.Player));
        Run(world, 1f);

        Assert.True(leaper.Position.X - leaper.Radius >= wallRight - 0.5f, "never passes through the wall");
        Assert.Contains(world.Events.OfType<LeapLanded>(), landed => !landed.HitSomeone);
        Assert.Empty(world.Events.OfType<ActorHit>());
    }

    [Fact]
    public void A_leaper_never_hurts_its_own_kind_on_the_way()
    {
        GameWorld world = Room(Leapers(), actors:
        [
            TestContent.Enemy("brawler", 10, 4, facing: 180, id: "leaper"),
            TestContent.Enemy("dummy", 7, 4, id: "ally"),
        ]);
        Actor leaper = world.FindActor("leaper")!;
        Actor ally = world.FindActor("ally")!;
        world.Combat.TryLeap(leaper, world.Player);

        Run(world, 1.2f);

        Assert.DoesNotContain(world.Events.OfType<ActorHit>(), hit => hit.Target == ally);
        Assert.True(ally.IsAlive);
    }

    [Fact]
    public void A_creature_in_ambush_waits_still_until_the_player_comes_close()
    {
        GameWorld world = Room(Definitions(), actors: TestContent.Enemy("brawler", 14, 4, facing: 180, behavior: AiBehavior.Ambush));
        Actor watcher = world.Actors[1];
        Vector2 post = watcher.Position;

        // In plain view but far away: nothing.
        Run(world, 2f, new PlayerCommands { AimTarget = watcher.Position });
        Assert.Equal(AiState.Ambush, watcher.Brain!.State);
        Assert.Equal(post, watcher.Position);

        // Walk towards it: it wakes up and hunts.
        RunUntil(world, () => watcher.Brain.State == AiState.Chase, 4f,
            new PlayerCommands { Move = Vector2.UnitX, AimTarget = watcher.Position });
        Assert.Equal(AiState.Chase, watcher.Brain.State);
        Assert.True(Vector2.Distance(world.Player.Position, watcher.Position) <= watcher.Definition.Ai.WakeRadius + world.Player.Radius + 5f);
        Assert.Contains(world.Events.OfType<EnemyAlerted>(), alerted => alerted.Enemy == watcher);
    }

    [Fact]
    public void A_wall_hides_the_player_from_a_creature_in_ambush()
    {
        string[] map =
        [
            "##########",
            "#........#",
            "#...#....#",
            "#...#....#",
            "#...#....#",
            "##########",
        ];
        GameWorld world = Room(Definitions(), map, TestContent.Enemy("brawler", 5, 3, behavior: AiBehavior.Ambush));
        world.Player.Position = new Vector2(3.5f * 32f, 3.5f * 32f);

        Run(world, 1f);

        Assert.Equal(AiState.Ambush, world.Actors[1].Brain!.State);
    }

    [Fact]
    public void Only_a_noise_made_close_by_gives_the_ambush_away()
    {
        GameWorld world = Room(Definitions(), actors: TestContent.Enemy("brawler", 16, 4, behavior: AiBehavior.Ambush));
        Actor watcher = world.Actors[1];

        world.EmitNoise(world.Player.Position, 2000f, world.Player);
        Run(world, 0.1f);
        Assert.Equal(AiState.Ambush, watcher.Brain!.State);

        world.EmitNoise(watcher.Position - new Vector2(150f, 0f), 400f, world.Player);
        Run(world, 0.1f);
        Assert.Equal(AiState.Chase, watcher.Brain.State);
    }
}
