using FontStashSharp;
using Game.Core.Content;
using Game.Desktop.Graphics;
using Game.Desktop.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Ui;

public enum Align
{
    Left,
    Center,
    Right,
}

/// <summary>Interface colours parsed once from the pack's <see cref="UiStyle"/>.</summary>
public sealed class UiColors(UiStyle style)
{
    public Color Background { get; } = ColorParser.Parse(style.Background);
    public Color Panel { get; } = ColorParser.Parse(style.Panel);
    public Color PanelBorder { get; } = ColorParser.Parse(style.PanelBorder);
    public Color Text { get; } = ColorParser.Parse(style.Text);
    public Color Muted { get; } = ColorParser.Parse(style.Muted);
    public Color Accent { get; } = ColorParser.Parse(style.Accent);
    public Color Friendly { get; } = ColorParser.Parse(style.Friendly);
    public Color Hostile { get; } = ColorParser.Parse(style.Hostile);
    public Color Neutral { get; } = ColorParser.Parse(style.Neutral);
    public Color Good { get; } = ColorParser.Parse(style.Good);
    public Color Bad { get; } = ColorParser.Parse(style.Bad);
}

/// <summary>
/// Fonts, colours, texts and a few immediate-mode widgets shared by every screen. A widget
/// returns true when clicked and marks the click as used so the map below ignores it.
/// </summary>
public sealed class UiContext
{
    private readonly string _fontId;

    public UiContext(AssetCache assets, UiStyle style, Localizer texts, InputState input)
    {
        Assets = assets;
        _fontId = style.Font;
        Colors = new UiColors(style);
        Texts = texts;
        Input = input;
    }

    public AssetCache Assets { get; }
    public UiColors Colors { get; }
    public Localizer Texts { get; }
    public InputState Input { get; }

    /// <summary>Interface scale relative to a 720-pixel-high window.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Set when the mouse is over a widget this frame (the map does not show its cursor then).</summary>
    public bool MouseOverUi { get; set; }

    public SpriteFontBase Font(float size) => Assets.Font(_fontId, size * Scale);

    public Vector2 Measure(string text, float size) => Font(size).MeasureString(text);

    public int S(float value) => (int)MathF.Round(value * Scale);

    public Vector2 Text(SpriteBatch batch, string text, Vector2 position, float size, Color color, Align align = Align.Left)
    {
        SpriteFontBase font = Font(size);
        Vector2 measured = font.MeasureString(text);
        float x = align switch
        {
            Align.Center => position.X - measured.X * 0.5f,
            Align.Right => position.X - measured.X,
            _ => position.X,
        };
        var at = new Vector2(MathF.Round(x), MathF.Round(position.Y));
        font.DrawText(batch, text, at + new Vector2(1f, 1f), Color.Black * (color.A / 255f * 0.8f));
        font.DrawText(batch, text, at, color);
        return measured;
    }

    public void Panel(SpriteBatch batch, Rectangle area, Color? fill = null)
    {
        batch.Draw(Assets.Pixel, area, fill ?? Colors.Panel);
        Primitives.Rect(batch, Assets.Pixel, new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), Colors.PanelBorder);
        if (area.Contains(Input.MousePosition)) MouseOverUi = true;
    }

    /// <summary>A clickable labelled button. Disabled buttons are drawn dimmed and never fire.</summary>
    public bool Button(SpriteBatch batch, Rectangle area, string label, bool enabled = true, bool active = false, float size = 15f)
    {
        bool hover = area.Contains(Input.MousePosition);
        if (hover) MouseOverUi = true;
        Color fill = active ? Colors.Accent * 0.45f : hover && enabled ? Colors.PanelBorder * 0.9f : Colors.Panel;
        batch.Draw(Assets.Pixel, area, fill);
        Primitives.Rect(batch, Assets.Pixel, new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), active ? Colors.Accent : Colors.PanelBorder);
        Vector2 measured = Measure(label, size);
        Text(batch, label, new Vector2(area.Center.X, area.Center.Y - measured.Y * 0.55f), size, enabled ? Colors.Text : Colors.Muted, Align.Center);
        if (!enabled || !hover || !Input.LeftClicked) return false;
        Input.ClickConsumed = true;
        return true;
    }

    /// <summary>Horizontal gauge with "value/max" (TU, health…).</summary>
    public void Bar(SpriteBatch batch, Rectangle area, int value, int max, Color color, string label)
    {
        batch.Draw(Assets.Pixel, area, Color.Black * 0.6f);
        float ratio = max <= 0 ? 0f : Math.Clamp(value / (float)max, 0f, 1f);
        batch.Draw(Assets.Pixel, new Rectangle(area.X, area.Y, (int)(area.Width * ratio), area.Height), color);
        Primitives.Rect(batch, Assets.Pixel, new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), Colors.PanelBorder);
        string text = $"{label} {value}/{max}";
        Text(batch, text, new Vector2(area.X + S(4), area.Y + (area.Height - Measure(text, 12f).Y) * 0.5f - 1f), 12f, Colors.Text);
    }
}
