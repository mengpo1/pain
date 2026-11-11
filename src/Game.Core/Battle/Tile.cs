using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// One cell of the battle map: up to four parts (floor, west wall, north wall, object), the unit
/// standing in it, items lying on it, smoke and fire, and what each side has discovered.
/// </summary>
public sealed class Tile
{
    private readonly TilePartDefinition?[] _parts = new TilePartDefinition?[4];
    private readonly int[] _damage = new int[4];
    private readonly bool[] _discovered = new bool[Factions.Count];

    public Tile(Position position) => Position = position;

    public Position Position { get; }

    public TilePartDefinition? this[TilePartSlot slot]
    {
        get => _parts[(int)slot];
        set
        {
            _parts[(int)slot] = value;
            _damage[(int)slot] = 0;
        }
    }

    public TilePartDefinition? Floor => _parts[(int)TilePartSlot.Floor];
    public TilePartDefinition? WestWall => _parts[(int)TilePartSlot.WestWall];
    public TilePartDefinition? NorthWall => _parts[(int)TilePartSlot.NorthWall];
    public TilePartDefinition? Object => _parts[(int)TilePartSlot.Object];

    public BattleUnit? Unit { get; set; }

    public List<BattleItem> Items { get; } = new();

    /// <summary>Smoke density (0 = none). Reduces vision through the tile.</summary>
    public int Smoke { get; set; }

    /// <summary>Turns of fire left on the tile (0 = not burning).</summary>
    public int Fire { get; set; }

    /// <summary>Named zone from the map (extraction point…), or null.</summary>
    public string? Zone { get; set; }

    /// <summary>Damage accumulated by a part since it was placed (see <see cref="TilePartDefinition.Armor"/>).</summary>
    public int DamageOf(TilePartSlot slot) => _damage[(int)slot];

    internal void AddDamage(TilePartSlot slot, int amount) => _damage[(int)slot] += amount;

    public bool IsDiscovered(Faction faction) => _discovered[(int)faction];

    internal void Discover(Faction faction) => _discovered[(int)faction] = true;

    /// <summary>Height in voxels of the surface a unit stands on (floor or object such as stairs).</summary>
    public int SurfaceHeight => Math.Max(Floor?.StandHeight ?? 0, Object?.StandHeight ?? 0);

    public bool IsEmpty => _parts.All(p => p is null);

    public override string ToString() => $"Tile {Position}";
}
