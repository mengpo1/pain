namespace TopDown.Core.Definitions;

/// <summary>Every gameplay definition available to a world, indexed by id.</summary>
public sealed class DefinitionSet
{
    public DefinitionSet(
        IReadOnlyDictionary<string, WeaponDefinition> weapons,
        IReadOnlyDictionary<string, ActorDefinition> actors,
        IReadOnlyDictionary<string, TileDefinition> tiles,
        IReadOnlyDictionary<string, DoorDefinition> doors,
        IReadOnlyDictionary<string, ModifierDefinition> modifiers,
        GameRules rules)
    {
        Weapons = weapons;
        Actors = actors;
        Tiles = tiles;
        Doors = doors;
        Modifiers = modifiers;
        Rules = rules;
    }

    public IReadOnlyDictionary<string, WeaponDefinition> Weapons { get; }
    public IReadOnlyDictionary<string, ActorDefinition> Actors { get; }
    public IReadOnlyDictionary<string, TileDefinition> Tiles { get; }
    public IReadOnlyDictionary<string, DoorDefinition> Doors { get; }
    public IReadOnlyDictionary<string, ModifierDefinition> Modifiers { get; }
    public GameRules Rules { get; }

    public WeaponDefinition Weapon(string id) => Find(Weapons, id, "weapon");
    public ActorDefinition Actor(string id) => Find(Actors, id, "actor");
    public TileDefinition Tile(string id) => Find(Tiles, id, "tile");
    public DoorDefinition Door(string id) => Find(Doors, id, "door");

    private static T Find<T>(IReadOnlyDictionary<string, T> table, string id, string kind) =>
        table.TryGetValue(id, out T? value)
            ? value
            : throw new KeyNotFoundException($"Unknown {kind} definition '{id}'.");
}
