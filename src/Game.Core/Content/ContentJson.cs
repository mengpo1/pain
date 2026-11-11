using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Game.Core.Content;

/// <summary>
/// Shared JSON settings: camelCase, comments and trailing commas allowed, unknown properties
/// rejected (a typo in a content file is reported instead of being silently ignored).
/// </summary>
public static class ContentJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions(strict: true);

    /// <summary>Same settings, but unknown properties are ignored (user settings written by older versions).</summary>
    public static JsonSerializerOptions LenientOptions { get; } = CreateOptions(strict: false);

    /// <summary>
    /// Raw nodes keep exact keys: map legends are case-sensitive data ("W" and "w" are different
    /// symbols). Schema properties are matched case-insensitively when deserialising.
    /// </summary>
    public static JsonNodeOptions NodeOptions { get; } = new() { PropertyNameCaseInsensitive = false };

    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static JsonSerializerOptions CreateOptions(bool strict)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = strict ? JsonUnmappedMemberHandling.Disallow : JsonUnmappedMemberHandling.Skip,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    /// <summary>Parses a file completely, so duplicate keys are reported here and not later.</summary>
    public static JsonNode? ParseFile(string path) => Parse(File.ReadAllText(path));

    public static JsonNode? Parse(string json)
    {
        JsonNode? node = JsonNode.Parse(json, NodeOptions, DocumentOptions);
        Materialize(node);
        return node;
    }

    private static void Materialize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string _, JsonNode? child) in obj) Materialize(child);
                break;
            case JsonArray array:
                foreach (JsonNode? child in array) Materialize(child);
                break;
        }
    }

    public static T Deserialize<T>(JsonNode node) =>
        node.Deserialize<T>(Options) ?? throw new JsonException("The JSON value is null.");

    /// <summary>
    /// Merges <paramref name="patch"/> into <paramref name="target"/>: objects are merged
    /// recursively, every other value (including arrays) replaces the previous one.
    /// </summary>
    public static void Merge(JsonObject target, JsonObject patch)
    {
        foreach ((string key, JsonNode? value) in patch)
        {
            if (value is JsonObject patchObject && target[key] is JsonObject targetObject)
            {
                Merge(targetObject, patchObject);
            }
            else
            {
                target.Remove(key);
                target[key] = value?.DeepClone();
            }
        }
    }

    public static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
