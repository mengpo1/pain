using System.Text.Json.Serialization;

namespace Game.Core.Content;

/// <summary>
/// Base of every content definition: a stable identifier, independent of the displayed name,
/// used by saves, mods and references between definitions.
/// </summary>
public abstract class Definition
{
    [JsonInclude]
    public string Id { get; internal set; } = "";

    public override string ToString() => $"{GetType().Name} '{Id}'";
}
