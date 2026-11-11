using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Core.Definitions;

namespace Game.Core.Content;

/// <summary>The game pack is missing or unreadable: nothing can be played.</summary>
public sealed class ContentLoadException(string message) : Exception(message);

public sealed class ContentLoadOptions
{
    /// <summary>Folder whose sub-folders are game packs (each with a manifest.json).</summary>
    public required string ContentRoot { get; init; }

    /// <summary>Id of the game pack to play (its folder in <see cref="ContentRoot"/>).</summary>
    public required string GamePack { get; init; }

    /// <summary>Folder where each sub-folder with a manifest is an installed mod.</summary>
    public string? ModsRoot { get; init; }

    /// <summary>Mod ids to load, in priority order (later wins).</summary>
    public IReadOnlyList<string> EnabledMods { get; init; } = [];
}

/// <summary>
/// Loads a game pack then the enabled mods, in a deterministic order (files sorted by name).
/// Definitions sharing an id are merged property by property, later packs winning; arrays and
/// plain values are replaced as a whole (see <see cref="DefinitionStore"/> for $replace/$delete).
/// </summary>
public static class ContentLoader
{
    private const string ManifestFile = "manifest.json";

    /// <summary>Content folder → definition kind. One JSON file holds one definition or an array of them.</summary>
    internal static readonly (string Folder, string Kind)[] DefinitionFolders =
    [
        ("stats", "stat"),
        ("damageTypes", "damageType"),
        ("shapes", "shape"),
        ("tiles", "tilePart"),
        ("maps", "mapBlock"),
        ("terrains", "terrain"),
        ("items", "item"),
        ("armors", "armor"),
        ("units", "unit"),
        ("inventory", "inventorySlot"),
        ("missions", "mission"),
    ];

    /// <summary>Game packs available in a content folder, sorted by id.</summary>
    public static List<ContentPack> DiscoverGamePacks(string contentRoot, DiagnosticList diagnostics) =>
        Discover(contentRoot, isBase: true, diagnostics);

