using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Tests;

public class ModTests
{
    [Fact]
    public void Example_mod_adds_changes_and_replaces_definitions()
    {
        ContentDatabase content = ShippedContentTests.Load("horizon", "horizon_arsenal");
        Assert.Empty(content.Diagnostics.Errors);
        Assert.Equal(["horizon", "horizon_arsenal"], content.LoadedPacks.Select(p => p.Manifest.Id));

        Assert.True(content.Rules.Items.ContainsKey("rail_rifle"));
        Assert.Equal(30, content.Rules.Items["k7_clip"].Rounds);             // merged: one value changed
        Assert.Equal(20, ShippedContentTests.Load("horizon").Rules.Items["k7_clip"].Rounds);
        Assert.Equal(46, content.Rules.Items["pulse_cell"].Damage!.Power);   // replaced as a whole
        UnitDefinition heavy = content.Rules.Units["operative_heavy"];
        Assert.Equal("rail_rifle", heavy.Loadout[0].Item);                   // arrays are replaced
        Assert.Equal(58, heavy.Stats["tu"]);                                 // other values kept
        Assert.EndsWith("warden_red.png", content.Assets.Textures["texture.unit.warden"].Path);
        Assert.Equal("Fusil à rail", content.CreateLocalizer("fr").Get("item.rail_rifle"));
        Assert.Equal("Fusil K-7", content.CreateLocalizer("fr").Get("item.k7_rifle"));
    }

