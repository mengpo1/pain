using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class GameplayTests
{
    [Fact]
    public void Player_death_ends_the_level()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).Build());

        world.Combat.ApplyHit(world.Player, new Hit(100f, "bullet", false, 0f, Vector2.UnitX, 50f, null, HitMethod.Firearm));
        world.Step(Dt, default);

        Assert.Equal(WorldOutcome.Defeat, world.Outcome);
        Assert.NotNull(Event<PlayerDied>(world));
    }

    [Fact]
    public void Restarting_rebuilds_the_initial_state()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "pistol")
            .With(TestContent.Enemy("dummy", 10, 2)).WithWeapon("bat", 5, 3).Build();
        DefinitionSet definitions = Definitions();
        GameWorld first = GameWorld.Create(level, definitions);
        Run(first, 0.5f, Aim(first, TileCenter(10, 2), attack: true));
        Run(first, 0.5f, new PlayerCommands { Move = Vector2.UnitY });
        Assert.False(first.Actors.First(a => !a.IsPlayer).IsAlive);

        GameWorld restarted = GameWorld.Create(level, definitions);

        Assert.True(restarted.Actors.All(a => a.IsAlive));
        Assert.Equal(TileCenter(2, 2), restarted.Player.Position);
        Assert.Equal(3, restarted.Player.Weapon!.Ammo);
        Assert.Single(restarted.Weapons);
        Assert.Equal(WorldOutcome.Playing, restarted.Outcome);
        Assert.Equal(0f, restarted.Time);
    }

    [Fact]
    public void Restart_is_fast()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(80, 60), new Vector2(2, 2))
            .With(Enumerable.Range(0, 40).Select(i => TestContent.Enemy("thug", 10 + i % 20 * 3, 10 + i / 20 * 20)).ToArray()).Build();
        DefinitionSet definitions = Definitions();
        GameWorld.Create(level, definitions);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10; i++) GameWorld.Create(level, definitions);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds / 10.0 < 50, $"{watch.ElapsedMilliseconds / 10.0} ms per restart");
    }

    [Fact]
    public void Objectives_complete_in_order_then_the_level_is_won()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "revolver")
            .With(TestContent.Enemy("dummy", 10, 2))
            .WithZone("exit", 17, 1, 2, 3)
            .WithObjective(ObjectiveType.EliminateAll)
            .WithObjective(ObjectiveType.ReachZone, zone: "exit")
            .Build();
        GameWorld world = World(level);

        // Walking to the exit first does nothing: enemies remain.
        Assert.Equal(ObjectiveType.EliminateAll, world.Objectives.Current!.Type);

        Run(world, 0.1f, Aim(world, TileCenter(10, 2), attack: true));
        Assert.Equal(1, world.Objectives.CurrentIndex);
        Assert.Equal(WorldOutcome.Playing, world.Outcome);

        Run(world, 4f, new PlayerCommands { Move = Vector2.UnitX });
        Assert.Equal(WorldOutcome.Victory, world.Outcome);
        Assert.NotNull(Event<LevelCompleted>(world));
    }

    [Fact]
    public void Knocked_down_enemies_still_count_as_remaining()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .With(TestContent.Enemy("dummy", 2.8f, 2))
            .WithObjective(ObjectiveType.EliminateAll)
            .Build();
        GameWorld world = World(level);

        Run(world, 0.2f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.Equal(ActorState.KnockedDown, world.Actors.First(a => !a.IsPlayer).State);
        Assert.Equal(WorldOutcome.Playing, world.Outcome);
    }

    [Fact]
    public void Survive_objective_needs_time()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).WithObjective(ObjectiveType.Survive, duration: 2f).Build());

        Run(world, 1.5f);
        Assert.Equal(WorldOutcome.Playing, world.Outcome);

        Run(world, 0.6f);
        Assert.Equal(WorldOutcome.Victory, world.Outcome);
    }

    [Fact]
    public void Collect_objective_completes_when_the_item_is_picked_up()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .WithItem("briefcase", 5, 2).WithObjective(ObjectiveType.Collect, target: "briefcase").Build());

        Run(world, 1.5f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.True(world.Items.Single().Collected);
        Assert.Equal(WorldOutcome.Victory, world.Outcome);
        Assert.NotNull(Event<ItemCollected>(world));
    }

    [Fact]
    public void Activate_objective_needs_the_interact_command()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .WithInteractable("terminal", 3, 2).WithObjective(ObjectiveType.Activate, target: "terminal").Build());

        Run(world, 0.2f);
        Assert.Equal(WorldOutcome.Playing, world.Outcome);

        Run(world, 0.1f, new PlayerCommands { InteractPressed = true });
        Assert.Equal(WorldOutcome.Victory, world.Outcome);
    }

    [Fact]
    public void Message_trigger_fires_once_when_entering_a_zone()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2))
            .WithZone("hall", 5, 1, 2, 3)
            .WithTrigger(TriggerCondition.EnterZone, "hall", new TriggerAction { Type = TriggerActionType.Message, TextKey = "hello" })
            .Build());

        Run(world, 1f, new PlayerCommands { Move = Vector2.UnitX });
        Run(world, 1f, new PlayerCommands { Move = -Vector2.UnitX });
        Run(world, 1f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.Single(world.Events.OfType<MessageRequested>());
    }

    [Fact]
    public void Quick_kills_build_a_combo()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 8), new Vector2(2, 3), playerWeapon: "revolver")
            .With(TestContent.Enemy("dummy", 10, 2), TestContent.Enemy("dummy", 10, 4)).Build());

        Run(world, 0.6f, Aim(world, TileCenter(10, 2), attack: true));
        Run(world, 0.6f, Aim(world, TileCenter(10, 4), attack: true));

        Assert.Equal(2, world.Score.Kills);
        Assert.Equal(2, world.Score.Combo);
        Assert.Equal(300 + 600, world.Score.Total);

        Run(world, 3.5f);
        Assert.Equal(0, world.Score.Combo);
    }

    [Fact]
    public void Score_can_be_disabled()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "revolver")
            .With(TestContent.Enemy("dummy", 10, 2)).Build(), Definitions(rules => rules.Score.Enabled = false));

        Run(world, 0.3f, Aim(world, TileCenter(10, 2), attack: true));

        Assert.Equal(1, world.Score.Kills);
        Assert.Equal(0, world.Score.Total);
    }

    [Fact]
    public void Enemy_vision_modifier_reduces_detection_range()
    {
        var definitions = Definitions();
        var shadow = new ModifierDefinition { Id = "shadow", EnemyVision = 0.5f };
        LevelDefinition level = new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("thug", 12, 2, facing: 180)).Build();

        GameWorld normal = World(level, definitions);
        GameWorld hidden = World(level, definitions, shadow);
        Run(normal, 0.05f);
        Run(hidden, 0.05f);

        Assert.Equal(AiState.Chase, normal.Actors.First(a => !a.IsPlayer).Brain!.State);
        Assert.NotEqual(AiState.Chase, hidden.Actors.First(a => !a.IsPlayer).Brain!.State);
    }

    [Fact]
    public void Simulation_with_many_enemies_runs_much_faster_than_real_time()
    {
        string[] map = Room(60, 40);
        // Pillars so the AI has to path around obstacles.
        for (int y = 6; y < 34; y += 6) map[y] = "#" + string.Concat(Enumerable.Range(1, 58).Select(x => x % 8 == 0 ? '#' : '.')) + "#";
        var actors = Enumerable.Range(0, 60)
            .Select(i => TestContent.Enemy(i % 2 == 0 ? "guard" : "thug", 10 + i % 15 * 3, 3 + i / 15 * 8))
            .ToArray();
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 20)).With(actors).Build(), Definitions(rules => rules.FriendlyFire.Projectiles = false));
        foreach (Actor enemy in world.Actors.Where(a => !a.IsPlayer)) enemy.Brain!.AlertTo(world.Player);
        // Keep the player alive so the AI keeps chasing and shooting.
        world.Player.Health = float.MaxValue;
        Run(world, 0.5f);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Run(world, 2f);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 1000, $"2 s of simulation took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Simulation_is_deterministic_for_a_given_seed()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(30, 10), new Vector2(2, 5), playerWeapon: "pistol")
            .With(TestContent.Enemy("guard", 20, 3), TestContent.Enemy("thug", 22, 7, behavior: AiBehavior.Wander)).Build();
        DefinitionSet definitions = Definitions();
        GameWorld a = GameWorld.Create(level, definitions, seed: 42);
        GameWorld b = GameWorld.Create(level, definitions, seed: 42);

        var commands = new PlayerCommands { Move = new Vector2(1, 0.3f), AimTarget = TileCenter(20, 3), AttackHeld = true, AttackPressed = true };
        Run(a, 3f, commands);
        Run(b, 3f, commands);

        Assert.Equal(a.Actors.Select(x => x.Position), b.Actors.Select(x => x.Position));
        Assert.Equal(a.Events.Count, b.Events.Count);
    }
}
