using Game.Core.Definitions;

namespace Game.Core.Content;

/// <summary>Everything loaded from the game pack and the enabled mods, merged and validated.</summary>
public sealed class ContentDatabase
{
    internal ContentDatabase(
        IReadOnlyList<ContentPack> loadedPacks,
        IReadOnlyList<ContentPack> installedMods,
        Ruleset rules,
        AssetCatalog assets,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales,
        GameSettings game,
        DiagnosticList diagnostics)
    {
        LoadedPacks = loadedPacks;
        InstalledMods = installedMods;
        Rules = rules;
        Assets = assets;
        Locales = locales;
        Game = game;
        Diagnostics = diagnostics;
    }

    /// <summary>Game pack first, then enabled mods in load order (later wins).</summary>
    public IReadOnlyList<ContentPack> LoadedPacks { get; }

    /// <summary>Every mod found in the mods folder, enabled or not.</summary>
    public IReadOnlyList<ContentPack> InstalledMods { get; }

    public ContentPack GamePack => LoadedPacks[0];
    public Ruleset Rules { get; }
    public AssetCatalog Assets { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales { get; }
    public GameSettings Game { get; }
    public DiagnosticList Diagnostics { get; }

    /// <summary>Missions that failed validation and must not be started.</summary>
    public HashSet<string> InvalidMissions { get; } = new(StringComparer.Ordinal);

    /// <summary>Menu missions that exist and are valid, in order.</summary>
    public IReadOnlyList<MissionDefinition> PlayableMissions =>
        Game.Missions.Where(id => Rules.Missions.ContainsKey(id) && !InvalidMissions.Contains(id)).Select(id => Rules.Missions[id]).ToList();

    public Localizer CreateLocalizer(string? language) =>
        new(Locales, language ?? Game.DefaultLanguage, Game.FallbackLanguage);
}
