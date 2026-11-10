using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TopDown.Content.Json;

/// <summary>Shared JSON settings: camelCase, comments allowed, unknown properties rejected.</summary>
public static class ContentJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static JsonNodeOptions NodeOptions { get; } = new() { PropertyNameCaseInsensitive = true };

    public static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new Vector2Converter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static JsonNode? ParseFile(string path) =>
        JsonNode.Parse(File.ReadAllText(path), NodeOptions, DocumentOptions);

    public static T Deserialize<T>(JsonNode node) =>
        node.Deserialize<T>(Options) ?? throw new JsonException("The JSON value is null.");

    /// <summary>
    /// Merges <paramref name="patch"/> into <paramref name="target"/>: objects are merged
    /// recursively, every other value (including arrays) replaces the previous one.
    /// </summary>
    public static void Merge(JsonObject target, JsonObject patch)
    {
        // Nodes are parsed with case-insensitive property names, so "Damage" patches "damage".
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

    /// <summary>Reads <c>[x, y]</c> or <c>{ "x": .., "y": .. }</c>.</summary>
    private sealed class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read();
                float x = reader.GetSingle();
                reader.Read();
                float y = reader.GetSingle();
                reader.Read();
                if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("A vector must be [x, y].");
                return new Vector2(x, y);
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                float x = 0f, y = 0f;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    string name = reader.GetString() ?? "";
                    reader.Read();
                    if (name.Equals("x", StringComparison.OrdinalIgnoreCase)) x = reader.GetSingle();
                    else if (name.Equals("y", StringComparison.OrdinalIgnoreCase)) y = reader.GetSingle();
                    else throw new JsonException($"Unknown vector component '{name}'.");
                }

                return new Vector2(x, y);
            }

            throw new JsonException("A vector must be [x, y].");
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }

    public static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
