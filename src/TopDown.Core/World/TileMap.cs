using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>
/// Static level geometry: a grid of tile definitions. A solid tile is a full square, unless its
/// definition gives a <see cref="TileDefinition.Thickness"/>: then it is a thin wall (see <see cref="Solids"/>).
/// </summary>
public sealed class TileMap
{
    private readonly TileDefinition[] _tiles;
    private readonly bool[] _joints;
    private RectF[][]? _solids;
    private int _solidsVersion = -1;

    public TileMap(int width, int height, float tileSize, TileDefinition fill)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("A map needs at least one tile.");
        if (tileSize <= 0f) throw new ArgumentOutOfRangeException(nameof(tileSize));
        Width = width;
        Height = height;
        TileSize = tileSize;
        _tiles = new TileDefinition[width * height];
        Array.Fill(_tiles, fill);
        _joints = new bool[width * height];
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

    /// <summary>A doorway: thin walls next to it reach into it, so that they meet the door.</summary>
    internal void MarkJoint(int x, int y)
    {
        if (!InBounds(x, y) || _joints[y * Width + x]) return;
        _joints[y * Width + x] = true;
        Version++;
    }

    /// <summary>A doorway marked with <see cref="MarkJoint"/>.</summary>
    public bool IsJoint(int x, int y) => InBounds(x, y) && _joints[y * Width + x];

    /// <summary>True if the tile is drawn and collides as a thin wall.</summary>
    public bool IsThin(int x, int y) => IsThin(Get(x, y));

    private bool IsThin(TileDefinition tile) =>
        tile.Thickness is { } thickness && thickness > 0f && thickness < TileSize && tile.Blocking != Blocking.None;

    /// <summary>
    /// The solid parts of a tile, in world units: none for an open tile, the whole square for a
    /// solid one. A thin wall keeps what stays within half its thickness of the lines joining its
    /// centre to the neighbouring walls and doorways (the wall area shrunk by (tile - thickness) / 2):
    /// a band through a wall line, an L at a corner, a solid block inside a wall mass.
    /// </summary>
    public IReadOnlyList<RectF> Solids(int x, int y)
    {
        if (!InBounds(x, y)) return [TileRect(x, y)];
        if (_solidsVersion != Version)
        {
            _solids = new RectF[_tiles.Length][];
            _solidsVersion = Version;
        }

        int index = y * Width + x;
        return _solids![index] ??= ComputeSolids(x, y);
    }

    private RectF[] ComputeSolids(int x, int y)
    {
        TileDefinition tile = Get(x, y);
        if (tile.Blocking == Blocking.None) return [];
        if (!IsThin(tile)) return [TileRect(x, y)];

        float margin = (TileSize - tile.Thickness!.Value) * 0.5f;
        float[] edges = [x * TileSize, x * TileSize + margin, (x + 1) * TileSize - margin, (x + 1) * TileSize];
        float[] rows = [y * TileSize, y * TileSize + margin, (y + 1) * TileSize - margin, (y + 1) * TileSize];
        // Cell (column, row) of a 3 x 3 split: the centre is always solid, a side when the wall goes on
        // that way, a corner when the walls on both sides and across the corner all do.
        bool Solid(int column, int row)
        {
            int dx = column - 1, dy = row - 1;
            if (dx != 0 && !Joins(x + dx, y)) return false;
            if (dy != 0 && !Joins(x, y + dy)) return false;
            return dx == 0 || dy == 0 || Joins(x + dx, y + dy);
        }

        var solids = new List<RectF>(3);
        for (int row = 0; row < 3; row++)
        {
            int column = 0;
            while (column < 3)
            {
                if (!Solid(column, row)) { column++; continue; }
                int end = column;
                while (end + 1 < 3 && Solid(end + 1, row)) end++;
                var span = new RectF(edges[column], rows[row], edges[end + 1] - edges[column], rows[row + 1] - rows[row]);
                // Same span as the band above: extend it instead of adding a rectangle.
                int last = solids.Count - 1;
                if (last >= 0 && solids[last].Left == span.Left && solids[last].Right == span.Right && solids[last].Bottom == span.Top)
                    solids[last] = new RectF(span.Left, solids[last].Top, span.Width, span.Bottom - solids[last].Top);
                else solids.Add(span);
                column = end + 1;
            }
        }

        return solids.ToArray();
    }

    /// <summary>True if the point lies in a solid part of a tile (outside the map counts as solid).</summary>
    public bool IsSolidAt(Vector2 point)
    {
        (int x, int y) = TileAt(point);
        foreach (RectF solid in Solids(x, y))
        {
            if (solid.Contains(point)) return true;
        }

        return false;
    }

    /// <summary>Walls (thin or not) and doorways join a thin wall; open floor and the outside do not.</summary>
    private bool Joins(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        if (_joints[y * Width + x]) return true;
        TileDefinition tile = _tiles[y * Width + x];
        return tile != TileDefinition.Void && tile.Layer == TileLayer.Wall && tile.Blocking != Blocking.None;
    }

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
        if (Blocks(x, y, mask) && HitsTile(x, y, from, delta, 0f, -Geo.NormalizeOrZero(delta), out hit)) return true;

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
            if (Blocks(x, y, mask) && HitsTile(x, y, from, delta, t, normal, out hit)) return true;
        }

        hit = default;
        return false;
    }

    /// <summary>
    /// The segment, which enters tile (x, y) at <paramref name="entry"/>, hits its solid part. A full tile
    /// is hit where the segment enters it; a thin wall only if the segment crosses one of its bands.
    /// </summary>
    private bool HitsTile(int x, int y, Vector2 from, Vector2 delta, float entry, Vector2 entryNormal, out TileHit hit)
    {
        hit = default;
        if (!IsThin(x, y))
        {
            hit = new TileHit(x, y, entry, from + delta * entry, entryNormal);
            return true;
        }

        float best = float.PositiveInfinity;
        foreach (RectF solid in Solids(x, y))
        {
            if (!Geo.SegmentRect(from, delta, solid, out float t, out Vector2 normal) || t >= best) continue;
            best = t;
            hit = new TileHit(x, y, t, from + delta * t, normal);
        }

        return best <= 1f;
    }
}

public readonly record struct TileHit(int X, int Y, float Fraction, Vector2 Point, Vector2 Normal);
