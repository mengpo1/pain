using TopDown.Content;
using TopDown.Core.Definitions;

namespace TopDown.Tests;

/// <summary>A throw-away content folder with a minimal valid base pack.</summary>
internal sealed class TempContent : IDisposable
{
    public TempContent()
    {
        Root = Path.Combine(Path.GetTempPath(), "topdown-tests-" + Guid.NewGuid().ToString("N"));
        Write("Content/Base/manifest.json", """{ "id": "base", "name": "Base", "version": "1.0.0", "formatVersion": 1 }""");
        Write("Content/Base/assets.json", """
            {
              "textures": { "weapon.pistol": "assets/pistol.png", "actor.player": "assets/player.png", "tile.floor": "assets/floor.png" },
              "sounds": { "sound.shot": "assets/shot.wav" },
              "fonts": { "font.ui": "assets/ui.ttf" }
            }
            """);
        foreach (string file in new[] { "pistol.png", "player.png", "floor.png", "shot.wav", "ui.ttf" }) Write($"Content/Base/assets/{file}", "x");
        Write("Content/Base/weapons/weapons.json", """
            [
              { "id": "unarmed", "kind": "melee", "canDrop": false, "damage": 0, "knockdown": true },
              // Comments are allowed in content files.
              { "id": "pistol", "kind": "firearm", "spriteId": "weapon.pistol", "damage": 100, "magazineSize": 12, "fireRate": 4, "range": 800, "sounds": { "attack": "sound.shot" } }
            ]
            """);
        Write("Content/Base/characters/player.json", """{ "id": "player", "faction": "player", "sprites": { "body": "actor.player" } }""");
        Write("Content/Base/enemies/guard.json", """{ "id": "guard", "weaponId": "pistol", "sprites": { "body": "actor.player" } }""");
        Write("Content/Base/tiles/tiles.json", """
            [
              { "id": "floor", "spriteId": "tile.floor" },
              { "id": "wall", "spriteId": "tile.floor", "layer": "wall", "blocksMovement": true, "blocksProjectiles": true, "blocksVision": true }
            ]
            """);
        Write("Content/Base/levels/one.json", """
            {
              "legend": { "#": "wall", ".": "floor" },
              "map": [ "#####", "#...#", "#####" ],
              "player": { "position": [1, 1] },
              "actors": [ { "definition": "guard", "position": [3, 1] } ],
              "objectives": [ { "type": "eliminateAll", "textKey": "objective.kill" } ]
            }
            """);
        Write("Content/Base/locales/en.json", """{ "game.title": "Test", "objective": { "kill": "Kill them" } }""");
        Write("Content/Base/locales/fr.json", """{ "game.title": "Essai", "objective": { "kill": "Éliminez-les" } }""");
        Write("Content/Base/themes/default.json", """{ "id": "default" }""");
        Write("Content/Base/game.json", """{ "campaign": ["one"], "theme": "default", "defaultLanguage": "en", "fallbackLanguage": "en" }""");
        Directory.CreateDirectory(Path.Combine(Root, "Mods"));
    }

    public string Root { get; }

    public void Write(string relativePath, string text)
    {
        string path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public void Delete(string relativePath) => File.Delete(Path.Combine(Root, relativePath));

    public void Mod(string folder, string id, string dependencies = "", int formatVersion = 1) =>
        Write($"Mods/{folder}/manifest.json",
            $$"""{ "id": "{{id}}", "name": "{{id}}", "version": "1.0.0", "formatVersion": {{formatVersion}}, "dependencies": [{{dependencies}}] }""");

    public ContentDatabase Load(params string[] mods) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(Root, "Content"),
        ModsRoot = Path.Combine(Root, "Mods"),
        EnabledMods = mods,
    });

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary files: leaving them behind is harmless.
        }
    }
}

public class ContentTests
{
    [Fact]
    public void Base_pack_loads_definitions_and_levels()
    {
        using var content = new TempContent();
        ContentDatabase database = content.Load();

        Assert.False(database.Diagnostics.HasErrors, string.Join("\n", database.Diagnostics.Items));
        Assert.Equal(100f, database.Definitions.Weapon("pistol").Damage);
        Assert.Equal(WeaponKind.Firearm, database.Definitions.Weapon("pistol").Kind);
        Assert.Equal("one", database.Levels["one"].Id);
        Assert.Single(database.PlayableCampaign);
        Assert.True(database.Assets.HasSprite("weapon.pistol"));
    }

