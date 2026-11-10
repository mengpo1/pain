using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

public class WeaponTests
{
    [Fact]
    public void Ammo_runs_out_and_the_gun_clicks()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "pistol").Build());
        Vector2 aim = TileCenter(15, 2);

        for (int i = 0; i < 3; i++) Run(world, 0.3f, Aim(world, aim, attack: true));
        Assert.Equal(0, world.Player.Weapon!.Ammo);
        Assert.True(world.Player.Weapon.IsEmpty);
        world.ClearEvents();

        Run(world, 0.3f, Aim(world, aim, attack: true));

        Assert.NotNull(Event<WeaponEmpty>(world));
        Assert.Null(Event<ShotFired>(world));
    }

    [Fact]
    public void Empty_weapon_can_be_thrown_automatically()
    {
        var definitions = Definitions();
        definitions.Weapons["pistol"].WhenEmpty = EmptyBehavior.Throw;
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "pistol").Build(), definitions);
        for (int i = 0; i < 4; i++) Run(world, 0.3f, Aim(world, TileCenter(15, 2), attack: true));

        Assert.Null(world.Player.Weapon);
        Assert.NotNull(Event<WeaponThrown>(world));
    }

    [Fact]
    public void Unarmed_player_picks_up_a_nearby_weapon()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).WithWeapon("bat", 2.4f, 2).Build());
        Assert.Single(world.Weapons);

        Run(world, 0.05f, new PlayerCommands { PickupOrThrowPressed = true });

        Assert.Equal("bat", world.Player.Weapon?.Definition.Id);
        Assert.Empty(world.Weapons);
    }

    [Fact]
    public void Picking_up_while_armed_swaps_weapons()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "knife").WithWeapon("bat", 2.4f, 2).Build());

        Run(world, 0.05f, new PlayerCommands { PickupOrThrowPressed = true });

        Assert.Equal("bat", world.Player.Weapon?.Definition.Id);
        WorldWeapon dropped = Assert.Single(world.Weapons);
        Assert.Equal("knife", dropped.Weapon.Definition.Id);
        Assert.False(dropped.IsFlying);
    }

    [Fact]
    public void Throw_rule_throws_instead_of_swapping()
    {
        var definitions = Definitions(rules => rules.PickupWhileArmed = PickupWhileArmed.Throw);
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "knife").WithWeapon("bat", 2.4f, 2).Build(), definitions);

        Run(world, 0.02f, new PlayerCommands { PickupOrThrowPressed = true, AimTarget = TileCenter(8, 2) });

        Assert.Null(world.Player.Weapon);
        Assert.Contains(world.Weapons, w => w.Weapon.Definition.Id == "knife" && w.IsFlying);
    }

    [Fact]
    public void Ammo_is_kept_when_a_gun_changes_hands()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).WithWeapon("pistol", 2.4f, 2, ammo: 1).Build());

        Run(world, 0.05f, new PlayerCommands { PickupOrThrowPressed = true });

        Assert.Equal(1, world.Player.Weapon!.Ammo);
    }

    [Fact]
    public void Thrown_blunt_weapon_knocks_down_and_can_be_picked_up_again()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(Enemy("dummy", 8, 2)).Build());
        Actor target = world.Actors.First(a => !a.IsPlayer);

        Run(world, 0.02f, new PlayerCommands { PickupOrThrowPressed = true, AimTarget = target.Position });
        Assert.True(world.Weapons.Single().IsFlying);

        Run(world, 1.5f, new PlayerCommands { AimTarget = target.Position });

        Assert.Equal(ActorState.KnockedDown, target.State);
        Assert.True(target.IsAlive);
        WorldWeapon bat = world.Weapons.Single();
        Assert.False(bat.IsFlying);
        Assert.True(bat.IsResting);
    }

    [Fact]
    public void Thrown_knife_kills()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2), playerWeapon: "knife")
            .With(Enemy("dummy", 10, 2)).Build());
        Actor target = world.Actors.First(a => !a.IsPlayer);

        Run(world, 1f, new PlayerCommands { PickupOrThrowPressed = true, AimTarget = target.Position });

        Assert.False(target.IsAlive);
        Assert.Equal(HitMethod.Thrown, Event<ActorKilled>(world)!.Method);
    }

    [Fact]
    public void Thrown_weapon_bounces_off_walls()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(8, 5), new Vector2(2, 2), playerWeapon: "bat").Build());

        Run(world, 1.5f, new PlayerCommands { PickupOrThrowPressed = true, AimTarget = TileCenter(7, 2) });

        WorldWeapon bat = world.Weapons.Single();
        Assert.True(bat.Position.X < 7 * Tile - WorldWeapon.CollisionRadius + 0.5f);
        Assert.False(bat.IsFlying);
    }

    [Fact]
    public void Knocked_down_enemies_drop_their_weapon()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2))
            .With(Enemy("dummy", 2.8f, 2, weapon: "pistol")).Build());
        Actor target = world.Actors.First(a => !a.IsPlayer);

        Run(world, 0.1f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.Null(target.Weapon);
        Assert.Contains(world.Weapons, w => w.Weapon.Definition.Id == "pistol");
    }

    [Fact]
    public void No_firearm_pickup_with_a_melee_only_modifier()
    {
        var definitions = Definitions();
        var brawler = new ModifierDefinition { Id = "brawler", AllowFirearms = false };
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).WithWeapon("pistol", 2.4f, 2).Build(), definitions, brawler);

        Run(world, 0.05f, new PlayerCommands { PickupOrThrowPressed = true });

        Assert.Null(world.Player.Weapon);
    }
}
