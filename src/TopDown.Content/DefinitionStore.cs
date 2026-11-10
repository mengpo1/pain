using System.Text.Json.Nodes;
using TopDown.Content.Json;

namespace TopDown.Content;

/// <summary>
/// Collects raw JSON definitions per kind and id while packs are read, merging later packs
/// into earlier ones, then turns them into typed definitions.
/// </summary>
internal sealed class DefinitionStore
{
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

        if (!entries.TryGetValue(id, out Entry? entry))
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
    public Dictionary<string, T> Build<T>(string kind, Action<T, string> assignId) where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (!_kinds.TryGetValue(kind, out var entries)) return result;
        foreach ((string id, Entry entry) in entries)
        {
            string source = string.Join(" + ", entry.Sources);
            if (ContentLoader.TryDeserialize<T>(entry.Node, source, _diagnostics) is not { } definition) continue;
            assignId(definition, id);
            result[id] = definition;
        }

        return result;
    }

    private sealed class Entry(JsonObject node, ContentPack pack, string file)
    {
        public JsonObject Node { get; } = node;
        public ContentPack Pack { get; set; } = pack;
        public string File { get; set; } = file;
        public List<string> Sources { get; } = [file];
    }
}