    [Fact]
    public void Loaded_level_can_be_played()
    {
        using var content = new TempContent();
        ContentDatabase database = content.Load();

        var world = TopDown.Core.World.GameWorld.Create(database.Levels["one"], database.Definitions);

        Assert.Equal(2, world.Actors.Count);
        Assert.Equal("pistol", world.Actors[1].Weapon!.Definition.Id);
    }

    [Fact]
    public void Unknown_properties_are_reported()
    {
        using var content = new TempContent();
        content.Write("Content/Base/weapons/typo.json", """{ "id": "bat", "kind": "melee", "spriteId": "weapon.pistol", "damge": 100 }""");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Errors, d => d.Source.Contains("typo.json") && d.Message.Contains("damge"));
        Assert.False(database.Definitions.Weapons.ContainsKey("bat"));
    }

    [Fact]
    public void Broken_json_is_reported_with_its_file()
    {
        using var content = new TempContent();
        content.Write("Content/Base/weapons/broken.json", """{ "id": "bat", """);

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Errors, d => d.Source.EndsWith("broken.json"));
    }

    [Fact]
    public void Invalid_references_are_reported()
    {
        using var content = new TempContent();
        content.Write("Content/Base/enemies/thug.json", """{ "id": "thug", "weaponId": "chainsaw", "sprites": { "body": "actor.player" } }""");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Errors, d => d.Source == "actor:thug" && d.Message.Contains("chainsaw"));
    }

    [Fact]
    public void Levels_with_errors_are_not_playable()
    {
        using var content = new TempContent();
        content.Write("Content/Base/levels/two.json", """
            { "legend": { ".": "lava" }, "map": [ "..." ], "player": { "position": [0, 0] }, "objectives": [ { "type": "survive", "duration": 5 } ] }
            """);
        content.Write("Content/Base/game.json", """{ "campaign": ["one", "two"], "theme": "default" }""");

        ContentDatabase database = content.Load();

        Assert.Contains("two", database.InvalidLevels);
        Assert.Contains(database.Diagnostics.Errors, d => d.Source == "level:two" && d.Message.Contains("lava"));
        Assert.Single(database.PlayableCampaign);
    }

    [Fact]
    public void Missing_asset_files_produce_a_warning()
    {
        using var content = new TempContent();
        content.Delete("Content/Base/assets/pistol.png");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Warnings, d => d.Message.Contains("weapon.pistol") && d.Message.Contains("not found"));
        Assert.False(database.Diagnostics.HasErrors);
    }

    [Fact]
    public void Unknown_sprite_ids_produce_a_warning()
    {
        using var content = new TempContent();
        content.Write("Content/Base/weapons/bat.json", """{ "id": "bat", "kind": "melee", "spriteId": "weapon.bat" }""");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Warnings, d => d.Source == "weapon:bat" && d.Message.Contains("weapon.bat"));
    }

    [Fact]
    public void A_mod_patches_only_the_values_it_provides()
    {
        using var content = new TempContent();
        content.Mod("Balance", "balance");
        content.Write("Mods/Balance/weapons/pistol.json", """{ "id": "pistol", "damage": 50 }""");

        ContentDatabase database = content.Load("balance");

        WeaponDefinition pistol = database.Definitions.Weapon("pistol");
        Assert.Equal(50f, pistol.Damage);
        Assert.Equal(12, pistol.MagazineSize);
        Assert.Equal("sound.shot", pistol.Sounds.Attack);
    }

    [Fact]
    public void A_mod_adds_new_content()
    {
        using var content = new TempContent();
        content.Mod("Arsenal", "arsenal");
        content.Write("Mods/Arsenal/weapons/crossbow.json", """{ "id": "crossbow", "kind": "firearm", "spriteId": "weapon.pistol", "magazineSize": 1, "projectileSpeed": 900 }""");

        ContentDatabase database = content.Load("arsenal");

        Assert.Equal(900f, database.Definitions.Weapon("crossbow").ProjectileSpeed);
    }

    [Fact]
    public void A_mod_replaces_a_sprite_without_touching_gameplay_files()
    {
        using var content = new TempContent();
        content.Mod("Reskin", "reskin");
        content.Write("Mods/Reskin/assets/new_pistol.png", "x");
        content.Write("Mods/Reskin/assets.json", """{ "textures": { "weapon.pistol": "assets/new_pistol.png" } }""");

        ContentDatabase database = content.Load("reskin");

        AssetEntry entry = database.Assets.Textures["weapon.pistol"];
        Assert.Equal("reskin", entry.PackId);
        Assert.EndsWith(Path.Combine("Reskin", "assets", "new_pistol.png"), entry.Path);
    }

    [Fact]
    public void Installed_but_disabled_mods_are_not_loaded()
    {
        using var content = new TempContent();
        content.Mod("Balance", "balance");
        content.Write("Mods/Balance/weapons/pistol.json", """{ "id": "pistol", "damage": 50 }""");

        ContentDatabase database = content.Load();

        Assert.Equal(100f, database.Definitions.Weapon("pistol").Damage);
        Assert.Single(database.InstalledMods);
        Assert.Single(database.LoadedPacks);
    }

    [Fact]
    public void Later_mods_win_and_conflicts_are_reported()
    {
        using var content = new TempContent();
        content.Mod("A", "mod_a");
        content.Mod("B", "mod_b");
        content.Write("Mods/A/weapons/pistol.json", """{ "id": "pistol", "damage": 10, "fireRate": 9 }""");
        content.Write("Mods/B/weapons/pistol.json", """{ "id": "pistol", "damage": 20 }""");

        ContentDatabase database = content.Load("mod_a", "mod_b");

        WeaponDefinition pistol = database.Definitions.Weapon("pistol");
        Assert.Equal(20f, pistol.Damage);
        Assert.Equal(9f, pistol.FireRate);
        Assert.Contains(database.Diagnostics.Warnings, d => d.Message.Contains("mod_a") && d.Message.Contains("mod_b") && d.Message.Contains("pistol"));
    }

    [Fact]
    public void Dependencies_are_loaded_first_and_do_not_conflict()
    {
        using var content = new TempContent();
        content.Mod("Core", "core_mod");
        content.Mod("Addon", "addon", dependencies: "\"core_mod\"");
        content.Write("Mods/Core/weapons/pistol.json", """{ "id": "pistol", "damage": 10 }""");
        content.Write("Mods/Addon/weapons/pistol.json", """{ "id": "pistol", "damage": 30 }""");

        // Enabled in the "wrong" order: the dependency must still load first.
        ContentDatabase database = content.Load("addon", "core_mod");

        Assert.Equal(["base", "core_mod", "addon"], database.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Equal(30f, database.Definitions.Weapon("pistol").Damage);
        Assert.DoesNotContain(database.Diagnostics.Warnings, d => d.Message.Contains("both modify"));
    }

    [Fact]
    public void Mods_with_missing_dependencies_are_skipped()
    {
        using var content = new TempContent();
        content.Mod("Addon", "addon", dependencies: "\"missing\"");
        content.Write("Mods/Addon/weapons/pistol.json", """{ "id": "pistol", "damage": 30 }""");

        ContentDatabase database = content.Load("addon");

        Assert.Contains(database.Diagnostics.Errors, d => d.Source == "mod:addon" && d.Message.Contains("missing"));
        Assert.Equal(100f, database.Definitions.Weapon("pistol").Damage);
    }

    [Fact]
    public void Mods_with_an_unsupported_format_are_skipped()
    {
        using var content = new TempContent();
        content.Mod("Future", "future", formatVersion: 99);

        ContentDatabase database = content.Load("future");

        Assert.Contains(database.Diagnostics.Errors, d => d.Source == "mod:future" && d.Message.Contains("99"));
        Assert.Single(database.LoadedPacks);
    }

    [Fact]
    public void Enabling_a_mod_that_is_not_installed_is_reported()
    {
        using var content = new TempContent();

        ContentDatabase database = content.Load("ghost");

        Assert.Contains(database.Diagnostics.Errors, d => d.Source == "mod:ghost");
    }

    [Fact]
    public void Mods_can_add_and_override_texts()
    {
        using var content = new TempContent();
        content.Mod("Texts", "texts");
        content.Write("Mods/Texts/locales/en.json", """{ "game.title": "Modded", "mod.new": "New text" }""");
        content.Write("Mods/Texts/locales/de.json", """{ "game.title": "Versuch" }""");

        ContentDatabase database = content.Load("texts");
        Localizer english = database.CreateLocalizer("en");

        Assert.Equal("Modded", english["game.title"]);
        Assert.Equal("New text", english["mod.new"]);
        Assert.Equal("Kill them", english["objective.kill"]);
        Assert.Equal("Versuch", database.CreateLocalizer("de")["game.title"]);
    }

    [Fact]
    public void Missing_translations_are_listed()
    {
        using var content = new TempContent();
        content.Write("Content/Base/locales/fr.json", """{ "game.title": "Essai" }""");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Warnings, d => d.Source == "locale:fr" && d.Message.Contains("objective.kill"));
    }

    [Fact]
    public void Missing_base_pack_is_fatal()
    {
        using var content = new TempContent();
        Directory.Delete(Path.Combine(content.Root, "Content", "Base"), recursive: true);

        Assert.Throws<ContentLoadException>(() => content.Load());
    }
}