    public static ContentDatabase Load(ContentLoadOptions options)
    {
        var diagnostics = new DiagnosticList();
        string gamePath = Path.Combine(options.ContentRoot, options.GamePack);
        ContentPack gamePack = ReadManifest(gamePath, isBase: true, diagnostics)
            ?? throw new ContentLoadException($"No valid game pack in '{gamePath}': {string.Join(" ", diagnostics.Errors)}");

        List<ContentPack> installed = Discover(options.ModsRoot, isBase: false, diagnostics);
        List<ContentPack> packs = ModLoadOrder.Resolve(gamePack, installed, options.EnabledMods, diagnostics);

        var store = new DefinitionStore(packs, diagnostics);
        var assets = new AssetCatalog();
        var locales = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var game = new JsonObject(ContentJson.NodeOptions);

        foreach (ContentPack pack in packs)
        {
            foreach ((string folder, string kind) in DefinitionFolders) LoadDefinitions(pack, folder, kind, store, diagnostics);
            LoadAssets(pack, assets, diagnostics);
            LoadLocales(pack, locales, diagnostics);
            if (ReadJson(Path.Combine(pack.RootDirectory, "game.json"), diagnostics, optional: true) is JsonObject gameNode)
                ContentJson.Merge(game, gameNode);
        }

        GameSettings settings = TryDeserialize<GameSettings>(game, "game.json", diagnostics) ?? new GameSettings();
        var rules = new Ruleset
        {
            Stats = store.Build<StatDefinition>("stat"),
            DamageTypes = store.Build<DamageTypeDefinition>("damageType"),
            Shapes = store.Build<ShapeDefinition>("shape"),
            TileParts = store.Build<TilePartDefinition>("tilePart"),
            MapBlocks = store.Build<MapBlockDefinition>("mapBlock"),
            Terrains = store.Build<TerrainDefinition>("terrain"),
            Items = store.Build<ItemDefinition>("item"),
            Armors = store.Build<ArmorDefinition>("armor"),
            Units = store.Build<UnitDefinition>("unit"),
            InventorySlots = store.Build<InventorySlotDefinition>("inventorySlot"),
            Missions = store.Build<MissionDefinition>("mission"),
            Battle = settings.Battle,
        };

        var database = new ContentDatabase(
            packs,
            installed,
            rules,
            assets,
            locales.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, string>)pair.Value, StringComparer.OrdinalIgnoreCase),
            settings,
            diagnostics);

        ContentValidator.Validate(database);
        return database;
    }

    /// <summary>Reads a manifest; returns null (with a diagnostic) if it is missing or invalid.</summary>
    public static ContentPack? ReadManifest(string directory, bool isBase, DiagnosticList diagnostics)
    {
        string path = Path.Combine(directory, ManifestFile);
        if (!File.Exists(path))
        {
            diagnostics.Error(directory, "manifest.json not found.");
            return null;
        }

        if (ReadJson(path, diagnostics, optional: false) is not JsonObject node) return null;
        PackManifest? manifest = TryDeserialize<PackManifest>(node, path, diagnostics);
        if (manifest is null) return null;
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            diagnostics.Error(path, "The manifest has no 'id'.");
            return null;
        }

        if (isBase && manifest.FormatVersion != PackManifest.SupportedFormatVersion)
        {
            diagnostics.Error(path, $"Content format {manifest.FormatVersion} is not supported (expected {PackManifest.SupportedFormatVersion}).");
            return null;
        }

        return new ContentPack(manifest, directory, isBase);
    }

    private static List<ContentPack> Discover(string? root, bool isBase, DiagnosticList diagnostics)
    {
        var packs = new List<ContentPack>();
        if (root is null || !Directory.Exists(root)) return packs;
        foreach (string directory in Directory.GetDirectories(root).Order(StringComparer.Ordinal))
        {
            if (!File.Exists(Path.Combine(directory, ManifestFile))) continue;
            if (ReadManifest(directory, isBase, diagnostics) is not { } pack) continue;
            if (packs.Any(m => m.Manifest.Id == pack.Manifest.Id))
            {
                diagnostics.Error(directory, $"Another pack already uses the id '{pack.Manifest.Id}'; this one is ignored.");
                continue;
            }

            packs.Add(pack);
        }

        return packs;
    }

    private static void LoadDefinitions(ContentPack pack, string folder, string kind, DefinitionStore store, DiagnosticList diagnostics)
    {
        string directory = Path.Combine(pack.RootDirectory, folder);
        if (!Directory.Exists(directory)) return;
        foreach (string file in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            JsonNode? root = ReadJson(file, diagnostics, optional: false);
            IEnumerable<JsonNode?> items = root is JsonArray array ? array : [root];
            foreach (JsonNode? item in items)
            {
                if (item is not JsonObject definition)
                {
                    if (item is not null) diagnostics.Error(file, "Each definition must be a JSON object.");
                    continue;
                }

                string? id = definition["id"] is JsonValue idValue && idValue.TryGetValue(out string? text) ? text : null;
                if (string.IsNullOrWhiteSpace(id) && kind is "mapBlock" or "mission") id = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostics.Error(file, $"A {kind} definition has no 'id'.");
                    continue;
                }

                store.Add(kind, id, definition, pack, file);
            }
        }
    }

    private static void LoadAssets(ContentPack pack, AssetCatalog catalog, DiagnosticList diagnostics)
    {
        string file = Path.Combine(pack.RootDirectory, "assets.json");
        if (ReadJson(file, diagnostics, optional: true) is not JsonObject root) return;
        foreach ((string section, JsonNode? node) in root)
        {
            if (section is not ("textures" or "sounds" or "fonts" or "sheets"))
            {
                diagnostics.Warning(file, $"Unknown section '{section}' ignored.");
                continue;
            }

            if (node is not JsonObject entries)
            {
                diagnostics.Error(file, $"Section '{section}' must be an object.");
                continue;
            }

            foreach ((string id, JsonNode? value) in entries)
            {
                if (section == "sheets")
                {
                    if (value is JsonObject sheetNode && TryDeserialize<SpriteSheetDefinition>(sheetNode, $"{file} ({id})", diagnostics) is { } sheet)
                        catalog.SetSheet(id, sheet);
                    continue;
                }

                if (value is not JsonValue pathValue || !pathValue.TryGetValue(out string? relative))
                {
                    diagnostics.Error(file, $"Asset '{id}' must be a file path.");
                    continue;
                }

                string fullPath = Path.GetFullPath(Path.Combine(pack.RootDirectory, relative));
                if (!File.Exists(fullPath)) diagnostics.Warning(file, $"Asset '{id}': file '{relative}' not found (a placeholder will be used).");
                var entry = new AssetEntry(id, fullPath, pack.Manifest.Id);
                if (section == "textures") catalog.SetTexture(entry);
                else if (section == "sounds") catalog.SetSound(entry);
                else catalog.SetFont(entry);
            }
        }
    }

    private static void LoadLocales(ContentPack pack, Dictionary<string, Dictionary<string, string>> locales, DiagnosticList diagnostics)
    {
        string directory = Path.Combine(pack.RootDirectory, "localization");
        if (!Directory.Exists(directory)) return;
        foreach (string file in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            string language = Path.GetFileNameWithoutExtension(file);
            if (ReadJson(file, diagnostics, optional: false) is not JsonObject root)
            {
                diagnostics.Error(file, "A localization file must be a JSON object of key/text pairs.");
                continue;
            }

            if (!locales.TryGetValue(language, out var table)) locales[language] = table = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(root, "", table, file, diagnostics);
        }
    }

    /// <summary>Nested objects are allowed: <c>{"ui": {"end_turn": "…"}}</c> becomes <c>ui.end_turn</c>.</summary>
    private static void Flatten(JsonObject node, string prefix, Dictionary<string, string> table, string file, DiagnosticList diagnostics)
    {
        foreach ((string key, JsonNode? value) in node)
        {
            string fullKey = prefix.Length == 0 ? key : $"{prefix}.{key}";
            if (value is JsonObject child) Flatten(child, fullKey, table, file, diagnostics);
            else if (value is JsonValue text && text.TryGetValue(out string? s)) table[fullKey] = s;
            else diagnostics.Error(file, $"Text '{fullKey}' must be a string.");
        }
    }

    internal static JsonNode? ReadJson(string path, DiagnosticList diagnostics, bool optional)
    {
        if (!File.Exists(path))
        {
            if (!optional) diagnostics.Error(path, "File not found.");
            return null;
        }

        try
        {
            return ContentJson.ParseFile(path);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // ArgumentException: the same key twice in one object.
            diagnostics.Error(path, $"Cannot read JSON: {exception.Message}");
            return null;
        }
    }

    internal static T? TryDeserialize<T>(JsonNode node, string source, DiagnosticList diagnostics) where T : class
    {
        try
        {
            return ContentJson.Deserialize<T>(node);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            diagnostics.Error(source, $"Invalid {typeof(T).Name.Replace("Definition", "", StringComparison.Ordinal)}: {exception.Message}");
            return null;
        }
    }
}
