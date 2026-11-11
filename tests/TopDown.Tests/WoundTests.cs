using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Wounded characters: left crawling by a lethal hit, never getting up, finished off.</summary>
public class WoundTests
{
    private static DefinitionSet Woundable(float chance = 1f, float bleedOut = 0f)
    {
        DefinitionSet definitions = Definitions();
        definitions.Actor("dummy").Wound = new WoundDefinition { CrawlSpeed = 30f, BleedOutTime = bleedOut };
        definitions.Weapon("pistol").WoundChance = chance;
        return definitions;
    }

    private static GameWorld Room(DefinitionSet definitions, float dummyX = 8f, string playerWeapon = "pistol", bool eliminateAll = false)
    {
        var builder = new LevelDefinitionBuilder(TestContent.Room(20, 7), new Vector2(3, 3), playerWeapon: playerWeapon)
            .With(TestContent.Enemy("dummy", dummyX, 3, facing: 180));
        if (eliminateAll) builder.WithObjective(ObjectiveType.EliminateAll);
        return World(builder.Build(), definitions);
    }

    /// <summary>A pistol bullet that would kill, applied directly.</summary>
    private static void Shoot(GameWorld world, Actor target) =>
        world.Combat.ApplyHit(target, new Hit(100f, "bullet", false, 0f, Vector2.UnitX, 120f, world.Player, HitMethod.Firearm,
            world.Definitions.Weapon("pistol")));

    [Fact]
    public void A_lethal_bullet_can_leave_the_target_crawling_instead_of_dead()
    {
        GameWorld world = Room(Woundable());
        Actor dummy = world.Actors[1];

        Run(world, 0.02f, Aim(world, dummy.Position, attack: true));
        Run(world, 0.2f, Aim(world, dummy.Position));

        Assert.True(dummy.IsAlive);
        Assert.Equal(ActorState.Wounded, dummy.State);
        Assert.True(dummy.IsDown);
        Assert.Single(world.Events.OfType<ActorWounded>());
        Assert.Empty(world.Events.OfType<ActorKilled>());
    }

    [Fact]
    public void A_wounded_character_crawls_away_and_never_gets_up()
    {
        GameWorld world = Room(Woundable());
        Actor dummy = world.Actors[1];
        Shoot(world, dummy);
        Run(world, 0.3f);
        float before = Vector2.Distance(dummy.Position, world.Player.Position);

        Run(world, 4f);

        Assert.Equal(ActorState.Wounded, dummy.State);
        Assert.True(Vector2.Distance(dummy.Position, world.Player.Position) > before + 20f, "crawled away from the player");
        // Head first: its body points the way it crawls.
        Assert.True(Vector2.Dot(Vector2.Normalize(dummy.Position - world.Player.Position), new Vector2(MathF.Cos(dummy.Facing), MathF.Sin(dummy.Facing))) > 0.7f);
    }

    [Fact]
    public void A_wounded_character_can_be_executed()
    {
        GameWorld world = Room(Woundable(), dummyX: 4f);
        Actor dummy = world.Actors[1];
        Shoot(world, dummy);

        Assert.Same(dummy, world.Combat.FindExecutionTarget(world.Player));
        Run(world, 0.02f, new PlayerCommands { InteractPressed = true, AimTarget = dummy.Position });
        Assert.Equal(ActorState.BeingExecuted, dummy.State);
        Run(world, 2f);

        Assert.False(dummy.IsAlive);
        Assert.Equal(HitMethod.Execution, Assert.Single(world.Events.OfType<ActorKilled>()).Method);
    }

    [Fact]
    public void Blows_without_damage_do_not_get_a_crawler_up_but_any_damage_kills_it()
    {
        GameWorld world = Room(Woundable());
        Actor dummy = world.Actors[1];
        Shoot(world, dummy);

        WeaponDefinition fists = world.Definitions.Weapon("unarmed");
        world.Combat.ApplyHit(dummy, new Hit(0f, "unarmed", true, 2f, Vector2.UnitX, 160f, world.Player, HitMethod.Melee, fists));
        Run(world, 4f);
        Assert.Equal(ActorState.Wounded, dummy.State);

        Vector2 position = dummy.Position;
        world.Combat.ApplyHit(dummy, new Hit(100f, "blunt", true, 2f, Vector2.UnitX, 160f, world.Player, HitMethod.Melee, world.Definitions.Weapon("bat")));
        Assert.False(dummy.IsAlive);
        Assert.Equal(position, dummy.Position);
    }

    [Fact]
    public void A_crawler_can_bleed_out_and_the_kill_goes_to_whoever_wounded_it()
    {
        GameWorld world = Room(Woundable(bleedOut: 1.5f));
        Actor dummy = world.Actors[1];
        Shoot(world, dummy);

        Run(world, 1f);
        Assert.True(dummy.IsAlive);
        Run(world, 1f);

        Assert.False(dummy.IsAlive);
        ActorKilled death = Assert.Single(world.Events.OfType<ActorKilled>());
        Assert.Same(world.Player, death.Killer);
        Assert.Equal(HitMethod.Firearm, death.Method);
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(1f, false)]
    public void Only_woundable_characters_hit_by_a_wounding_weapon_crawl(float chance, bool woundable)
    {
        DefinitionSet definitions = Woundable(chance);
        if (!woundable) definitions.Actor("dummy").Wound = null;
        GameWorld world = Room(definitions);
        Actor dummy = world.Actors[1];

        Shoot(world, dummy);

        Assert.False(dummy.IsAlive);
        Assert.Empty(world.Events.OfType<ActorWounded>());
    }

    [Fact]
    public void A_crawler_must_be_finished_to_clear_the_level()
    {
        GameWorld world = Room(Woundable(), eliminateAll: true);
        Actor dummy = world.Actors[1];
        Shoot(world, dummy);
        Run(world, 1f);

        Assert.False(world.Objectives.AllComplete);
        Shoot(world, dummy);
        Run(world, 0.1f);
        Assert.True(world.Objectives.AllComplete);
    }

    [Fact]
    public void An_interrupted_execution_leaves_a_crawler_crawling()
    {
        GameWorld world = Room(Woundable(), dummyX: 4f);
        Actor dummy = world.Actors[1];
        Shoot(world, dummy);
        Run(world, 0.02f, new PlayerCommands { InteractPressed = true, AimTarget = dummy.Position });
        Assert.Equal(ActorState.BeingExecuted, dummy.State);

        // Something floors the executioner before the last blow.
        world.Combat.ApplyHit(world.Player, new Hit(0f, "unarmed", true, 2f, Vector2.UnitX, 0f, null, HitMethod.Door));

        Assert.Equal(ActorState.Wounded, dummy.State);
        Assert.Null(dummy.Executioner);
    }
}
