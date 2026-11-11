namespace Game.Core.Content;

/// <summary>manifest.json at the root of a game pack or a mod.</summary>
public sealed class PackManifest
{
    /// <summary>Content format understood by this engine version.</summary>
    public const int SupportedFormatVersion = 1;

    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public int FormatVersion { get; init; } = SupportedFormatVersion;
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";

    /// <summary>
    /// Packs this one needs. A mod usually lists the game pack it was made for; it is then
    /// refused with a clear message when another game is loaded.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}

/// <summary>A pack found on disk. The game pack is loaded first, then the enabled mods.</summary>
public sealed record ContentPack(PackManifest Manifest, string RootDirectory, bool IsBase);
