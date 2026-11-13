using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Several enemies against the player: no friendly knock-outs, one executioner at a time.</summary>
public class GroupFightTests
{
    private static GameWorld TwoBrawlers(DefinitionSet? definitions = null, float firstX = 6f, float secondX = 7f) =>
        World(new LevelDefinitionBuilder(Room(16, 7), new Vector2(3, 3))
            .With(TestContent.Enemy("brawler", firstX, 3, facing: 180, id: "first", weapon: "none"),
                  TestContent.Enemy("brawler", secondX, 3, facing: 180, id: "second", weapon: "none")).Build(), definitions);

    [Fact]
    public void Two_unarmed_enemies_attacking_the_player_never_hurt_each_other()
    {
        // Lined up: the second one punches through the first one's position.
        GameWorld world = TwoBrawlers();
        Run(world, 4f);

        Assert.DoesNotContain(world.Events.OfType<ActorHit>(), hit => !hit.Target.IsPlayer);
        Assert.DoesNotContain(world.Events.OfType<ActorKnockedDown>(), down => !down.Target.IsPlayer);
        Assert.All(world.Actors.Where(a => !a.IsPlayer), enemy => Assert.True(enemy.IsAlive));
    }

    [Fact]
    public void The_player_is_a_valid_target_for_each_enemy()
    {
        DefinitionSet definitions = Definitions();
        GameWorld world = World(new LevelDefinitionBuilder(Room(16, 7), new Vector2(5, 3)).Build(), definitions);
        Actor left = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4.3f, 3), 0f);
        Actor right = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(5.7f, 3), MathF.PI);
        WeaponDefinition fists = world.Combat.NaturalWeapon(left);

        world.Combat.ResolveMelee(left, fists);
        world.Combat.ResolveMelee(right, fists);

        List<ActorHit> hits = world.Events.OfType<ActorHit>().ToList();
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.True(hit.Target.IsPlayer));
        Assert.Contains(hits, hit => hit.Source == left);
        Assert.Contains(hits, hit => hit.Source == right);
    }

    [Fact]
    public void An_enemy_knocks_the_player_down_then_finishes_him()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(16, 7), new Vector2(3, 3))
            .With(TestContent.Enemy("brawler", 7, 3, facing: 180, weapon: "none")).Build());

        Run(world, 4f);

        Assert.Contains(world.Events.OfType<ActorKnockedDown>(), down => down.Target.IsPlayer);
        ActorKilled death = Assert.Single(world.Events.OfType<ActorKilled>());
        Assert.True(death.Victim.IsPlayer);
        Assert.Equal(HitMethod.Execution, death.Method);
        Assert.Equal(WorldOutcome.Defeat, world.Outcome);
    }

    [Fact]
    public void A_second_enemy_does_not_disrupt_an_execution()
    {
        GameWorld world = TwoBrawlers(firstX: 6f, secondX: 6f);
        Actor? executioner = null;
        bool otherHitDuringExecution = false;
        for (int i = 0; i < 600 && world.Player.IsAlive; i++)
        {
            bool pinned = world.Player.State == ActorState.BeingExecuted;
            world.Step(Dt, default);
            if (world.Player.State == ActorState.BeingExecuted) executioner ??= world.Player.Executioner;
            if (pinned)
                otherHitDuringExecution |= world.Events.OfType<ActorHit>().Any(hit => hit.Target.IsPlayer && hit.Source != executioner);
            world.ClearEvents();
        }

        Assert.False(world.Player.IsAlive);
        Assert.NotNull(executioner);
        Assert.False(otherHitDuringExecution);
    }

    [Fact]
    public void While_one_enemy_executes_the_other_waits_nearby()
    {
        GameWorld world = TwoBrawlers();
        Actor? waiting = null;
        for (int i = 0; i < 600 && world.Player.IsAlive; i++)
        {
            world.Step(Dt, default);
            if (world.Player.State != ActorState.BeingExecuted) continue;
            waiting = world.Actors.First(a => !a.IsPlayer && a != world.Player.Executioner);
            Assert.NotEqual(ActorState.Executing, waiting.State);
        }

        Assert.NotNull(waiting);
        Assert.True(Vector2.Distance(waiting!.Position, world.Player.Position) < 4 * Tile);
        Assert.Single(world.Events.OfType<ActorKilled>(), k => k.Victim.IsPlayer);
    }

    [Fact]
    public void Enemies_surround_the_player_instead_of_queueing()
    {
        DefinitionSet definitions = Definitions();
        definitions.Actors["player"].KnockdownImmune = true;
        // Both come from the same side, one behind the other.
        GameWorld world = TwoBrawlers(definitions, firstX: 9f, secondX: 10f);
        Run(world, 2f);

        Actor first = world.FindActor("first")!;
        Actor second = world.FindActor("second")!;
        float a = MathF.Atan2(first.Position.Y - world.Player.Position.Y, first.Position.X - world.Player.Position.X);
        float b = MathF.Atan2(second.Position.Y - world.Player.Position.Y, second.Position.X - world.Player.Position.X);
        float spread = MathF.Abs(TopDown.Core.Geometry.Geo.AngleDifference(a, b));
        Assert.True(spread > 0.5f, $"angle between attackers {spread} rad");
        Assert.True(Vector2.Distance(first.Position, world.Player.Position) < 3 * Tile);
        Assert.True(Vector2.Distance(second.Position, world.Player.Position) < 3 * Tile);
    }

    [Fact]
    public void Friendly_melee_damage_can_be_enabled_explicitly()
    {
        DefinitionSet definitions = Definitions(rules => rules.FriendlyFire.Melee = true);
        GameWorld world = World(new LevelDefinitionBuilder(Room(16, 7), new Vector2(12, 3)).Build(), definitions);
        Actor front = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4.6f, 3), MathF.PI);
        Actor back = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4f, 3), 0f);

        world.Combat.ResolveMelee(back, world.Combat.NaturalWeapon(back));

        Assert.Equal(ActorState.KnockedDown, front.State);
    }

    [Fact]
    public void Friendly_melee_damage_is_off_by_default()
    {
        DefinitionSet definitions = Definitions();
        GameWorld world = World(new LevelDefinitionBuilder(Room(16, 7), new Vector2(12, 3)).Build(), definitions);
        Actor front = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4.6f, 3), MathF.PI);
        Actor back = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4f, 3), 0f);

        world.Combat.ResolveMelee(back, world.Combat.NaturalWeapon(back));

        Assert.Equal(ActorState.Active, front.State);
        Assert.Empty(world.Events.OfType<ActorHit>());
    }

    [Fact]
    public void Allied_door_hits_follow_the_friendly_fire_rule()
    {
        Assert.True(Definitions().Rules.FriendlyFire.Doors);
        DefinitionSet definitions = Definitions(rules => rules.FriendlyFire.Doors = false);
        GameWorld world = World(new LevelDefinitionBuilder(Room(16, 7), new Vector2(12, 3)).Build(), definitions);
        Actor pusher = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(4f, 3), 0f);
        Actor ally = world.SpawnActor(definitions.Actor("brawler"), false, TileCenter(6f, 3), 0f);

        Assert.False(world.Combat.CanDamage(pusher, ally, DamageKind.Door));
        Assert.True(world.Combat.CanDamage(pusher, world.Player, DamageKind.Door));
    }
}
