using System.Numerics;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class CombatTests
{
    private static Actor Target(GameWorld world) => world.Actors.First(a => !a.IsPlayer);

    [Fact]
    public void Pistol_kills_a_visible_target()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "pistol")
            .With(Enemy("dummy", 12, 2)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(12, 2), attack: true));

        Assert.False(Target(world).IsAlive);
        Assert.Equal(2, world.Player.Weapon!.Ammo);
        Assert.NotNull(Event<ActorKilled>(world));
    }

    [Fact]
    public void A_wall_stops_bullets()
    {
        string[] map =
        [
            "####################",
            "#.........#........#",
            "#.........#........#",
            "#.........#........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 2), playerWeapon: "pistol")
            .With(Enemy("dummy", 15, 2)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(15, 2), attack: true));

        Assert.True(Target(world).IsAlive);
        Assert.NotNull(Event<BulletImpact>(world));
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void No_shooting_through_a_wall_while_hugging_it()
    {
        string[] map =
        [
            "##########",
            "#...#....#",
            "#...#....#",
            "#...#....#",
            "##########",
        ];
        // The player stands against the wall: its muzzle (20 units ahead) is inside the wall.
        var level = new LevelDefinitionBuilder(map, new Vector2(3, 2), playerWeapon: "pistol").With(Enemy("dummy", 5, 2)).Build();
        GameWorld world = World(level);
        Run(world, 0.5f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = TileCenter(6, 2) });
        Assert.True(world.Player.Position.X > 4 * Tile - world.Player.Radius - 1f, "the player should touch the wall");

        Run(world, 0.3f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.True(Target(world).IsAlive);
    }

    [Fact]
    public void Point_blank_target_is_hit()
    {
        // Target touches the player: it is between the player's centre and the muzzle.
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "pistol")
            .With(Enemy("dummy", 2.65f, 2)).Build());

        Run(world, 0.2f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.False(Target(world).IsAlive);
    }

    [Fact]
    public void Very_fast_bullets_do_not_tunnel_through_thin_walls()
    {
        string[] map =
        [
            "##############################",
            "#..............#.............#",
            "#..............#.............#",
            "##############################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 1), playerWeapon: "railgun")
            .With(Enemy("dummy", 25, 1)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(25, 1), attack: true));

        Assert.True(Target(world).IsAlive);
    }

    [Fact]
    public void Hitscan_weapon_hits_instantly()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(30, 5), new Vector2(2, 2), playerWeapon: "revolver")
            .With(Enemy("dummy", 25, 2)).Build());

        world.Step(Dt, Aim(world, TileCenter(25, 2), attack: true));

        Assert.False(Target(world).IsAlive);
        Assert.NotNull(Event<BulletTrace>(world));
    }

    [Fact]
    public void Bullets_break_glass_and_continue()
    {
        string[] map =
        [
            "####################",
            "#.........=........#",
            "#.........=........#",
            "#.........=........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2, 2), playerWeapon: "pistol")
            .With(Enemy("dummy", 15, 2)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(15, 2), attack: true));

        Assert.False(Target(world).IsAlive);
        Assert.Equal("broken_glass", world.Map.Get(10, 2).Id);
        Assert.NotNull(Event<TileBroken>(world));
    }

    [Fact]
    public void Melee_hits_a_target_in_front_within_reach()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(Enemy("dummy", 3, 2)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(5, 2), attack: true));

        Assert.False(Target(world).IsAlive);
    }

    [Fact]
    public void Melee_misses_targets_out_of_reach()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(Enemy("dummy", 4, 2)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.True(Target(world).IsAlive);
    }

    [Fact]
    public void Melee_misses_targets_behind_the_attacker()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(3, 2), playerWeapon: "bat")
            .With(Enemy("dummy", 2.2f, 2)).Build());

        // Facing right, the target is on the left.
        Run(world, 0.5f, Aim(world, TileCenter(8, 2), attack: true));

        Assert.True(Target(world).IsAlive);
    }

    [Fact]
    public void Melee_does_not_reach_through_walls()
    {
        string[] map =
        [
            "#######",
            "#..#..#",
            "#..#..#",
            "#######",
        ];
        // Player and target stand on each side of a one-tile wall, both touching it.
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(2.18f, 1), playerWeapon: "bat")
            .With(Enemy("dummy", 3.82f, 1)).Build());

        Run(world, 0.5f, Aim(world, TileCenter(5, 1), attack: true));

        Assert.True(Target(world).IsAlive);
    }

    [Fact]
    public void Punch_knocks_down_and_the_target_gets_up_later()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .With(Enemy("dummy", 2.8f, 2)).Build());
        Actor target = Target(world);

        Run(world, 0.1f, Aim(world, TileCenter(6, 2), attack: true));
        Assert.Equal(ActorState.KnockedDown, target.State);
        Assert.True(target.IsAlive);

        Run(world, 2.05f);
        Assert.Equal(ActorState.GettingUp, target.State);

        Run(world, 1f);
        Assert.Equal(ActorState.Active, target.State);
    }

    [Fact]
    public void Execution_kills_a_knocked_down_enemy()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .With(Enemy("dummy", 2.8f, 2)).Build());
        Actor target = Target(world);
        Run(world, 0.1f, Aim(world, TileCenter(6, 2), attack: true));
        Assert.Equal(ActorState.KnockedDown, target.State);

        Run(world, 0.05f, new PlayerCommands { InteractPressed = true, AimTarget = target.Position });
        Assert.Equal(ActorState.Executing, world.Player.State);

        Run(world, 1.2f, new PlayerCommands { AimTarget = target.Position });
        Assert.False(target.IsAlive);
        Assert.Equal(ActorState.Active, world.Player.State);
        Assert.Equal(HitMethod.Execution, Event<ActorKilled>(world)!.Method);
    }

    [Fact]
    public void Moving_cancels_an_execution()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .With(Enemy("dummy", 2.8f, 2)).Build());
        Actor target = Target(world);
        Run(world, 0.1f, Aim(world, TileCenter(6, 2), attack: true));
        Run(world, 0.05f, new PlayerCommands { InteractPressed = true });

        Run(world, 0.05f, new PlayerCommands { Move = -Vector2.UnitX });

        Assert.NotEqual(ActorState.Executing, world.Player.State);
        Assert.True(target.IsAlive);
    }

    [Fact]
    public void Damage_multipliers_make_a_brute_immune_to_bats()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(Enemy("brute", 3, 2)).Build());
        Actor brute = Target(world);

        Run(world, 0.5f, Aim(world, brute.Position, attack: true));
        Assert.True(brute.IsAlive);
        Assert.Equal(300f, brute.Health);
        Assert.NotEqual(ActorState.KnockedDown, brute.State);
    }

    [Fact]
    public void A_resistant_enemy_needs_several_bullets()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "pistol")
            .With(Enemy("brute", 10, 2)).Build());
        Actor brute = Target(world);

        Run(world, 0.4f, Aim(world, brute.Position, attack: true));
        Assert.True(brute.IsAlive);
        Assert.Equal(200f, brute.Health);

        for (int i = 0; i < 2; i++) Run(world, 0.4f, Aim(world, brute.Position, attack: true));
        Assert.False(brute.IsAlive);
    }

    [Fact]
    public void Armored_modifier_survives_one_bullet()
    {
        var definitions = Definitions();
        var level = new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2)).Build();
        GameWorld world = World(level, definitions, definitions.Modifiers["armored"]);

        world.Combat.ApplyHit(world.Player, new Hit(100f, "bullet", false, 0f, Vector2.UnitX, 0f, null, HitMethod.Firearm));

        Assert.True(world.Player.IsAlive);
        Assert.Equal(50f, world.Player.Health);
    }

    [Fact]
    public void Dead_actors_cannot_act()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "pistol").Build());
        world.Combat.ApplyHit(world.Player, new Hit(100f, "bullet", false, 0f, Vector2.UnitX, 0f, null, HitMethod.Firearm));

        Assert.Equal(ActorState.Dead, world.Player.State);
        Assert.False(world.Combat.TryAttack(world.Player));
        Run(world, 0.5f, new PlayerCommands { Move = Vector2.UnitX, AttackPressed = true });
        Assert.Equal(ActorState.Dead, world.Player.State);
    }
}
