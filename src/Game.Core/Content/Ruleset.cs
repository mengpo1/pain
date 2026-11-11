using Game.Core.Definitions;

namespace Game.Core.Content;

/// <summary>
/// Every definition of the loaded game, by id (OpenXcom calls this "the mod"). Built by the
/// loader, read-only afterwards.
/// </summary>
public sealed class Ruleset
{
    public IReadOnlyDictionary<string, StatDefinition> Stats { get; init; } = new Dictionary<string, StatDefinition>();
    public IReadOnlyDictionary<string, DamageTypeDefinition> DamageTypes { get; init; } = new Dictionary<string, DamageTypeDefinition>();
    public IReadOnlyDictionary<string, ShapeDefinition> Shapes { get; init; } = new Dictionary<string, ShapeDefinition>();
    public IReadOnlyDictionary<string, TilePartDefinition> TileParts { get; init; } = new Dictionary<string, TilePartDefinition>();
    public IReadOnlyDictionary<string, MapBlockDefinition> MapBlocks { get; init; } = new Dictionary<string, MapBlockDefinition>();
    public IReadOnlyDictionary<string, TerrainDefinition> Terrains { get; init; } = new Dictionary<string, TerrainDefinition>();
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; init; } = new Dictionary<string, ItemDefinition>();
    public IReadOnlyDictionary<string, ArmorDefinition> Armors { get; init; } = new Dictionary<string, ArmorDefinition>();
    public IReadOnlyDictionary<string, UnitDefinition> Units { get; init; } = new Dictionary<string, UnitDefinition>();
    public IReadOnlyDictionary<string, InventorySlotDefinition> InventorySlots { get; init; } = new Dictionary<string, InventorySlotDefinition>();
    public IReadOnlyDictionary<string, MissionDefinition> Missions { get; init; } = new Dictionary<string, MissionDefinition>();
    public BattleRules Battle { get; init; } = new();

    /// <summary>Stats in display order.</summary>
    public IEnumerable<StatDefinition> OrderedStats => Stats.Values.OrderBy(s => s.Order).ThenBy(s => s.Id, StringComparer.Ordinal);
}
