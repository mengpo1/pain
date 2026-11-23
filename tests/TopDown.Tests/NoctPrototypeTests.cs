using System.Numerics;
using TopDown.Content;
using TopDown.Content.Json;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Tests;

/// <summary>The NoctPrototype pack as shipped: loading, creatures, the "Last Signal" mission, and coexistence with the action game.</summary>
public class NoctPrototypeTests
{
    private const string PackId = "noct_prototype";
    private const float Dt = 1f / 120f;

    private static string RepositoryRoot
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null && !File.Exists(Path.Combine(directory, "TopDown.slnx")))
                directory = Path.GetDirectoryName(directory);
            return directory ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    private static ContentDatabase Load(params string[] mods) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(RepositoryRoot, "Content"),
        ModsRoot = Path.Combine(RepositoryRoot, "Mods"),
        EnabledMods = mods,
    });

    private static readonly Lazy<ContentDatabase> Noct = new(() => Load(PackId));

    private static GameWorld Mission() => GameWorld.Create(Noct.Value.Levels["installation"], Noct.Value.Definitions);

    private static void Run(GameWorld world, float seconds, PlayerCommands commands = default)
    {
        for (int i = 0; i < (int)(seconds / Dt); i++)
        {
            world.Step(Dt, commands);
            commands.InteractPressed = false;
            commands.AttackPressed = false;
        }
    }

    private static void Teleport(GameWorld world, float x, float y)
    {
        world.Player.Position = world.Level.ToWorld(new Vector2(x, y));
        world.Player.PreviousPosition = world.Player.Position;
    }

    [Fact]
    public void The_pack_loads_without_errors_or_warnings()
    {
        ContentDatabase database = Noct.Value;

        Assert.Empty(database.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Select(d => d.ToString()));
        Assert.Equal(["base", PackId], database.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Equal("thermal_cam", database.Theme.Id);
        Assert.Equal(["installation"], database.PlayableCampaign.Select(l => l.Id));
        Assert.Equal(["noct_demo"], database.PlayableExtraLevels.Select(l => l.Id));
        Assert.False(database.Game.RestartWhileAlive);
        Assert.True(database.Game.Hud.Survival);
    }

    [Fact]
    public void The_level_builds_and_runs()
    {
        GameWorld world = Mission();
        Run(world, 3f);

        Assert.True(world.Player.IsAlive);
        Assert.Equal(16, world.Inventory.AmmoOf("9mm"));
        Assert.Equal(1, world.Inventory.Medkits);
        Assert.Equal("pistol", world.Player.Weapon!.Definition.Id);
        Assert.Equal("9mm", world.Player.Weapon.Definition.AmmoType);
    }

    [Fact]
    public void The_three_creatures_are_data_on_the_shared_systems()
    {
        DefinitionSet defs = Noct.Value.Definitions;
        ActorDefinition scavenger = defs.Actor("scavenger"), leaper = defs.Actor("leaper"), watcher = defs.Actor("watcher");

        Assert.All(new[] { scavenger, leaper, watcher }, creature =>
        {
            Assert.Equal("creature", creature.Faction);
            Assert.NotNull(creature.NaturalWeaponId);
            Assert.NotNull(creature.Sprites.Heat);
        });
        Assert.Null(scavenger.Ai.Leap);
        Assert.NotNull(leaper.Ai.Leap);
        Assert.Equal(AiBehavior.Ambush, watcher.Ai.Behavior);
        Assert.True(watcher.Sprites.Heat!.Rest < 0.5f && watcher.Sprites.Heat.Active >= 0.9f, "cold at rest, visible when moving");
        Assert.NotNull(watcher.Sounds.Idle);
        Assert.True(scavenger.Health < watcher.Health && leaper.Health < scavenger.Health);
    }

    [Fact]
    public void The_scavenger_hunts_the_player_through_the_gate()
    {
        GameWorld world = Mission();
        Actor scavenger = world.FindActor("scav_yard")!;
        scavenger.Brain!.AlertTo(world.Player);
        float before = Vector2.Distance(scavenger.Position, world.Player.Position);

        Run(world, 3f);

        Assert.Equal(AiState.Chase, scavenger.Brain.State);
        Assert.True(Vector2.Distance(scavenger.Position, world.Player.Position) < before - 100f);
    }

    [Fact]
    public void The_watcher_waits_until_the_player_comes_close()
    {
        GameWorld world = Mission();
        Actor watcher = world.FindActor("watcher_radio")!;
        Teleport(world, 26, 6);
        Run(world, 1f);
        Assert.Equal(AiState.Ambush, watcher.Brain!.State);

        Teleport(world, 29.5f, 5);
        Run(world, 0.2f);

        // It uncurls and heats up first (its awakening), then gives chase.
        Assert.Equal(AiState.Awakening, watcher.Brain.State);
        Run(world, watcher.Definition.Ai.WakeTime);
        Assert.Equal(AiState.Chase, watcher.Brain.State);
    }

    [Fact]
    public void Creatures_never_hurt_each_other()
    {
        GameWorld world = Mission();
        Actor scavenger = world.FindActor("scav_yard")!;
        Actor other = world.FindActor("scav_generator")!;
        other.Position = scavenger.Position + new Vector2(scavenger.Radius + other.Radius + 4f, 0f);
        scavenger.Facing = 0f;

        world.Combat.ResolveMelee(scavenger, world.Definitions.Weapon("scavenger_bite"));

        Assert.False(world.Combat.CanDamage(scavenger, other, DamageKind.Melee));
        Assert.Equal(other.Definition.Health, other.Health);
        Assert.True(world.Combat.CanDamage(scavenger, world.Player, DamageKind.Melee));
    }

    [Fact]
    public void Walking_over_an_ammo_box_and_searching_a_locker_fill_the_inventory()
    {
        GameWorld world = Mission();
        Teleport(world, 25, 13);
        Run(world, 0.1f);
        Assert.Equal(24, world.Inventory.AmmoOf("9mm"));

        Teleport(world, 15.6f, 12.4f);
        Run(world, 0.05f, new PlayerCommands { InteractPressed = true });
        Run(world, 0.05f, new PlayerCommands { InteractPressed = true });
        Assert.Equal(34, world.Inventory.AmmoOf("9mm"));
        Assert.False(world.Interactables.Single(i => i.Definition.Id == "office_locker").Available);
    }

    [Fact]
    public void The_whole_mission_can_be_completed()
    {
        GameWorld world = Mission();
        Run(world, 0.1f);
        Assert.Contains(world.Events.OfType<TransmissionRequested>(), t => t.TextKey == "noctproto.tx.signal");

        Teleport(world, 27, 15);
        Run(world, 0.1f);
        Assert.Equal(1, world.Objectives.CurrentIndex);

        // The watcher is dealt with off-screen: this test is about the mission flow.
        world.FindActor("watcher_radio")!.Brain!.Activate();
        world.Combat.ApplyHit(world.FindActor("watcher_radio")!, new Hit(1000f, "bullet", false, 0f, Vector2.UnitX, 0f, world.Player, HitMethod.Firearm));
        Teleport(world, 26.5f, 3.6f);
        Run(world, 0.1f);
        Assert.Equal(2, world.Objectives.CurrentIndex);

        world.ClearEvents();
        Run(world, 0.05f, new PlayerCommands { InteractPressed = true });
        Run(world, 0.05f);
        Assert.True(world.Inventory.Has("battery"));
        Assert.Equal(3, world.Objectives.CurrentIndex);
        Assert.Contains(world.Events.OfType<TransmissionRequested>(), t => t.TextKey == "noctproto.tx.battery");
        Actor leaper = Assert.Single(world.Events.OfType<ActorSpawned>()).Actor;
        Assert.Equal("leaper", leaper.Definition.Id);
        Assert.Equal(AiState.Chase, world.FindActor("scav_shed")!.Brain!.State);

        Teleport(world, 3, 28.5f);
        Run(world, 0.1f);
        Assert.Equal(WorldOutcome.Victory, world.Outcome);
    }

    [Fact]
    public void The_extraction_without_the_battery_is_not_enough()
    {
        GameWorld world = Mission();
        Teleport(world, 27, 15);
        Run(world, 0.1f);
        Teleport(world, 29, 5);
        Run(world, 0.1f);
        Teleport(world, 3, 28.5f);
        Run(world, 0.5f);

        Assert.Equal(WorldOutcome.Playing, world.Outcome);
        Assert.False(world.Objectives.AllComplete);
    }

    [Fact]
    public void After_death_the_mission_restarts_from_scratch()
    {
        GameWorld world = Mission();
        Teleport(world, 25, 13);
        Run(world, 0.1f);
        world.Combat.ApplyHit(world.Player, new Hit(1000f, "sharp", false, 0f, Vector2.UnitX, 0f, null, HitMethod.Melee));
        Assert.Equal(WorldOutcome.Defeat, world.Outcome);

        GameWorld again = Mission();

        Assert.Equal(WorldOutcome.Playing, again.Outcome);
        Assert.Equal(16, again.Inventory.AmmoOf("9mm"));
        Assert.All(again.Items, item => Assert.False(item.Collected));
        Assert.Equal(again.Player.Definition.Health, again.Player.Health);
    }

    [Fact]
    public void Every_text_of_the_pack_exists_in_french_and_english()
    {
        ContentDatabase database = Noct.Value;
        var french = database.Locales["fr"];
        var english = database.Locales["en"];
        Assert.Empty(english.Keys.Except(french.Keys));
        Assert.Empty(french.Keys.Except(english.Keys));

        LevelDefinition level = database.Levels["installation"];
        IEnumerable<string> keys = new[] { level.NameKey, database.Game.TitleKey }
            .Concat(level.Objectives.Select(o => o.TextKey))
            .Concat(level.Triggers.SelectMany(t => t.Actions).Select(a => a.TextKey).OfType<string>())
            .Concat(level.Items.Select(i => i.NameKey))
            .Concat(level.Interactables.Select(i => i.PromptKey))
            .Concat(level.Interactables.SelectMany(i => i.Loot).Select(l => l.NameKey).OfType<string>())
            .Concat(new[] { "scavenger", "leaper", "watcher" }.Select(id => database.Definitions.Actor(id).NameKey))
            .Concat(new[] { "ammo.9mm", "ammo.shells", "hud.transmission", "hud.reloading", "prompt.heal", "prompt.search" });
        foreach (string key in keys)
        {
            Assert.True(french.ContainsKey(key) || french.Keys.Any(k => k.StartsWith(key + ".", StringComparison.Ordinal)), $"fr: {key}");
            Assert.True(english.ContainsKey(key) || english.Keys.Any(k => k.StartsWith(key + ".", StringComparison.Ordinal)), $"en: {key}");
        }
    }

    [Fact]
    public void The_menu_offers_the_action_game_and_the_horror_prototype()
    {
        var node = ContentJson.ParseFile(Path.Combine(RepositoryRoot, "Content", "profiles.json"))!;
        List<ProfileDefinition> profiles = ContentJson.Deserialize<List<ProfileDefinition>>(node["profiles"]!);

        ProfileDefinition action = profiles.Single(p => p.Id == "action");
        ProfileDefinition noct = profiles.Single(p => p.Id == "noct");
        Assert.Empty(action.Mods);
        Assert.Equal([PackId], noct.Mods);

        ContentDatabase actionGame = Load([.. action.Mods]);
        ContentDatabase horror = Load([.. noct.Mods]);
        Assert.Equal("neon", actionGame.Theme.Id);
        Assert.Equal("thermal_cam", horror.Theme.Id);
        Assert.NotEqual(actionGame.PlayableCampaign.Select(l => l.Id), horror.PlayableCampaign.Select(l => l.Id));
    }

    [Fact]
    public void The_action_game_is_unchanged_by_the_pack()
    {
        ContentDatabase actionGame = Load();

        Assert.True(actionGame.Game.RestartWhileAlive);
        Assert.False(actionGame.Game.Hud.Survival);
        Assert.Null(actionGame.Definitions.Weapon("pistol").AmmoType);
        Assert.Null(actionGame.Definitions.Actor("player").Stamina);
        Assert.Equal(250f, actionGame.Definitions.Actor("player").Speed); // base value (calibrated), not the survivor's 165
        Assert.DoesNotContain(actionGame.Definitions.Actors.Keys, id => id is "scavenger" or "leaper" or "watcher");
    }
}
