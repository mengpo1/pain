using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Weapons that sever heads and limbs on a kill.</summary>
public class DismemberTests
{
    private static DefinitionSet WithParts(DismemberDefinition? dismember)
    {
        DefinitionSet definitions = Definitions();
        ActorSprites sprites = definitions.Actor("dummy").Sprites;
        sprites.Torso = "actor.dummy.torso";
        sprites.Parts =
        [
            new BodyPart { Id = "head", Sprite = "actor.dummy.head", Over = true },
            new BodyPart { Id = "arm", Sprite = "actor.dummy.arm1" },
            new BodyPart { Id = "leg", Sprite = "actor.dummy.leg1" },
        ];
        definitions.Weapon("knife").Dismember = dismember;
        return definitions;
    }

    private static (GameWorld World, Actor Dummy) Room(DefinitionSet definitions, float dummyX = 8f)
    {
        GameWorld world = World(new LevelDefinitionBuilder(TestContent.Room(20, 7), new Vector2(3, 3))
            .With(TestContent.Enemy("dummy", dummyX, 3)).Build(), definitions);
        return (world, world.Actors[1]);
    }

    private static void Slash(GameWorld world, Actor target, HitMethod method = HitMethod.Melee) =>
        world.Combat.ApplyHit(target, new Hit(100f, "sharp", false, 0f, Vector2.UnitX, 90f, world.Player, method, world.Definitions.Weapon("knife")));

    [Fact]
    public void A_dismembering_weapon_reports_the_severed_parts_of_its_kill()
    {
        var (world, dummy) = Room(WithParts(new DismemberDefinition { Chance = 1f, Parts = ["head", "arm"], Count = 2 }));

        Slash(world, dummy);

        ActorKilled death = Assert.Single(world.Events.OfType<ActorKilled>());
        Assert.Same(world.Definitions.Weapon("knife"), death.Weapon);
        Assert.Equal([0, 1], death.Severed!.Order());
    }

    [Fact]
    public void Only_the_listed_kinds_of_parts_come_off_and_never_twice()
    {
        var (world, dummy) = Room(WithParts(new DismemberDefinition { Chance = 1f, Parts = ["leg"], Count = 3 }));

        Slash(world, dummy);

        Assert.Equal([2], world.Events.OfType<ActorKilled>().Single().Severed!);
    }

    [Fact]
    public void Nothing_comes_off_beyond_the_weapon_reach_or_without_a_dismembering_weapon()
    {
        var (far, farDummy) = Room(WithParts(new DismemberDefinition { Chance = 1f, Parts = ["head"], MaxDistance = 100f }), dummyX: 12f);
        Slash(far, farDummy);
        Assert.Empty(far.Events.OfType<ActorKilled>().Single().Severed!);

        var (plain, plainDummy) = Room(WithParts(null));
        Slash(plain, plainDummy);
        Assert.Empty(plain.Events.OfType<ActorKilled>().Single().Severed!);
    }

    [Fact]
    public void Executions_leave_the_severing_to_their_animation()
    {
        var (world, dummy) = Room(WithParts(new DismemberDefinition { Chance = 1f, Parts = ["head"] }));

        Slash(world, dummy, HitMethod.Execution);

        Assert.Empty(world.Events.OfType<ActorKilled>().Single().Severed!);
    }

    [Fact]
    public void A_beheading_is_done_standing_beside_the_head()
    {
        DefinitionSet definitions = WithParts(null);
        definitions.Weapon("bat").Execution.Animations = [ExecutionAnimation.Behead];
        GameWorld world = World(new LevelDefinitionBuilder(TestContent.Room(14, 9), new Vector2(3, 4), playerWeapon: "bat")
            .With(TestContent.Enemy("dummy", 4f, 4)).Build(), definitions);
        Actor victim = world.Actors[1];
        victim.KnockDown(5f);

        Run(world, 0.02f, new PlayerCommands { InteractPressed = true, AimTarget = TileCenter(10, 4) });
        Run(world, 0.1f);

        Assert.Equal(ExecutionAnimation.Behead, world.Player.ExecutionAnimation);
        Assert.False(ExecutionPose.IsAstride(ExecutionAnimation.Behead));
        float distance = Vector2.Distance(world.Player.Position, ExecutionPose.HeadOf(victim));
        Assert.InRange(distance, world.Player.Radius, world.Player.Radius + 14f);
    }
}
