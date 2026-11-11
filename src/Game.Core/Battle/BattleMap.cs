using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>A deployment point read from the map (legend "spawn").</summary>
public sealed record SpawnPoint(Position Position, Faction Faction);

/// <summary>
/// The three-dimensional grid of tiles. Only geometry lives here; units and items are placed on
/// tiles by <see cref="Battle"/>.
/// </summary>
public sealed class BattleMap
{
    private readonly Tile[] _tiles;

    public BattleMap(int width, int length, int height)
    {
        if (width <= 0 || length <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be positive.");
        Width = width;
        Length = length;
        Height = height;
        _tiles = new Tile[width * length * height];
        for (int z = 0; z < height; z++)
            for (int y = 0; y < length; y++)
                for (int x = 0; x < width; x++)
                    _tiles[Index(x, y, z)] = new Tile(new Position(x, y, z));
    }

    public int Width { get; }
    public int Length { get; }
    public int Height { get; }

    public List<SpawnPoint> Spawns { get; } = new();
    public List<Position> PatrolPoints { get; } = new();

    /// <summary>Global light level (0 = dark night, 15 = full day), see BattleRules.</summary>
    public int GlobalLight { get; set; } = 15;

    public IEnumerable<Tile> Tiles => _tiles;

    public bool InBounds(Position p) => InBounds(p.X, p.Y, p.Z);

    public bool InBounds(int x, int y, int z) =>
        (uint)x < (uint)Width && (uint)y < (uint)Length && (uint)z < (uint)Height;

    public Tile? this[Position p] => InBounds(p) ? _tiles[Index(p.X, p.Y, p.Z)] : null;

    public Tile? this[int x, int y, int z] => InBounds(x, y, z) ? _tiles[Index(x, y, z)] : null;

    /// <summary>Height in voxels (0–24) of the surface a unit standing on this tile has its feet on.</summary>
    public int StandElevation(Position p) => this[p]?.SurfaceHeight ?? 0;

    /// <summary>The wall part on the edge crossed when stepping from a tile to an orthogonal neighbour.</summary>
    public TilePartDefinition? EdgeWall(Position from, int orthogonalDirection)
    {
        return Directions.Normalize(orthogonalDirection) switch
        {
            0 => this[from]?.NorthWall,
            2 => this[from.X + 1, from.Y, from.Z]?.WestWall,
            4 => this[from.X, from.Y + 1, from.Z]?.NorthWall,
            6 => this[from]?.WestWall,
            _ => throw new ArgumentException("Only orthogonal directions cross a single edge.", nameof(orthogonalDirection)),
        };
    }

    /// <summary>Tile and slot holding the wall of that edge (for doors and destruction).</summary>
    public (Tile? Tile, TilePartSlot Slot) EdgeWallLocation(Position from, int orthogonalDirection)
    {
        return Directions.Normalize(orthogonalDirection) switch
        {
            0 => (this[from], TilePartSlot.NorthWall),
            2 => (this[from.X + 1, from.Y, from.Z], TilePartSlot.WestWall),
            4 => (this[from.X, from.Y + 1, from.Z], TilePartSlot.NorthWall),
            6 => (this[from], TilePartSlot.WestWall),
            _ => throw new ArgumentException("Only orthogonal directions cross a single edge.", nameof(orthogonalDirection)),
        };
    }

    private int Index(int x, int y, int z) => (z * Length + y) * Width + x;
}
