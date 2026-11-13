namespace TopDown.Content;

/// <summary>Where a logical asset id points to, and which pack provided it.</summary>
public sealed record AssetEntry(string Id, string Path, string PackId);

/// <summary>
/// Logical id → file. Gameplay data only ever references ids; replacing a sprite or a sound
/// means mapping its id to another file (a mod does exactly that).
/// </summary>
public sealed class AssetCatalog
{
    private readonly Dictionary<string, AssetEntry> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssetEntry> _sounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AssetEntry> _fonts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimationDefinition> _animations = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, AssetEntry> Textures => _textures;
    public IReadOnlyDictionary<string, AssetEntry> Sounds => _sounds;
    public IReadOnlyDictionary<string, AssetEntry> Fonts => _fonts;
    public IReadOnlyDictionary<string, AnimationDefinition> Animations => _animations;

    internal void SetTexture(AssetEntry entry) => _textures[entry.Id] = entry;
    internal void SetSound(AssetEntry entry) => _sounds[entry.Id] = entry;
    internal void SetFont(AssetEntry entry) => _fonts[entry.Id] = entry;
    internal void SetAnimation(string id, AnimationDefinition animation) => _animations[id] = animation;

    /// <summary>A sprite id can name a texture or an animation.</summary>
    public bool HasSprite(string id) => _textures.ContainsKey(id) || _animations.ContainsKey(id);
}
