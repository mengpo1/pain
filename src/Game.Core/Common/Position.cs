namespace Game.Core.Common;

/// <summary>
/// Tile coordinates on a battle map: X grows to the east, Y to the south, Z is the level
/// (0 = ground). Same axes as OpenXcom, so "north" is -Y.
/// </summary>
public readonly record struct Position(int X, int Y, int Z)
{
    public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Position operator -(Position a, Position b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public Position Offset(int dx, int dy, int dz = 0) => new(X + dx, Y + dy, Z + dz);

    public Position Step(int direction)
    {
        (int dx, int dy) = Directions.Delta(direction);
        return new Position(X + dx, Y + dy, Z);
    }

    /// <summary>Horizontal distance in tiles (euclidean), as used for weapon ranges and vision.</summary>
    public double HorizontalDistance(Position other)
    {
        int dx = other.X - X, dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public int HorizontalDistanceSquared(Position other)
    {
        int dx = other.X - X, dy = other.Y - Y;
        return dx * dx + dy * dy;
    }

    public override string ToString() => $"({X}, {Y}, {Z})";
}

/// <summary>A point in voxel space: a tile is 16 × 16 voxels wide and 24 voxels high (OpenXcom scale).</summary>
public readonly record struct Voxel(int X, int Y, int Z)
{
    public const int TileWidth = 16;
    public const int TileHeight = 24;

    public Position Tile => new(FloorDiv(X, TileWidth), FloorDiv(Y, TileWidth), FloorDiv(Z, TileHeight));

    /// <summary>Centre of a tile, at <paramref name="height"/> voxels above its floor.</summary>
    public static Voxel TileCenter(Position tile, int height) =>
        new(tile.X * TileWidth + TileWidth / 2, tile.Y * TileWidth + TileWidth / 2, tile.Z * TileHeight + height);

    public double DistanceTo(Voxel other)
    {
        double dx = other.X - X, dy = other.Y - Y, dz = other.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

    public override string ToString() => $"<{X}, {Y}, {Z}>";
}
