using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class AiTests
{
    private static Actor Enemy(GameWorld world) => world.Actors.First(a => !a.IsPlayer);

    [Fact]
    public void Enemy_detects_the_player_in_range_and_in_front()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("thug", 8, 2, facing: 180)).Build());

        Run(world, 0.05f);

        Assert.Equal(AiState.Chase, Enemy(world).Brain!.State);
        Assert.NotNull(Event<EnemyAlerted>(world));
    }

    [Fact]
    public void Enemy_does_not_see_through_walls()
    {
        string[] map =
        [
            "####################",
            "#......#...........#",
            "#......#...........#",
            "#......#...........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 2)).With(TestContent.Enemy("thug", 10, 2, facing: 180)).Build());

        Run(world, 0.5f);

        Assert.NotEqual(AiState.Chase, Enemy(world).Brain!.State);
    }

    [Fact]
    public void Enemy_sees_through_glass()
    {
        string[] map =
        [
            "####################",
            "#......=...........#",
            "#......=...........#",
            "#......=...........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 2)).With(TestContent.Enemy("thug", 10, 2, facing: 180)).Build());

        Run(world, 0.05f);

        Assert.Equal(AiState.Chase, Enemy(world).Brain!.State);
    }

    [Fact]
    public void Enemy_does_not_see_behind_its_back()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("thug", 8, 2, facing: 0)).Build());

        Run(world, 0.5f);

        Assert.NotEqual(AiState.Chase, Enemy(world).Brain!.State);
    }

    [Fact]
    public void Enemy_notices_a_player_very_close_behind_it()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("thug", 3, 2, facing: 0)).Build());

        Run(world, 0.05f);

        Assert.Equal(AiState.Chase, Enemy(world).Brain!.State);
    }

    [Fact]
    public void Out_of_range_player_is_not_detected()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(40, 5), new Vector2(1, 2)).With(TestContent.Enemy("thug", 38, 2, facing: 180)).Build());

        Run(world, 0.2f);

        Assert.NotEqual(AiState.Chase, Enemy(world).Brain!.State);
    }

    [Fact]
    public void Gunshots_make_enemies_investigate()
    {
        string[] map =
        [
            "####################",
            "#......#...........#",
            "#.................##",
            "#......#...........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 1), playerWeapon: "pistol")
            .With(TestContent.Enemy("thug", 14, 1, facing: 0)).Build());

        // Shoot at the wall: the enemy cannot see the player but hears the shot.
        Run(world, 0.1f, Aim(world, TileCenter(2, 0), attack: true));
        EnemyBrain brain = Enemy(world).Brain!;
        Assert.Equal(AiState.Suspicious, brain.State);

        Run(world, 1f);
        Assert.True(brain.State is AiState.Investigate or AiState.Chase);
    }

    [Fact]
    public void Silent_attacks_are_not_heard()
    {
        string[] map =
        [
            "####################",
            "#......#...........#",
            "#......#...........#",
            "#......#...........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 2), playerWeapon: "knife")
            .With(TestContent.Enemy("thug", 14, 2, facing: 0)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(4, 2), attack: true));

        Assert.Equal(AiState.Idle, Enemy(world).Brain!.State);
    }

    [Fact]
    public void Chasing_enemy_goes_around_obstacles()
    {
        string[] map =
        [
            "##############",
            "#............#",
            "#.##########.#",
            "#............#",
            "##############",
        ];
        // The enemy must go around the long wall to reach the player.
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(1, 3)).With(TestContent.Enemy("thug", 2, 1, facing: 0, weapon: "none")).Build());
        Actor enemy = Enemy(world);
        enemy.Brain!.AlertTo(world.Player);

        Run(world, 6f, new PlayerCommands { AimTarget = TileCenter(5, 3) });

        Assert.True(Vector2.Distance(enemy.Position, world.Player.Position) < 3 * Tile,
            $"enemy at {enemy.Position}, player at {world.Player.Position}");
    }

    [Fact]
    public void Enemy_searches_after_losing_the_player()
    {
        string[] map =
        [
            "####################",
            "#..................#",
            "#..................#",
            "##########.#########",
            "#....#.............#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(12, 4)).With(TestContent.Enemy("thug", 18, 1, facing: 180, weapon: "none")).Build());
        Actor enemy = Enemy(world);
        enemy.Brain!.AlertTo(world.Player);
        // The player vanishes into a closed pocket; the enemy goes to the last known position then searches.
        world.Player.Position = TileCenter(1, 4);

        bool searched = false;
        for (int i = 0; i < 1200 && !searched; i++)
        {
            world.Step(Dt, default);
            searched = enemy.Brain.State == AiState.Search;
        }

        Assert.True(searched);
    }

    [Fact]
    public void Armed_enemy_shoots_when_the_line_of_fire_is_clear()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("guard", 12, 2, facing: 180)).Build());

        Run(world, 1f);

        Assert.Contains(world.Events, e => e is ShotFired shot && !shot.Shooter.IsPlayer);
        Assert.False(world.Player.IsAlive);
    }

    [Fact]
    public void Armed_enemy_waits_for_its_reaction_time()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("guard", 12, 2, facing: 180)).Build());

        Run(world, 0.2f);

        Assert.DoesNotContain(world.Events, e => e is ShotFired);
    }

    [Fact]
    public void Armed_enemy_does_not_shoot_through_an_ally()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2))
            .With(TestContent.Enemy("guard", 12, 2, facing: 180), TestContent.Enemy("dummy", 7, 2)).Build());
        // Keep the shooter in place so the ally stays in between.
        Actor guard = world.Actors.First(a => a.Definition.Id == "guard");

        for (int i = 0; i < 120; i++)
        {
            guard.Position = TileCenter(12, 2);
            world.Step(Dt, default);
        }

        Assert.DoesNotContain(world.Events, e => e is ShotFired);
    }

    [Fact]
    public void Melee_enemy_attacks_when_in_reach()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).With(TestContent.Enemy("thug", 6, 2, facing: 180)).Build());

        Run(world, 2f);

        Assert.False(world.Player.IsAlive);
        Assert.Equal(HitMethod.Melee, Event<ActorKilled>(world)!.Method);
    }

    [Fact]
    public void Dormant_enemies_wake_up_with_a_trigger()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2))
            .With(new LevelActor { Definition = "thug", Position = new Vector2(15, 2), FacingDegrees = 180, Behavior = AiBehavior.Dormant, Tag = "ambush" })
            .WithZone("hall", 4, 1, 2, 3)
            .WithTrigger(TriggerCondition.EnterZone, "hall", new TriggerAction { Type = TriggerActionType.AlertTag, Tag = "ambush" })
            .Build();
        GameWorld world = World(level);
        Actor enemy = Enemy(world);

        Run(world, 0.3f);
        Assert.Equal(AiState.Dormant, enemy.Brain!.State);

        Run(world, 0.6f, new PlayerCommands { Move = Vector2.UnitX });
        Assert.Equal(AiState.Chase, enemy.Brain.State);
    }

    [Fact]
    public void Patrolling_enemy_follows_its_route()
    {
        LevelDefinition level = new LevelDefinitionBuilder(Room(20, 8), new Vector2(1, 6))
            .With(new LevelActor { Definition = "thug", Position = new Vector2(10, 1), FacingDegrees = 0, Behavior = AiBehavior.Patrol, Patrol = [new Vector2(10, 1), new Vector2(16, 1)] })
            .Build();
        GameWorld world = World(level);
        Actor enemy = Enemy(world);

        Run(world, 4f, new PlayerCommands { AimTarget = TileCenter(0, 6) });

        Assert.Equal(AiState.Patrol, enemy.Brain!.State);
        Assert.True(enemy.Position.X > 12 * Tile || enemy.Brain.Path.Count > 0, $"x = {enemy.Position.X}");
    }
}