    [Fact]
    public void Mod_made_for_another_game_is_refused_with_a_clear_message()
    {
        ContentDatabase content = ShippedContentTests.Load("mistvale", "horizon_arsenal");
        Assert.Equal(["mistvale"], content.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Contains(content.Diagnostics.Errors, d => d.Source == "mod:horizon_arsenal" && d.Message.Contains("'horizon'", StringComparison.Ordinal));
        Assert.False(content.Rules.Items.ContainsKey("rail_rifle"));
    }

    [Fact]
    public void Enabled_mod_that_is_not_installed_is_reported()
    {
        ContentDatabase content = ShippedContentTests.Load("horizon", "nonexistent");
        Assert.Contains(content.Diagnostics.Errors, d => d.Source == "mod:nonexistent");
    }
}

/// <summary>Loader behaviour on small packs written to a temporary folder.</summary>
public sealed class LoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "opentactics-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative, string json)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private ContentDatabase Load(params string[] mods) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(_root, "Content"),
        ModsRoot = Path.Combine(_root, "Mods"),
        GamePack = "game",
        EnabledMods = mods,
    });

    private void MinimalGame()
    {
        Write("Content/game/manifest.json", """{ "id": "game", "name": "Test" }""");
        Write("Content/game/items/items.json", """
            [ { "id": "stone", "nameKey": "item.stone", "kind": "none" },
              { "id": "clip", "nameKey": "item.clip", "kind": "ammo", "rounds": 5, "damage": { "power": 10, "type": "ap" } } ]
            """);
        Write("Content/game/damageTypes/d.json", """{ "id": "ap" }""");
    }

    [Fact]
    public void Unknown_property_is_an_error_naming_the_file()
    {
        MinimalGame();
        Write("Content/game/items/typo.json", """{ "id": "gun", "kind": "firearm", "acuracy": 5 }""");
        ContentDatabase content = Load();
        ContentDiagnostic error = Assert.Single(content.Diagnostics.Errors, d => d.Source.Contains("typo.json", StringComparison.Ordinal));
        Assert.Contains("acuracy", error.Message, StringComparison.Ordinal);
        Assert.False(content.Rules.Items.ContainsKey("gun"));
    }

    [Fact]
    public void Broken_reference_is_reported()
    {
        MinimalGame();
        Write("Content/game/items/gun.json", """{ "id": "gun", "kind": "firearm", "ammo": ["missing_clip"], "snap": { "accuracy": 50, "tu": 20 } }""");
        ContentDatabase content = Load();
        Assert.Contains(content.Diagnostics.Errors, d => d.Source == "item 'gun'" && d.Message.Contains("missing_clip", StringComparison.Ordinal));
    }

    [Fact]
    public void Mod_can_delete_a_definition()
    {
        MinimalGame();
        Write("Mods/cleaner/manifest.json", """{ "id": "cleaner", "dependencies": ["game"] }""");
        Write("Mods/cleaner/items/x.json", """{ "id": "stone", "$delete": true }""");
        Assert.False(Load("cleaner").Rules.Items.ContainsKey("stone"));
        Assert.True(Load().Rules.Items.ContainsKey("stone"));
    }

    [Fact]
    public void Mods_load_after_their_dependencies_whatever_the_requested_order()
    {
        MinimalGame();
        Write("Mods/a/manifest.json", """{ "id": "a", "dependencies": ["b"] }""");
        Write("Mods/a/items/x.json", """{ "id": "clip", "rounds": 7 }""");
        Write("Mods/b/manifest.json", """{ "id": "b" }""");
        Write("Mods/b/items/x.json", """{ "id": "clip", "rounds": 6 }""");
        ContentDatabase content = Load("a", "b");
        Assert.Equal(["game", "b", "a"], content.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Equal(7, content.Rules.Items["clip"].Rounds);
    }

    [Fact]
    public void Circular_dependencies_are_refused()
    {
        MinimalGame();
        Write("Mods/a/manifest.json", """{ "id": "a", "dependencies": ["b"] }""");
        Write("Mods/b/manifest.json", """{ "id": "b", "dependencies": ["a"] }""");
        ContentDatabase content = Load("a", "b");
        Assert.Equal(["game"], content.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Contains(content.Diagnostics.Errors, d => d.Message.Contains("Circular", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_json_is_reported_not_thrown()
    {
        MinimalGame();
        Write("Content/game/items/bad.json", """{ "id": "x", """);
        ContentDatabase content = Load();
        Assert.Contains(content.Diagnostics.Errors, d => d.Source.EndsWith("bad.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_game_pack_throws_a_clear_exception()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Content"));
        ContentLoadException exception = Assert.Throws<ContentLoadException>(() => Load());
        Assert.Contains("game", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mission_with_errors_is_unplayable_but_the_rest_loads()
    {
        MinimalGame();
        Write("Content/game/missions/m.json", """{ "id": "broken", "map": { "block": "nowhere" }, "player": [ { "unit": "nobody" } ] }""");
        ContentDatabase content = Load();
        Assert.Contains("broken", content.InvalidMissions);
        Assert.True(content.Rules.Items.ContainsKey("stone"));
    }
}

public class LocalizationTests
{
    private static Localizer Texts(string language) => new(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en"] = new Dictionary<string, string> { ["a"] = "Apple", ["hello"] = "Hello {name}", ["turns.one"] = "{count} turn", ["turns.other"] = "{count} turns", ["only.en"] = "English" },
        ["fr"] = new Dictionary<string, string> { ["a"] = "Pomme", ["hello"] = "Bonjour {name}", ["turns.one"] = "{count} tour", ["turns.other"] = "{count} tours" },
    }, language, "en");

    [Fact]
    public void Missing_text_falls_back_to_the_default_language_then_to_the_key()
    {
        Localizer fr = Texts("fr");
        var missing = new List<string>();
        fr.MissingKey += missing.Add;
        Assert.Equal("Pomme", fr.Get("a"));
        Assert.Equal("English", fr.Get("only.en"));
        Assert.Equal("nothing.here", fr.Get("nothing.here"));
        Assert.Equal(2, missing.Count);
    }

    [Fact]
    public void Parameters_and_plurals_follow_the_language()
    {
        Assert.Equal("Bonjour Ana", Texts("fr").Format("hello", ("name", "Ana")));
        Assert.Equal("1 tour", Texts("fr").Format("turns", ("count", 1)));
        Assert.Equal("0 tour", Texts("fr").Format("turns", ("count", 0)));   // French: 0 is singular
        Assert.Equal("0 turns", Texts("en").Format("turns", ("count", 0)));
        Assert.Equal("3 tours", Texts("fr").Format("turns", ("count", 3)));
    }

    [Fact]
    public void Unknown_language_uses_the_fallback()
    {
        Assert.Equal("en", Texts("de").Language);
    }
}
