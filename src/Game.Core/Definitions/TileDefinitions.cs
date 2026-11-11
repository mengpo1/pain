using System.Text.Json.Serialization;
using Game.Core.Content;

namespace Game.Core.Definitions;

/// <summary>
/// The four parts a map tile can hold, as in OpenXcom: a floor, a wall on the west edge, a wall on
/// the north edge, and an object (furniture, tree, stairs…). Walls sit on tile edges, so a door or
/// a window separates two tiles without occupying either.
/// </summary>
public enum TilePartSlot
{
    Floor,
    WestWall,
    NorthWall,
    Object,
}

public enum DoorKind
{
    None,

    /// <summary>Opens when walked through and stays open (wooden door).</summary>
    Swing,

    /// <summary>Opens when walked through and closes again at the end of the turn if nobody stands in it.</summary>
    Sliding,
}

/// <summary>
/// Physical shape of a tile part in voxel space, as axis-aligned boxes inside the tile
/// (16 × 16 × 24 voxels). Projectiles and lines of sight are tested against these boxes.
/// </summary>
public sealed class ShapeDefinition : Definition
{
    /// <summary>Each box is [x0, y0, z0, x1, y1, z1], minimum included, maximum excluded.</summary>
    public IReadOnlyList<int[]> Boxes { get; init; } = [];

    public bool Contains(int x, int y, int z)
    {
        foreach (int[] b in Boxes)
            if (x >= b[0] && y >= b[1] && z >= b[2] && x < b[3] && y < b[4] && z < b[5]) return true;
        return false;
    }
}

/// <summary>
/// What a floor, wall or object is (OpenXcom "MCD" entry): cost to cross, what it blocks, how
/// solid it is and what it becomes when destroyed.
/// </summary>
public sealed class TilePartDefinition : Definition
{
    public const int Impassable = 255;

    /// <summary>Logical sprite id.</summary>
    public string Sprite { get; init; } = "";

    /// <summary>Shape id (voxels). Empty = no physical presence.</summary>
    public string Shape { get; init; } = "";

    /// <summary>
    /// Floors and objects: TU to walk onto the tile (<see cref="Impassable"/> = cannot enter).
    /// Walls: not used; a wall blocks the edge unless <see cref="Walkable"/> or an open door.
    /// </summary>
    public int TuWalk { get; init; }

    /// <summary>Walls only: units can cross the edge (low fence, broken wall).</summary>
    public bool Walkable { get; init; }

    /// <summary>Stops lines of sight through the tile edge or the tile (OpenXcom stopLOS).</summary>
    public bool BlocksSight { get; init; }

    /// <summary>
    /// Power an explosion loses crossing this part, counted twice as in OXCE (HE_Block); 255 or
    /// more stops it. Also stops smoke when positive.
    /// </summary>
    public int ExplosionBlock { get; init; }

    /// <summary>Height in voxels (0–24) of the surface a unit stands on (stairs, crates).</summary>
    public int StandHeight { get; init; }

    /// <summary>Damage resistance; a hit or blast stronger than this destroys the part. 0 = indestructible.</summary>
    public int Armor { get; init; }

    /// <summary>Part that replaces this one when destroyed (rubble); null = nothing left.</summary>
    public string? DestroyedInto { get; init; }

    public DoorKind Door { get; init; }

    /// <summary>For doors: the part shown while open.</summary>
    public string? OpenInto { get; init; }

    /// <summary>For open doors: the part restored when closing.</summary>
    public string? ClosedInto { get; init; }

    /// <summary>Light emitted on the tile (0–15).</summary>
    public int Light { get; init; }

    /// <summary>Sound id played when walked on (floors).</summary>
    public string? FootstepSound { get; init; }

    /// <summary>Sound id played when destroyed.</summary>
    public string? DestroySound { get; init; }

    /// <summary>The shape definition, looked up once (voxel tests run millions of times).</summary>
    [JsonIgnore]
    internal ShapeDefinition? ResolvedShape { get; set; }
}

/// <summary>One symbol of a map block legend: the parts placed on a tile, plus markers.</summary>
public sealed class MapCellDefinition
{
    public string? Floor { get; init; }
    public string? West { get; init; }
    public string? North { get; init; }
    public string? Object { get; init; }

    /// <summary>Deployment marker: "player", "hostile", "neutral" (units may start here).</summary>
    public string? Spawn { get; init; }

    /// <summary>Patrol point for the AI.</summary>
    public bool Patrol { get; init; }

    /// <summary>Named zone (extraction point, objective area).</summary>
    public string? Zone { get; init; }
}

/// <summary>
/// A piece of map drawn as text, level by level: each character is a key of <see cref="Legend"/>.
/// Rows go from north (first) to south, characters from west to east. Blocks are combined by the
/// map generator, or used alone as a fixed map.
/// </summary>
public sealed class MapBlockDefinition : Definition
{
    /// <summary>Width (X), length (Y) and number of levels (Z).</summary>
    public int[] Size { get; init; } = [10, 10, 1];

    public IReadOnlyDictionary<string, MapCellDefinition> Legend { get; init; } = new Dictionary<string, MapCellDefinition>();

    /// <summary>Levels from the ground up; each level is a list of rows.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Levels { get; init; } = [];

    public int Width => Size.Length > 0 ? Size[0] : 0;
    public int Length => Size.Length > 1 ? Size[1] : 0;
    public int Height => Size.Length > 2 ? Size[2] : 1;
}

/// <summary>
/// An environment for generated maps (OpenXcom "terrain"): which blocks can be used and how often.
/// </summary>
public sealed class TerrainDefinition : Definition
{
    public string NameKey { get; init; } = "";

    /// <summary>Size of one block in tiles (blocks of this size fill the map grid).</summary>
    public int BlockSize { get; init; } = 10;

    public IReadOnlyList<TerrainBlockEntry> Blocks { get; init; } = [];

    /// <summary>Global light level of missions on this terrain unless the mission says otherwise.</summary>
    public int? Light { get; init; }
}

public sealed class TerrainBlockEntry
{
    public string Block { get; init; } = "";

    /// <summary>Relative chance of being picked.</summary>
    public int Weight { get; init; } = 1;

    /// <summary>Maximum number of copies on one map (0 = unlimited).</summary>
    public int Max { get; init; }

    /// <summary>Placed first, before random blocks (landing zone, objective building).</summary>
    public int Required { get; init; }
}
