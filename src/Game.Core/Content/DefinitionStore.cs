using System.Text.Json.Nodes;

namespace Game.Core.Content;

/// <summary>
/// Collects raw JSON definitions per kind and id while packs are read, then turns them into
/// typed definitions. Rules when a later pack uses an existing id:
/// <list type="bullet">
/// <item>by default its properties are merged into the earlier definition (later wins);</item>
/// <item><c>"$replace": true</c> discards the earlier definition first;</item>
/// <item><c>"$delete": true</c> removes the definition (references to it then become errors).</item>
/// </list>
/// </summary>
internal sealed class DefinitionStore
{
    public const string ReplaceKey = "$replace";
    public const string DeleteKey = "$delete";

    private readonly Dictionary<string, Dictionary<string, Entry>> _kinds = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<ContentPack> _packs;
    private readonly DiagnosticList _diagnostics;

    public DefinitionStore(IReadOnlyList<ContentPack> packs, DiagnosticList diagnostics)
    {
        _packs = packs;
        _diagnostics = diagnostics;
    }

    public void Add(string kind, string id, JsonObject definition, ContentPack pack, string file)
    {
        if (!_kinds.TryGetValue(kind, out var entries)) _kinds[kind] = entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        bool replace = TakeFlag(definition, ReplaceKey);
        bool delete = TakeFlag(definition, DeleteKey);
        entries.TryGetValue(id, out Entry? entry);

        if (delete)
        {
            if (entry is null) _diagnostics.Warning(file, $"Cannot delete {kind} '{id}': it is not defined by an earlier pack.");
            entries.Remove(id);
            return;
        }

        if (entry is null || replace)
        {
            entries[id] = new Entry((JsonObject)definition.DeepClone(), pack, file);
            return;
        }

        if (entry.Pack == pack)
        {
            _diagnostics.Warning(file, $"{kind} '{id}' is defined twice in pack '{pack.Manifest.Id}' (also in {entry.File}); the definitions are merged.");
        }
        else if (!ModLoadOrder.DependsOn(pack, entry.Pack, _packs))
        {
            _diagnostics.Warning(file,
                $"Mods '{entry.Pack.Manifest.Id}' and '{pack.Manifest.Id}' both modify {kind} '{id}'. " +
                $"'{pack.Manifest.Id}' is loaded later and wins for the values both change.");
        }

        ContentJson.Merge(entry.Node, definition);
        entry.Pack = pack;
        entry.File = file;
        entry.Sources.Add(file);
    }

    /// <summary>Deserialises every merged definition of a kind; invalid ones are reported and skipped.</summary>
    public Dictionary<string, T> Build<T>(string kind) where T : Definition
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (!_kinds.TryGetValue(kind, out var entries)) return result;
        foreach ((string id, Entry entry) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            string source = string.Join(" + ", entry.Sources);
            if (ContentLoader.TryDeserialize<T>(entry.Node, source, _diagnostics) is not { } definition) continue;
            definition.Id = id;
            result[id] = definition;
        }

        return result;
    }

    private static bool TakeFlag(JsonObject definition, string key)
    {
        if (!definition.TryGetPropertyValue(key, out JsonNode? value)) return false;
        definition.Remove(key);
        return value is JsonValue flag && flag.TryGetValue(out bool set) && set;
    }

    private sealed class Entry(JsonObject node, ContentPack pack, string file)
    {
        public JsonObject Node { get; } = node;
        public ContentPack Pack { get; set; } = pack;
        public string File { get; set; } = file;
        public List<string> Sources { get; } = [file];
    }
}
