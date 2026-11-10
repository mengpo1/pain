using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Desktop.Graphics;

namespace TopDown.Desktop.Ui;

public enum Align
{
    Left,
    Center,
    Right,
}

/// <summary>Fonts, theme colours and text helpers shared by every screen.</summary>
public sealed class UiContext
{
    private readonly AssetCache _assets;
    private readonly string _fontId;

    public UiContext(AssetCache assets, ThemeDefinition theme, Localizer texts)
    {
        _assets = assets;
        _fontId = theme.Ui.Font;
        Theme = theme;
        Colors = new ThemeColors(theme);
        Texts = texts;
    }

    public ThemeDefinition Theme { get; }
    public ThemeColors Colors { get; }
    public Localizer Texts { get; }
    public AssetCache Assets => _assets;

    /// <summary>UI scale relative to a 720p screen.</summary>
    public float Scale { get; set; } = 1f;

    public SpriteFontBase Font(float size) => _assets.Font(_fontId, size * Scale);

    public Vector2 Measure(string text, float size) => Font(size).MeasureString(text);

    /// <summary>Draws text with a drop shadow; returns its size.</summary>
    public Vector2 Text(SpriteBatch batch, string text, Vector2 position, float size, Color color, Align align = Align.Left, float shadow = 2f)
    {
        SpriteFontBase font = Font(size);
        Vector2 measured = font.MeasureString(text);
        Vector2 origin = align switch
        {
            Align.Center => new Vector2(measured.X * 0.5f, 0f),
            Align.Right => new Vector2(measured.X, 0f),
            _ => Vector2.Zero,
        };
        Vector2 at = position - origin;
        if (shadow > 0f) font.DrawText(batch, text, at + new Vector2(shadow * Scale), Colors.Shadow * (color.A / 255f));
        font.DrawText(batch, text, at, color);
        return measured;
    }

    /// <summary>Large title with an optional per-letter wave (HUD headlines, menus).</summary>
    public void Title(SpriteBatch batch, string text, Vector2 center, float size, Color color, float time)
    {
        float wave = Theme.Ui.TitleWave;
        if (wave <= 0f)
        {
            Text(batch, text, center, size, color, Align.Center, 4f);
            return;
        }

        SpriteFontBase font = Font(size);
        float width = font.MeasureString(text).X;
        float x = center.X - width * 0.5f;
        for (int i = 0; i < text.Length; i++)
        {
            string letter = text[i].ToString();
            float offset = MathF.Sin(time * 3f + i * 0.45f) * 4f * wave * Scale;
            float rotation = MathF.Sin(time * 2f + i * 0.3f) * 0.05f * wave;
            var at = new Vector2(x, center.Y + offset);
            font.DrawText(batch, letter, at + new Vector2(4f * Scale), Colors.Shadow, rotation: rotation);
            font.DrawText(batch, letter, at, color, rotation: rotation);
            x += font.MeasureString(letter).X;
        }
    }

    public void Panel(SpriteBatch batch, Rectangle area) => batch.Draw(_assets.Pixel, area, Colors.Panel);
}
