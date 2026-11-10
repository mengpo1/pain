using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>Static level geometry: a grid of tile definitions.</summary>
public sealed class TileMap
{
    private readonly TileDefinition[] _tiles;

    public TileMap(int width, int height, float tileSize, TileDefinition fill)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("A map needs at least one tile.");
        if (tileSize <= 0f) throw new ArgumentOutOfRangeException(nameof(tileSize));
        Width = width;
        Height = height;
        TileSize = tileSize;
        _tiles = new TileDefinition[width * height];
        Array.Fill(_tiles, fill);
    }

    public int Width { get; }
    public int Height { get; }
    public float TileSize { get; }
    public float PixelWidth => Width * TileSize;
    public float PixelHeight => Height * TileSize;

    /// <summary>Incremented whenever a tile changes (lets the AI and the renderer refresh caches).</summary>
    public int Version { get; private set; }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public TileDefinition Get(int x, int y) => InBounds(x, y) ? _tiles[y * Width + x] : TileDefinition.Void;

    public void Set(int x, int y, TileDefinition tile)
    {
        if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x}, {y}) is outside the map.");
        _tiles[y * Width + x] = tile;
        Version++;
    }

    public bool Blocks(int x, int y, Blocking mask) => (Get(x, y).Blocking & mask) != 0;

    public (int X, int Y) TileAt(Vector2 position) =>
        ((int)MathF.Floor(position.X / TileSize), (int)MathF.Floor(position.Y / TileSize));

    public RectF TileRect(int x, int y) => new(x * TileSize, y * TileSize, TileSize, TileSize);

    public Vector2 TileCenter(int x, int y) => new((x + 0.5f) * TileSize, (y + 0.5f) * TileSize);

    /// <summary>
    /// Walks the grid along the segment (Amanatides–Woo) and returns the first tile matching
    /// <paramref name="mask"/>. Exact at any speed, so fast bullets never skip a wall.
    /// </summary>
    public bool Raycast(Vector2 from, Vector2 to, Blocking mask, out TileHit hit)
    {
        Vector2 delta = to - from;
        (int x, int y) = TileAt(from);
        if (Blocks(x, y, mask))
        {
            hit = new TileHit(x, y, 0f, from, -Geo.NormalizeOrZero(delta));
            return true;
        }

        int stepX = Math.Sign(delta.X);
        int stepY = Math.Sign(delta.Y);
        float tDeltaX = stepX != 0 ? TileSize / MathF.Abs(delta.X) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? TileSize / MathF.Abs(delta.Y) : float.PositiveInfinity;
        float tMaxX = stepX != 0 ? (((stepX > 0 ? x + 1 : x) * TileSize) - from.X) / delta.X : float.PositiveInfinity;
        float tMaxY = stepY != 0 ? (((stepY > 0 ? y + 1 : y) * TileSize) - from.Y) / delta.Y : float.PositiveInfinity;

        // A segment never crosses more tiles than this; guards against float edge cases.
        int maxSteps = Width + Height + 4;
        for (int i = 0; i < maxSteps; i++)
        {
            float t;
            Vector2 normal;
            if (tMaxX < tMaxY)
            {
                t = tMaxX;
                x += stepX;
                tMaxX += tDeltaX;
                normal = new Vector2(-stepX, 0f);
            }
            else
            {
                t = tMaxY;
                y += stepY;
                tMaxY += tDeltaY;
                normal = new Vector2(0f, -stepY);
            }

            if (t > 1f) break;
            if (Blocks(x, y, mask))
            {
                hit = new TileHit(x, y, t, from + delta * t, normal);
                return true;
            }
        }

        hit = default;
        return false;
    }
}

public readonly record struct TileHit(int X, int Y, float Fraction, Vector2 Point, Vector2 Normal);
