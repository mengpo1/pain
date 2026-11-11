using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Where the executioner stands and which way it faces, by execution animation and victim.</summary>
public class ExecutionPoseTests
{
    private static (GameWorld World, Actor Victim) Downed(DefinitionSet definitions, float victimFacingDegrees, string? playerWeapon = null)
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(14, 9), new Vector2(3, 4), playerWeapon: playerWeapon)
            .With(TestContent.Enemy("dummy", 4f, 4)).Build(), definitions);
        Actor victim = world.Actors[1];
        victim.KnockDown(5f);
        victim.Facing = Geo.DegToRad(victimFacingDegrees);
        return (world, victim);
    }

    private static DefinitionSet FistsWith(params ExecutionAnimation[] animations)
    {
        DefinitionSet definitions = Definitions();
        definitions.Weapon("unarmed").Execution.Animations = animations.ToList();
        return definitions;
    }

    private static readonly PlayerCommands Interact = new() { InteractPressed = true, AimTarget = TileCenter(10, 4) };

    [Fact]
    public void Astride_the_executioner_sits_on_the_chest_facing_the_head()
    {
        // The body lies with its head pointing down the screen, across the player's path.
        var (world, victim) = Downed(FistsWith(ExecutionAnimation.HeadBash), 90f);

        Run(world, 0.02f, Interact);
        Run(world, 0.08f);

        Actor player = world.Player;
        Assert.Equal(ExecutionAnimation.HeadBash, player.ExecutionAnimation);
        Vector2 chest = victim.Position + Geo.FromAngle(victim.Facing) * (victim.Radius * 0.3f);
        Assert.True(Vector2.Distance(player.Position, chest) < 1f, $"astride the chest (off by {Vector2.Distance(player.Position, chest)})");
        Assert.True(MathF.Abs(Geo.AngleDifference(player.Facing, victim.Facing)) < 0.05f, "facing the victim's head");
    }

    [Fact]
    public void Kicking_the_executioner_stands_beside_the_head_and_faces_it()
    {
        var (world, victim) = Downed(FistsWith(ExecutionAnimation.Stomp), 90f);
        Vector2 start = world.Player.Position;

        Run(world, 0.02f, Interact);
        Run(world, 0.08f);

        Actor player = world.Player;
        Vector2 head = ExecutionPose.HeadOf(victim);
        float distance = Vector2.Distance(player.Position, head);
        Assert.InRange(distance, player.Radius, player.Radius + 8f);
        Assert.True(MathF.Abs(Geo.AngleDifference(player.Facing, Geo.ToAngle(head - player.Position))) < 0.05f, "facing the head");
        // It stays on the side it came from instead of walking over the body.
        Vector2 right = Geo.Perpendicular(Geo.FromAngle(victim.Facing));
        Assert.Equal(MathF.Sign(Vector2.Dot(start - victim.Position, right)), MathF.Sign(Vector2.Dot(player.Position - victim.Position, right)));
    }

    [Theory]
    [InlineData(ExecutionAnimation.HeadBash)]
    [InlineData(ExecutionAnimation.Stomp)]
    public void An_executed_body_does_not_move_or_turn(ExecutionAnimation animation)
    {
        var (world, victim) = Downed(FistsWith(animation), 90f);
        Vector2 position = victim.Position;
        float facing = victim.Facing;

        Run(world, 0.02f, Interact);
        Run(world, 2f);

        Assert.False(victim.IsAlive);
        Assert.True(Vector2.Distance(position, victim.Position) < 0.5f, "the body stays where it lay");
        Assert.Equal(facing, victim.Facing);
    }

    [Fact]
    public void A_weapon_can_execute_another_anatomy_differently()
    {
        DefinitionSet definitions = FistsWith(ExecutionAnimation.HeadBash);
        definitions.Actor("dummy").Anatomy = "quadruped";
        definitions.Weapon("unarmed").Execution.Against["quadruped"] = new ExecutionVariant
        {
            Strikes = 2,
            Animations = [ExecutionAnimation.TearLegs],
        };
        var (world, victim) = Downed(definitions, 0f);

        Run(world, 0.02f, Interact);
        Assert.Equal(ExecutionAnimation.TearLegs, Assert.Single(world.Events.OfType<ExecutionStarted>()).Animation);
        Run(world, 0.08f);
        // Astride the beast like every kneeling pose: shoulders across its body, facing its head.
        Assert.True(MathF.Abs(Geo.AngleDifference(world.Player.Facing, victim.Facing)) < 0.05f);
        Run(world, 2f);

        Assert.False(victim.IsAlive);
        Assert.Equal(2, world.Events.OfType<ExecutionStrike>().Count(s => s.Victim == victim));
    }

    [Fact]
    public void The_animation_is_drawn_among_the_listed_ones()
    {
        var seen = new HashSet<ExecutionAnimation>();
        for (int seed = 1; seed <= 20; seed++)
        {
            DefinitionSet definitions = FistsWith(ExecutionAnimation.HeadBash, ExecutionAnimation.Stomp);
            GameWorld world = GameWorld.Create(new LevelDefinitionBuilder(Room(14, 9), new Vector2(3, 4))
                .With(TestContent.Enemy("dummy", 4f, 4)).Build(), definitions, seed);
            world.Actors[1].KnockDown(5f);
            Run(world, 0.02f, Interact);
            seen.Add(world.Player.ExecutionAnimation);
        }

        Assert.Equal([ExecutionAnimation.HeadBash, ExecutionAnimation.Stomp], seen.Order());
    }

    [Theory]
    [InlineData(ExecutionAnimation.HeadBash)]
    [InlineData(ExecutionAnimation.Stab)]
    [InlineData(ExecutionAnimation.TearHead)]
    [InlineData(ExecutionAnimation.TearLegs)]
    public void Every_astride_pose_faces_along_the_body(ExecutionAnimation animation)
    {
        // Whatever the body's orientation and the side the executioner comes from.
        foreach (float bodyFacing in new[] { 0f, 90f, 200f, 300f })
        {
            var (world, victim) = Downed(FistsWith(animation), bodyFacing);
            Run(world, 0.02f, Interact);
            Run(world, 0.1f);

            Assert.True(ExecutionPose.IsAstride(world.Player.ExecutionAnimation));
            Assert.True(MathF.Abs(Geo.AngleDifference(world.Player.Facing, victim.Facing)) < 0.05f, $"{animation} on a body at {bodyFacing}°");
        }
    }

    [Fact]
    public void An_empty_gun_executes_with_a_blow_instead_of_a_shot()
    {
        var (loaded, _) = Downed(Definitions(), 0f, "pistol");
        Run(loaded, 0.02f, Interact);
        Assert.Equal(ExecutionAnimation.Shoot, loaded.Player.ExecutionAnimation);

        var (empty, _) = Downed(Definitions(), 0f, "pistol");
        empty.Player.Weapon = new WeaponInstance(empty.Player.Weapon!.Definition, ammo: 0);
        Run(empty, 0.02f, Interact);
        Assert.Equal(ExecutionAnimation.Smash, empty.Player.ExecutionAnimation);
    }
}
