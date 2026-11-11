using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;

namespace TopDown.Desktop.Graphics;

/// <summary>A drawable piece of a texture with its rotation origin.</summary>
public readonly record struct SpriteFrame(Texture2D Texture, Rectangle Source, Vector2 Origin);

/// <summary>Centre and joint of a body part, in pixels from the centre of its image.</summary>
public readonly record struct PartShape(Vector2 Center, Vector2 Joint);

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
    private readonly Dictionary<string, List<int>> _frames = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Part, string Torso), PartShape> _partShapes = new();

    public AssetCache(GraphicsDevice device, AssetCatalog catalog, bool loadSounds)
    {
        _device = device;
        _catalog = catalog;
        Pixel = Generate(1, 1, (_, _) => Color.White);
        Placeholder = Generate(16, 16, (x, y) => ((x / 4 + y / 4) % 2 == 0) ? new Color(255, 0, 255) : Color.Black);
        SoftCircle = Generate(128, 128, (x, y) => Radial(x, y, 128, d => MathF.Pow(MathF.Max(0f, 1f - d), 2f)));
        Vignette = Generate(256, 256, (x, y) => Radial(x, y, 256, d => Math.Clamp((d - 0.45f) / 0.55f, 0f, 1f)));
        var random = new Random(7);
        Noise = Generate(256, 256, (_, _) =>
        {
            int v = random.Next(256);
            return new Color(v, v, v, 255);
        });
        Scanlines = Generate(1, 3, (_, y) => y == 0 ? new Color(0, 0, 0, 255) : Color.Transparent);

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
    public Texture2D SoftCircle { get; }
    public Texture2D Vignette { get; }
    public Texture2D Noise { get; }
    public Texture2D Scanlines { get; }

    /// <summary>Resolves a sprite id (texture or animation). <paramref name="time"/> selects the animation frame.</summary>
    public SpriteFrame Sprite(string? id, float time = 0f)
    {
        if (string.IsNullOrEmpty(id)) return Frame(Placeholder);
        if (_catalog.Animations.TryGetValue(id, out AnimationDefinition? animation)) return AnimationFrame(id, animation, time);
        if (_textures.TryGetValue(id, out Texture2D? texture)) return Frame(texture);
        Report($"Unknown sprite '{id}': drawing a placeholder.");
        return Frame(Placeholder);
    }

    /// <summary>
    /// Where a body part sits on its image, relative to the image centre: its centre (pivot when it
    /// flies off) and the joint where it meets the torso (blood comes from there). Computed once from
    /// the pixels, so modders only provide the images.
    /// </summary>
    public PartShape PartShape(string partId, string torsoId)
    {
        if (_partShapes.TryGetValue((partId, torsoId), out PartShape cached)) return cached;
        PartShape shape = default;
        if (_textures.TryGetValue(partId, out Texture2D? part))
        {
            Color[] partPixels = Pixels(part);
            Color[]? torsoPixels = _textures.TryGetValue(torsoId, out Texture2D? torso) && torso.Bounds == part.Bounds ? Pixels(torso) : null;
            Vector2 half = new Vector2(part.Width, part.Height) * 0.5f;
            Vector2 sum = Vector2.Zero, overlap = Vector2.Zero, torsoSum = Vector2.Zero;
            int count = 0, overlapCount = 0, torsoCount = 0;
            for (int i = 0; i < partPixels.Length; i++)
            {
                var pixel = new Vector2(i % part.Width + 0.5f, i / part.Width + 0.5f) - half;
                bool inPart = partPixels[i].A > 96, inTorso = torsoPixels is not null && torsoPixels[i].A > 96;
                if (inPart) { sum += pixel; count++; }
                if (inTorso) { torsoSum += pixel; torsoCount++; }
                if (inPart && inTorso) { overlap += pixel; overlapCount++; }
            }

            Vector2 center = count > 0 ? sum / count : Vector2.Zero;
            Vector2 joint = overlapCount > 0 ? overlap / overlapCount
                : torsoCount > 0 ? center + (torsoSum / torsoCount - center) * 0.35f
                : center;
            shape = new PartShape(center, joint);
        }

        return _partShapes[(partId, torsoId)] = shape;
    }

    private static Color[] Pixels(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        return pixels;
    }

    public bool HasSprite(string? id) => id is not null && (_textures.ContainsKey(id) || _catalog.Animations.ContainsKey(id));

    /// <summary>Number of frames in an animation (1 for a plain texture).</summary>
    public int FrameCount(string id) =>
        _catalog.Animations.TryGetValue(id, out AnimationDefinition? animation) && _textures.TryGetValue(animation.Texture, out Texture2D? texture)
            ? FrameIndices(id, animation, texture).Count
            : 1;

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

    private SpriteFrame AnimationFrame(string id, AnimationDefinition animation, float time)
    {
        if (!_textures.TryGetValue(animation.Texture, out Texture2D? texture) || animation.FrameWidth <= 0 || animation.FrameHeight <= 0)
        {
            Report($"Animation '{id}' has no valid texture or frame size.");
            return Frame(Placeholder);
        }

        List<int> frames = FrameIndices(id, animation, texture);
        int step = animation.FrameDuration > 0f ? (int)MathF.Floor(time / animation.FrameDuration) : 0;
        int index = animation.Loop ? ((step % frames.Count) + frames.Count) % frames.Count : Math.Clamp(step, 0, frames.Count - 1);
        int columns = Math.Max(1, texture.Width / animation.FrameWidth);
        int frame = frames[index];
        var source = new Rectangle(frame % columns * animation.FrameWidth, frame / columns * animation.FrameHeight, animation.FrameWidth, animation.FrameHeight);
        Vector2 origin = animation.Origin is { } o ? new Vector2(o.X, o.Y) : new Vector2(animation.FrameWidth, animation.FrameHeight) * 0.5f;
        return new SpriteFrame(texture, source, origin);
    }

    /// <summary>Explicit frame list, or every frame of the sheet (computed once).</summary>
    private List<int> FrameIndices(string id, AnimationDefinition animation, Texture2D texture)
    {
        if (animation.Frames.Count > 0) return animation.Frames;
        if (_frames.TryGetValue(id, out List<int>? cached)) return cached;
        int count = Math.Max(1, texture.Width / Math.Max(1, animation.FrameWidth) * (texture.Height / Math.Max(1, animation.FrameHeight)));
        return _frames[id] = Enumerable.Range(0, count).ToList();
    }

    private static SpriteFrame Frame(Texture2D texture) =>
        new(texture, texture.Bounds, new Vector2(texture.Width, texture.Height) * 0.5f);

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

    /// <summary>White with an alpha depending on the distance to the centre (0 = centre, 1 = edge).</summary>
    private static Color Radial(int x, int y, int size, Func<float, float> alphaAtDistance)
    {
        float half = size * 0.5f;
        float dx = (x + 0.5f - half) / half;
        float dy = (y + 0.5f - half) / half;
        float alpha = Math.Clamp(alphaAtDistance(MathF.Sqrt(dx * dx + dy * dy)), 0f, 1f);
        return Color.White * alpha;
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
