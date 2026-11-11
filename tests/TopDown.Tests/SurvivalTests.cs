using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Survival mechanics: spare ammunition and reloading, medkits, running, searching, mission items.</summary>
public class SurvivalTests
{
    private static DefinitionSet Survival()
    {
        DefinitionSet definitions = Definitions();
        WeaponDefinition pistol = definitions.Weapon("pistol");
        pistol.AmmoType = "9mm";
        pistol.ReloadTime = 0.5f;
        ActorDefinition player = definitions.Actor("player");
        player.RunSpeed = 300f;
        player.Stamina = new StaminaDefinition { Max = 100f, RunDrain = 50f, Regen = 40f, RegenDelay = 0.2f, RecoverThreshold = 30f, RunNoise = 200f };
        return definitions;
    }

    private static GameWorld Room(DefinitionSet definitions, Action<LevelDefinition>? edit = null, string playerWeapon = "pistol")
    {
        LevelDefinition level = new LevelDefinitionBuilder(TestContent.Room(24, 9), new Vector2(3, 4), playerWeapon: playerWeapon).Build();
        edit?.Invoke(level);
        return World(level, definitions);
    }

    private static PlayerCommands Fire(GameWorld world) => new() { AttackPressed = true, AimTarget = world.Player.Position + new Vector2(200, 0) };

    [Fact]
    public void A_gun_with_an_ammo_type_reloads_from_the_reserve()
    {
        GameWorld world = Room(Survival(), level => level.Player.Inventory.Add(new LootEntry { Ammo = "9mm", Amount = 5 }));
        WeaponInstance gun = world.Player.Weapon!;
        for (int i = 0; i < 3; i++) Run(world, 0.3f, Fire(world));
        Assert.Equal(0, gun.Ammo);

        // The trigger on an empty magazine starts the reload instead of clicking.
        Run(world, 0.02f, Fire(world));
        Assert.True(world.Player.IsReloading);
        Assert.Single(world.Events.OfType<ReloadStarted>());
        Assert.Empty(world.Events.OfType<WeaponEmpty>());
        Run(world, 0.6f);

        Assert.False(world.Player.IsReloading);
        Assert.Equal(3, gun.Ammo);
        Assert.Equal(2, world.Inventory.AmmoOf("9mm"));
    }

    [Fact]
    public void Reloading_takes_its_time_and_blocks_shooting()
    {
        GameWorld world = Room(Survival(), level => level.Player.Inventory.Add(new LootEntry { Ammo = "9mm", Amount = 10 }));
        Run(world, 0.3f, Fire(world));
        Run(world, 0.02f, new PlayerCommands { ReloadPressed = true });
        Assert.True(world.Player.IsReloading);

        Run(world, 0.1f, Fire(world));
        Assert.Single(world.Events.OfType<ShotFired>());
        Run(world, 0.5f);
        Assert.Equal(3, world.Player.Weapon!.Ammo);
    }

    [Fact]
    public void Without_spare_rounds_or_ammo_type_an_empty_gun_only_clicks()
    {
        GameWorld world = Room(Survival());
        for (int i = 0; i < 4; i++) Run(world, 0.3f, Fire(world));

        Assert.False(world.Player.IsReloading);
        Assert.NotEmpty(world.Events.OfType<WeaponEmpty>());
        Assert.False(world.Combat.TryReload(world.Player));
    }

    [Fact]
    public void A_medkit_heals_a_hurt_player_and_is_used_up()
    {
        GameWorld world = Room(Survival(), level => level.Player.Inventory.Add(new LootEntry { Medkits = 1 }));
        Actor player = world.Player;
        Assert.False(world.TryHeal(player), "nothing to heal at full health");

        player.Health = 30f;
        Run(world, 0.02f, new PlayerCommands { HealPressed = true });

        Assert.Equal(80f, player.Health);
        Assert.Equal(0, world.Inventory.Medkits);
        Assert.Single(world.Events.OfType<Healed>());
        Assert.False(world.TryHeal(player));
    }

    [Fact]
    public void Running_is_faster_spends_stamina_and_is_heard()
    {
        GameWorld world = Room(Survival());
        Actor player = world.Player;
        var run = new PlayerCommands { Move = Vector2.UnitX, Run = true, AimTarget = player.Position + new Vector2(300, 0) };

        Run(world, 0.5f, run);
        Assert.True(player.IsRunning);
        Assert.InRange(player.Velocity.X, 290f, 301f);
        Assert.True(player.Stamina < 80f);
        Assert.Contains(world.Events.OfType<NoiseMade>(), noise => noise.Radius == 200f);

        // Exhausted: back to walking until enough stamina came back.
        Run(world, 2f, run with { AimTarget = player.Position - new Vector2(300, 0), Move = -Vector2.UnitX });
        Assert.False(player.IsRunning);
        Assert.True(MathF.Abs(player.Velocity.X) <= player.Definition.Speed + 1f);
    }

    [Fact]
    public void Without_stamina_the_player_never_runs()
    {
        GameWorld world = Room(Definitions());
        var run = new PlayerCommands { Move = Vector2.UnitX, Run = true, AimTarget = world.Player.Position + new Vector2(300, 0) };
        Run(world, 0.5f, run);

        Assert.False(world.Player.IsRunning);
        Assert.InRange(world.Player.Velocity.X, 0f, world.Player.Definition.Speed + 1f);
    }

    [Fact]
    public void A_searched_container_gives_its_loot_once()
    {
        GameWorld world = Room(Survival(), level => level.Interactables.Add(new LevelInteractable
        {
            Id = "locker", Sprite = "prop.locker", PromptKey = "prompt.search", Position = new Vector2(4, 4),
            Loot = [new LootEntry { Ammo = "9mm", Amount = 6 }, new LootEntry { Medkits = 1 }, new LootEntry { Item = "battery", NameKey = "item.battery" }],
        }));

        Run(world, 0.02f, new PlayerCommands { InteractPressed = true });
        Run(world, 0.02f, new PlayerCommands { InteractPressed = true });

        Assert.Equal(6, world.Inventory.AmmoOf("9mm"));
        Assert.Equal(1, world.Inventory.Medkits);
        Assert.True(world.Inventory.Has("battery"));
        Assert.Single(world.Events.OfType<LootReceived>());
        Assert.False(world.Interactables.Single().Available);
    }

    [Fact]
    public void A_weapon_found_in_a_container_is_left_on_the_floor()
    {
        GameWorld world = Room(Survival(), level => level.Interactables.Add(new LevelInteractable
        {
            Id = "crate", Sprite = "prop.crate", Position = new Vector2(4, 4), Loot = [new LootEntry { Weapon = "bat" }],
        }));

        Run(world, 0.02f, new PlayerCommands { InteractPressed = true });

        Assert.Contains(world.Weapons, w => w.Weapon.Definition.Id == "bat");
    }

    [Fact]
    public void Walking_over_an_ammo_box_fills_the_reserve_without_making_it_a_mission_item()
    {
        GameWorld world = Room(Survival(), level => level.Items.Add(new LevelItem
        {
            Id = "box", Sprite = "item.ammo", Position = new Vector2(4, 4), Loot = [new LootEntry { Ammo = "9mm", Amount = 8 }],
        }));

        Run(world, 0.5f, new PlayerCommands { Move = Vector2.UnitX, AimTarget = world.Player.Position + new Vector2(100, 0) });

        Assert.Equal(8, world.Inventory.AmmoOf("9mm"));
        Assert.False(world.Inventory.Has("box"));
    }
}
