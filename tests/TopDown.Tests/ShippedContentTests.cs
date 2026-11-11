using TopDown.Content;
using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Tests;

/// <summary>Checks the content shipped in the repository (Content/ and Mods/).</summary>
public class ShippedContentTests
{
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

    [Fact]
    public void Base_content_has_no_errors_or_warnings()
    {
        ContentDatabase database = Load();

        Assert.Empty(database.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Select(d => d.ToString()));
        Assert.NotEmpty(database.PlayableCampaign);
    }

    [Fact]
    public void Every_base_level_builds_and_runs()
    {
        ContentDatabase database = Load();
        foreach (var level in database.Levels.Values)
        {
            GameWorld world = GameWorld.Create(level, database.Definitions);
            for (int i = 0; i < 240; i++) world.Step(1f / 120f, default);
            Assert.True(world.Player.IsAlive || world.Outcome == WorldOutcome.Defeat);
        }
    }

    [Fact]
    public void Horror_mod_loads_without_errors_or_warnings()
    {
        ContentDatabase database = Load("noct_inspired");

        Assert.Empty(database.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Select(d => d.ToString()));
        Assert.Equal(["base", "noct_inspired"], database.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Equal("thermal", database.Theme.Id);
        Assert.False(database.Game.Rules.Score.Enabled);
        Assert.Equal(2, database.PlayableCampaign.Count);
        // Partial override: magazine changed, everything else inherited from the base pack.
        Assert.Equal(7, database.Definitions.Weapon("pistol").MagazineSize);
        Assert.Equal(4f, database.Definitions.Weapon("pistol").FireRate);
        Assert.Equal("noct_inspired", database.Assets.Textures["actor.player"].PackId);
        Assert.Equal("SIGNAL PERDU", database.CreateLocalizer("fr")["game.over"]);
    }

    [Fact]
    public void Every_horror_level_builds_and_runs()
    {
        ContentDatabase database = Load("noct_inspired");
        foreach (string id in database.Game.Campaign)
        {
            GameWorld world = GameWorld.Create(database.Levels[id], database.Definitions);
            for (int i = 0; i < 240; i++) world.Step(1f / 120f, default);
        }
    }

    [Fact]
    public void The_player_starts_stops_and_turns_around_almost_instantly()
    {
        ContentDatabase database = Load();
        GameWorld world = GameWorld.Create(database.Levels["arena"], database.Definitions);
        Actor player = world.Player;
        const float dt = 1f / 120f;
        var right = new PlayerCommands { Move = System.Numerics.Vector2.UnitX, AimTarget = player.Position + new System.Numerics.Vector2(100, 0) };
        var left = right with { Move = -System.Numerics.Vector2.UnitX };

        float StepsUntil(PlayerCommands commands, Func<float, bool> done)
        {
            int steps = 0;
            while (!done(player.Velocity.X) && steps < 120)
            {
                world.Step(dt, commands);
                steps++;
            }

            return steps * dt;
        }

        float speed = player.Definition.Speed;
        Assert.True(StepsUntil(right, v => v >= speed * 0.99f) <= 0.04f, "time to full speed");
        Assert.True(StepsUntil(left, v => v <= -speed * 0.99f) <= 0.06f, "time to turn around");
        Assert.True(StepsUntil(default, v => MathF.Abs(v) < 1f) <= 0.04f, "time to stop");
    }

    [Fact]
    public void The_test_room_has_the_expected_setup()
    {
        ContentDatabase database = Load();
        GameWorld world = GameWorld.Create(database.Levels["arena"], database.Definitions);

        Assert.Null(world.Player.Weapon);
        Assert.Equal(3, world.Actors.Count(a => !a.IsPlayer && a.Weapon is null && a.Definition.Anatomy == "human"));
        Assert.Contains(world.Actors, a => a.Definition.Anatomy == "quadruped");
        Assert.Contains(world.Weapons, w => !w.Weapon.Definition.IsFirearm);
        Assert.Contains(world.Weapons, w => w.Weapon.Definition.IsFirearm);
        Assert.Contains(database.PlayableExtraLevels, level => level.Id == "arena");
    }

    [Theory]
    [InlineData("bat")]
    [InlineData("pipe")]
    [InlineData("machete")]
    public void Long_melee_weapons_hit_where_the_knife_cannot(string weaponId)
    {
        ContentDatabase database = Load();
        GameWorld world = GameWorld.Create(database.Levels["arena"], database.Definitions);
        Actor player = world.Player;
        Actor dummy = world.SpawnActor(database.Definitions.Actor("thug"), false, player.Position, MathF.PI);
        // A gap of about one body between the two characters, straight ahead.
        dummy.Position = player.Position + System.Numerics.Vector2.UnitX * (player.Radius + dummy.Radius + 22f);

        world.Combat.ResolveMelee(player, database.Definitions.Weapon("knife"));
        Assert.DoesNotContain(world.Events.OfType<ActorHit>(), hit => hit.Target == dummy);

        world.Combat.ResolveMelee(player, database.Definitions.Weapon(weaponId));
        Assert.Contains(world.Events.OfType<ActorHit>(), hit => hit.Target == dummy);
    }

    [Fact]
    public void Guns_and_blades_can_leave_men_crawling_but_not_dogs()
    {
        ContentDatabase database = Load();
        DefinitionSet definitions = database.Definitions;

        Assert.All(new[] { "guard", "thug", "brute" }, id => Assert.NotNull(definitions.Actor(id).Wound));
        Assert.All(new[] { "guard", "thug", "brute" }, id => Assert.NotNull(definitions.Actor(id).Sprites.Crawl));
        Assert.Null(definitions.Actor("dog").Wound);
        Assert.Null(definitions.Actor("player").Wound);
        Assert.All(new[] { "pistol", "smg", "knife" }, id => Assert.InRange(definitions.Weapon(id).WoundChance, 0.1f, 0.5f));
        Assert.Equal(0f, definitions.Weapon("bat").WoundChance);
    }

    [Fact]
    public void Every_text_exists_in_french_and_english()
    {
        ContentDatabase database = Load("noct_inspired");
        var french = database.Locales["fr"];
        var english = database.Locales["en"];

        Assert.Empty(english.Keys.Except(french.Keys));
        Assert.Empty(french.Keys.Except(english.Keys));
    }
}
