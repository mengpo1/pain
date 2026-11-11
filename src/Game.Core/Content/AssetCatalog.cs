namespace Game.Core.Content;

/// <summary>Where a logical asset id points to, and which pack provided it.</summary>
public sealed record AssetEntry(string Id, string Path, string PackId);

/// <summary>
/// A sprite sheet cut into frames of equal size. What the frames mean (directions, animation
/// steps) is decided by whoever draws it; the catalog only knows the geometry.
/// </summary>
public sealed class SpriteSheetDefinition
{
    /// <summary>Logical texture id.</summary>
    public string Texture { get; init; } = "";
    public int FrameWidth { get; init; }
    public int FrameHeight { get; init; }

    /// <summary>
    /// Pixel of the frame placed on the anchor point (for map sprites: the north corner of the
    /// tile's floor diamond). Null = frame centre.
    /// </summary>
    public int[]? Anchor { get; init; }
}

/// <summary>
/// Logical id → file. Rules and data only ever reference ids (<c>unit.soldier</c>,
/// <c>sfx.rifle</c>); replacing a sprite or a sound means mapping its id to another file, which is
/// exactly what a mod does. No class of the simulation knows a file name.
/// </summary>
public sealed class AssetCatalog
{
    private readonly Dictionary<string, AssetEntry> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssetEntry> _sounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssetEntry> _fonts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpriteSheetDefinition> _sheets = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, AssetEntry> Textures => _textures;
    public IReadOnlyDictionary<string, AssetEntry> Sounds => _sounds;
    public IReadOnlyDictionary<string, AssetEntry> Fonts => _fonts;
    public IReadOnlyDictionary<string, SpriteSheetDefinition> Sheets => _sheets;

    internal void SetTexture(AssetEntry entry) => _textures[entry.Id] = entry;
    internal void SetSound(AssetEntry entry) => _sounds[entry.Id] = entry;
    internal void SetFont(AssetEntry entry) => _fonts[entry.Id] = entry;
    internal void SetSheet(string id, SpriteSheetDefinition sheet) => _sheets[id] = sheet;

    /// <summary>A sprite id can name a plain texture or a sprite sheet.</summary>
    public bool HasSprite(string id) => _textures.ContainsKey(id) || _sheets.ContainsKey(id);
}
