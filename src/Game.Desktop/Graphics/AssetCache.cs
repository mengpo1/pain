using FontStashSharp;
using Game.Core.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Graphics;

/// <summary>A drawable piece of a texture and the pixel placed on the anchor point.</summary>
public readonly record struct SpriteFrame(Texture2D Texture, Rectangle Source, Vector2 Anchor);

/// <summary>
/// Loads every texture, sound and font of the content catalog once, by logical id, and frees
/// them on <see cref="Dispose"/>. Missing files are reported once and replaced by a visible
/// placeholder texture or by silence, so the game stays playable.
/// </summary>
public sealed class AssetCache : IDisposable
{
    private const string DefaultFont = "font.ui";
    private readonly GraphicsDevice _device;
    private readonly AssetCatalog _catalog;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FontSystem> _fonts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly List<Texture2D> _generated = new();

    public AssetCache(GraphicsDevice device, AssetCatalog catalog, bool loadSounds)
    {
        _device = device;
        _catalog = catalog;
        Pixel = Generate(1, 1, (_, _) => Color.White);
        Placeholder = Generate(16, 16, (x, y) => ((x / 4 + y / 4) % 2 == 0) ? new Color(255, 0, 255) : Color.Black);

        foreach ((string id, AssetEntry entry) in catalog.Textures) LoadTexture(id, entry.Path);
        if (loadSounds)
        {
            foreach ((string id, AssetEntry entry) in catalog.Sounds) LoadSound(id, entry.Path);
        }

        foreach ((string id, AssetEntry entry) in catalog.Fonts) LoadFont(id, entry.Path);
        if (_fonts.Count == 0) throw new InvalidOperationException("No usable font in the content: at least one 'fonts' entry is required.");
    }

    public Texture2D Pixel { get; }
    public Texture2D Placeholder { get; }

    public bool HasSprite(string? id) => id is not null && (_textures.ContainsKey(id) || _catalog.Sheets.ContainsKey(id));

    /// <summary>
    /// Resolves a sprite id: a plain texture (frame index ignored) or frame <paramref name="frame"/>
    /// of a sprite sheet (frames counted left to right, top to bottom).
    /// </summary>
    public SpriteFrame Sprite(string? id, int frame = 0)
    {
        if (string.IsNullOrEmpty(id)) return Whole(Placeholder, null);
        if (_catalog.Sheets.TryGetValue(id, out SpriteSheetDefinition? sheet)) return SheetFrame(id, sheet, frame);
        if (_textures.TryGetValue(id, out Texture2D? texture)) return Whole(texture, null);
        Report($"Unknown sprite '{id}': drawing a placeholder.");
        return Whole(Placeholder, null);
    }

    /// <summary>Number of frames of a sheet (1 for a plain texture).</summary>
    public int FrameCount(string id)
    {
        if (!_catalog.Sheets.TryGetValue(id, out SpriteSheetDefinition? sheet) || !_textures.TryGetValue(sheet.Texture, out Texture2D? texture)) return 1;
        if (sheet.FrameWidth <= 0 || sheet.FrameHeight <= 0) return 1;
        return Math.Max(1, texture.Width / sheet.FrameWidth * (texture.Height / sheet.FrameHeight));
    }

    public SoundEffect? Sound(string? id)
    {
        if (id is null) return null;
        if (_sounds.TryGetValue(id, out SoundEffect? sound)) return sound;
        if (!_catalog.Sounds.ContainsKey(id)) Report($"Unknown sound '{id}'.");
        return null;
    }

    public SpriteFontBase Font(string? id, float size)
    {
        if (id is null || !_fonts.TryGetValue(id, out FontSystem? system))
        {
            if (id is not null) Report($"Unknown font '{id}': using a default font.");
            system = _fonts.TryGetValue(DefaultFont, out FontSystem? fallback) ? fallback : _fonts.Values.First();
        }

        return system.GetFont(MathF.Max(6f, MathF.Round(size)));
    }

    private SpriteFrame SheetFrame(string id, SpriteSheetDefinition sheet, int frame)
    {
        if (!_textures.TryGetValue(sheet.Texture, out Texture2D? texture) || sheet.FrameWidth <= 0 || sheet.FrameHeight <= 0)
        {
            Report($"Sprite sheet '{id}' has no valid texture or frame size.");
            return Whole(Placeholder, null);
        }

        int columns = Math.Max(1, texture.Width / sheet.FrameWidth);
        int count = FrameCount(id);
        if (frame < 0 || frame >= count)
        {
            Report($"Sprite sheet '{id}' has no frame {frame} (it has {count}).");
            frame = Math.Clamp(frame, 0, count - 1);
        }

        var source = new Rectangle(frame % columns * sheet.FrameWidth, frame / columns * sheet.FrameHeight, sheet.FrameWidth, sheet.FrameHeight);
        return new SpriteFrame(texture, source, AnchorOf(sheet.Anchor, sheet.FrameWidth, sheet.FrameHeight));
    }

    private static SpriteFrame Whole(Texture2D texture, int[]? anchor) =>
        new(texture, texture.Bounds, AnchorOf(anchor, texture.Width, texture.Height));

    private static Vector2 AnchorOf(int[]? anchor, int width, int height) =>
        anchor is { Length: 2 } ? new Vector2(anchor[0], anchor[1]) : new Vector2(width, height) * 0.5f;

    private void LoadTexture(string id, string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            using FileStream stream = File.OpenRead(path);
            _textures[id] = Texture2D.FromStream(_device, stream, DefaultColorProcessors.PremultiplyAlpha);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            Report($"Cannot load texture '{id}' from '{path}': {exception.Message}");
        }
    }

    private void LoadSound(string id, string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            _sounds[id] = SoundEffect.FromFile(path);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Report($"Cannot load sound '{id}' from '{path}': {exception.Message}");
        }
    }

    private void LoadFont(string id, string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var system = new FontSystem();
            system.AddFont(File.ReadAllBytes(path));
            _fonts[id] = system;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            Report($"Cannot load font '{id}' from '{path}': {exception.Message}");
        }
    }

    private Texture2D Generate(int width, int height, Func<int, int, Color> pixel)
    {
        var data = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                data[y * width + x] = pixel(x, y);
        var texture = new Texture2D(_device, width, height);
        texture.SetData(data);
        _generated.Add(texture);
        return texture;
    }

    private void Report(string message)
    {
        if (_reported.Add(message)) Log.Warning(message);
    }

    public void Dispose()
    {
        foreach (Texture2D texture in _textures.Values) texture.Dispose();
        foreach (Texture2D texture in _generated) texture.Dispose();
        foreach (SoundEffect sound in _sounds.Values) sound.Dispose();
        foreach (FontSystem font in _fonts.Values) font.Dispose();
        _textures.Clear();
        _sounds.Clear();
        _fonts.Clear();
    }
}
