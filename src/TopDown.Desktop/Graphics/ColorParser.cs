using System.Globalization;
using Microsoft.Xna.Framework;

namespace TopDown.Desktop.Graphics;

/// <summary>Parses "#rrggbb" and "#rrggbbaa" colours from content files.</summary>
public static class ColorParser
{
    public static Color Parse(string? text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        string hex = text.Trim().TrimStart('#');
        if ((hex.Length != 6 && hex.Length != 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return fallback;

        if (hex.Length == 6) value = (value << 8) | 0xFF;
        byte r = (byte)(value >> 24);
        byte g = (byte)(value >> 16);
        byte b = (byte)(value >> 8);
        byte a = (byte)value;
        // SpriteBatch works with premultiplied alpha.
        return new Color(r * a / 255, g * a / 255, b * a / 255, a);
    }
}
