using System.Text.Json;
using System.Text.Json.Nodes;
using TopDown.Content.Json;
using TopDown.Core.Definitions;

namespace TopDown.Content;

/// <summary>The base pack is missing or unreadable: nothing can be played.</summary>
public sealed class ContentLoadException(string message) : Exception(message);

public sealed class ContentLoadOptions
{
    /// <summary>Folder containing <c>Base/manifest.json</c>.</summary>
    public required string ContentRoot { get; init; }

    /// <summary>Folder where each sub-folder with a manifest is an installed mod.</summary>
    public string? ModsRoot { get; init; }

    /// <summary>Mod ids to load, in priority order (later wins).</summary>
    public IReadOnlyList<string> EnabledMods { get; init; } = [];
}

/// <summary>
/// Loads the base pack then the enabled mods. Definitions sharing an id are merged property
/// by property, later packs winning; arrays and plain values are replaced as a whole.
/// </summary>
public static class ContentLoader
{
    public const string BaseFolder = "Base";
    private const string ManifestFile = "manifest.json";

    private static readonly (string Folder, string Store)[] DefinitionFolders =
    [
        ("weapons", "weapon"),
        ("characters", "actor"),
        ("enemies", "actor"),
        ("tiles", "tile"),
        ("doors", "door"),
        ("modifiers", "modifier"),
        ("levels", "level"),
        ("themes", "theme"),
    ];

    public static ContentDatabase Load(ContentLoadOptions options)
    {
        var diagnostics = new DiagnosticList();
        string basePath = Path.Combine(options.ContentRoot, BaseFolder);
        ContentPack basePack = ReadManifest(basePath, isBase: true, diagnostics)
            ?? throw new ContentLoadException($"No valid base content pack in '{basePath}'.");

        List<ContentPack> installed = DiscoverMods(options.ModsRoot, diagnostics);
        List<ContentPack> packs = ModLoadOrder.Resolve(basePack, installed, options.EnabledMods, diagnostics);

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
        var definitions = new DefinitionSet(
            store.Build<WeaponDefinition>("weapon", (d, id) => d.Id = id),
            store.Build<ActorDefinition>("actor", (d, id) => d.Id = id),
            store.Build<TileDefinition>("tile", (d, id) => d.Id = id),
            store.Build<DoorDefinition>("door", (d, id) => d.Id = id),
            store.Build<ModifierDefinition>("modifier", (d, id) => d.Id = id),
            settings.Rules);

        var database = new ContentDatabase(
            packs,
            installed,
            definitions,
            store.Build<LevelDefinition>("level", (d, id) => d.Id = id),
            store.Build<ThemeDefinition>("theme", (d, id) => d.Id = id),
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

        return new ContentPack(manifest, directory, isBase);
    }

    private static List<ContentPack> DiscoverMods(string? modsRoot, DiagnosticList diagnostics)
    {
        var mods = new List<ContentPack>();
        if (modsRoot is null || !Directory.Exists(modsRoot)) return mods;
        foreach (string directory in Directory.GetDirectories(modsRoot).Order(StringComparer.Ordinal))
        {
            if (!File.Exists(Path.Combine(directory, ManifestFile))) continue;
            if (ReadManifest(directory, isBase: false, diagnostics) is not { } pack) continue;
            if (mods.Any(m => m.Manifest.Id == pack.Manifest.Id))
            {
                diagnostics.Error(directory, $"Another installed mod already uses the id '{pack.Manifest.Id}'; this one is ignored.");
                continue;
            }

            mods.Add(pack);
        }

        return mods;
    }

    private static void LoadDefinitions(ContentPack pack, string folder, string kind, DefinitionStore store, DiagnosticList diagnostics)
    {
        string directory = Path.Combine(pack.RootDirectory, folder);
        if (!Directory.Exists(directory)) return;
        foreach (string file in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
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
                if (string.IsNullOrWhiteSpace(id) && kind == "level") id = Path.GetFileNameWithoutExtension(file);
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
            string kind = section.ToLowerInvariant();
            if (kind is not ("textures" or "sounds" or "fonts" or "animations"))
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
                if (kind == "animations")
                {
                    if (value is JsonObject animationNode
                        && TryDeserialize<AnimationDefinition>(animationNode, $"{file} ({id})", diagnostics) is { } animation)
                        catalog.SetAnimation(id, animation);
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
                if (kind == "textures") catalog.SetTexture(entry);
                else if (kind == "sounds") catalog.SetSound(entry);
                else catalog.SetFont(entry);
            }
        }
    }

    private static void LoadLocales(ContentPack pack, Dictionary<string, Dictionary<string, string>> locales, DiagnosticList diagnostics)
    {
        string directory = Path.Combine(pack.RootDirectory, "locales");
        if (!Directory.Exists(directory)) return;
        foreach (string file in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            string language = Path.GetFileNameWithoutExtension(file);
            if (ReadJson(file, diagnostics, optional: false) is not JsonObject root)
            {
                diagnostics.Error(file, "A locale file must be a JSON object of key/text pairs.");
                continue;
            }

            if (!locales.TryGetValue(language, out var table)) locales[language] = table = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(root, "", table, file, diagnostics);
        }
    }

    /// <summary>Nested objects are allowed: <c>{"menu": {"play": "…"}}</c> becomes <c>menu.play</c>.</summary>
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
            diagnostics.Error(source, $"Invalid {typeof(T).Name.Replace("Definition", "", StringComparison.Ordinal).ToLowerInvariant()}: {exception.Message}");
            return null;
        }
    }
}
