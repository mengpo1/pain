using TopDown.Core.Definitions;

namespace TopDown.Content;

/// <summary>Everything loaded from the base pack and the enabled mods, already merged and validated.</summary>
public sealed class ContentDatabase
{
    internal ContentDatabase(
        IReadOnlyList<ContentPack> loadedPacks,
        IReadOnlyList<ContentPack> installedMods,
        DefinitionSet definitions,
        IReadOnlyDictionary<string, LevelDefinition> levels,
        IReadOnlyDictionary<string, ThemeDefinition> themes,
        AssetCatalog assets,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales,
        GameSettings game,
        DiagnosticList diagnostics)
    {
        LoadedPacks = loadedPacks;
        InstalledMods = installedMods;
        Definitions = definitions;
        Levels = levels;
        Themes = themes;
        Assets = assets;
        Locales = locales;
        Game = game;
        Diagnostics = diagnostics;
    }

    /// <summary>Base pack first, then enabled mods in load order (later wins).</summary>
    public IReadOnlyList<ContentPack> LoadedPacks { get; }

    /// <summary>Every mod found in the mods folder, enabled or not.</summary>
    public IReadOnlyList<ContentPack> InstalledMods { get; }

    public DefinitionSet Definitions { get; }
    public IReadOnlyDictionary<string, LevelDefinition> Levels { get; }
    public IReadOnlyDictionary<string, ThemeDefinition> Themes { get; }
    public AssetCatalog Assets { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales { get; }
    public GameSettings Game { get; }
    public DiagnosticList Diagnostics { get; }

    /// <summary>Levels that failed validation and must not be started.</summary>
    public HashSet<string> InvalidLevels { get; } = new();

    public ThemeDefinition Theme =>
        Themes.TryGetValue(Game.Theme, out ThemeDefinition? theme) ? theme : Themes.Values.FirstOrDefault() ?? new ThemeDefinition { Id = "fallback" };

    /// <summary>Campaign levels that exist and are valid, in order.</summary>
    public IReadOnlyList<LevelDefinition> PlayableCampaign =>
        Game.Campaign.Where(id => Levels.ContainsKey(id) && !InvalidLevels.Contains(id)).Select(id => Levels[id]).ToList();

    public Localizer CreateLocalizer(string? language) =>
        new(Locales, language ?? Game.DefaultLanguage, Game.FallbackLanguage);
}
