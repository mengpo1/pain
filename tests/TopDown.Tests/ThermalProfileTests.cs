using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TopDown.Content;
using TopDown.Content.Json;
using TopDown.Core.Definitions;

namespace TopDown.Tests;

/// <summary>
/// The thermal profiles (noct, signal) load their definitions on top of the base pack, field by field.
/// Retuning the action pack must not reach them: every value they actually load (definitions, rules,
/// camera, feel, theme, animations, levels) is compared with a snapshot taken from the content as it
/// was before the action pack was calibrated (commit 2285c08).
/// </summary>
public class ThermalProfileTests
{
    private static readonly (string Profile, string Mod)[] Profiles = [("noct", "noct_prototype"), ("signal", "noct_inspired")];

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

    private static string SnapshotPath => Path.Combine(RepositoryRoot, "tests", "TopDown.Tests", "Snapshots", "thermal_profiles.json");

    private static ContentDatabase Load(string root, string mod) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(root, "Content"),
        ModsRoot = Path.Combine(root, "Mods"),
        EnabledMods = [mod],
    });

    /// <summary>One entry per loaded value ("weapon:rifle", "game", "animation:actor.player.getup"…) and a hash of its JSON.</summary>
    internal static SortedDictionary<string, string> Fingerprint(ContentDatabase content)
    {
        var options = new JsonSerializerOptions(ContentJson.Options) { WriteIndented = false };
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Add(string key, object? value)
        {
            string json = JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), options);
            result[key] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
        }

        DefinitionSet definitions = content.Definitions;
        foreach ((string id, WeaponDefinition weapon) in definitions.Weapons) Add($"weapon:{id}", weapon);
        foreach ((string id, ActorDefinition actor) in definitions.Actors) Add($"actor:{id}", actor);
        foreach ((string id, TileDefinition tile) in definitions.Tiles) Add($"tile:{id}", tile);
        foreach ((string id, DoorDefinition door) in definitions.Doors) Add($"door:{id}", door);
        foreach ((string id, ModifierDefinition modifier) in definitions.Modifiers) Add($"modifier:{id}", modifier);
        foreach ((string id, LevelDefinition level) in content.Levels) Add($"level:{id}", level);
        foreach ((string id, AnimationDefinition animation) in content.Assets.Animations) Add($"animation:{id}", animation);
        Add("rules", definitions.Rules);
        Add("game", content.Game);
        Add("theme", content.Theme);
        return result;
    }

    [Fact]
    public void The_thermal_profiles_load_exactly_what_they_loaded_before_the_calibration()
    {
        JsonObject snapshot = JsonNode.Parse(File.ReadAllText(SnapshotPath))!.AsObject();
        var differences = new List<string>();
        foreach ((string profile, string mod) in Profiles)
        {
            SortedDictionary<string, string> now = Fingerprint(Load(RepositoryRoot, mod));
            JsonObject before = snapshot[profile]!.AsObject();
            foreach ((string key, JsonNode? hash) in before)
            {
                if (!now.TryGetValue(key, out string? current)) differences.Add($"{profile}: {key} no longer loaded");
                else if (current != hash!.GetValue<string>()) differences.Add($"{profile}: {key} changed");
            }

            foreach (string key in now.Keys.Where(k => !before.ContainsKey(k))) differences.Add($"{profile}: {key} is new");
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    /// <summary>
    /// Writes the snapshot from another copy of the repository, when asked:
    /// THERMAL_SNAPSHOT_FROM=/path/to/a/checkout/of/2285c08 dotnet test --filter Write_the_snapshot
    /// </summary>
    [Fact]
    public void Write_the_snapshot_when_asked()
    {
        string? from = Environment.GetEnvironmentVariable("THERMAL_SNAPSHOT_FROM");
        if (string.IsNullOrEmpty(from)) return;

        var snapshot = new JsonObject { ["source"] = "Content and Mods of commit 2285c08, before the action pack was calibrated" };
        foreach ((string profile, string mod) in Profiles)
        {
            var entries = new JsonObject();
            foreach ((string key, string hash) in Fingerprint(Load(from, mod))) entries[key] = hash;
            snapshot[profile] = entries;
        }

        File.WriteAllText(SnapshotPath, snapshot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    [Theory]
    [InlineData("noct_prototype")]
    [InlineData("noct_inspired")]
    [InlineData(null)]
    public void Every_human_stands_up_before_it_can_act_again(string? mod)
    {
        ContentDatabase content = mod is null
            ? ContentLoader.Load(new ContentLoadOptions { ContentRoot = Path.Combine(RepositoryRoot, "Content"), ModsRoot = Path.Combine(RepositoryRoot, "Mods") })
            : Load(RepositoryRoot, mod);

        foreach ((string id, ActorDefinition actor) in content.Definitions.Actors)
        {
            string? set = actor.Sprites.AnimationSet;
            if (set is null || !content.Assets.Animations.TryGetValue($"{set}.getup", out AnimationDefinition? getUp)) continue;
            // The last frame holds the standing pose; it must be reached when the state ends.
            float standing = getUp.Durations.Take(getUp.Durations.Count - 1).Sum();
            Assert.True(standing <= actor.GetUpDuration + 0.001f,
                $"{mod ?? "base"}: {id} stands after {standing:0.###} s but acts again after {actor.GetUpDuration} s");
        }
    }
}
