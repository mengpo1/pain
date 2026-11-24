using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>Raised when a level cannot be built. Content validation should catch these first.</summary>
public sealed class LevelFormatException(string message) : Exception(message);

/// <summary>Turns a <see cref="LevelDefinition"/> into a fresh <see cref="GameWorld"/>.</summary>
internal static class LevelBuilder
{
    /// <summary>Weapon id meaning "no weapon" in level files.</summary>
    public const string NoWeapon = "none";

    public static GameWorld Build(LevelDefinition level, DefinitionSet definitions, ModifierDefinition modifier, int seed)
    {
        TileMap map = BuildMap(level, definitions);
        var world = new GameWorld(level, definitions, map, modifier, seed);

        SpawnPlayer(world, level, definitions, modifier);
        foreach (LevelActor placement in level.Actors) SpawnEnemy(world, level, definitions, placement);

        foreach (LevelWeapon placement in level.Weapons)
        {
            var weapon = new WeaponInstance(Find(definitions.Weapons, placement.Definition, "weapon", level), placement.Ammo);
            world.AddWeapon(new WorldWeapon(weapon, level.ToWorld(placement.Position), Geo.DegToRad(placement.RotationDegrees)));
        }

        foreach (LevelDoor placement in level.Doors.Concat(DoorsFromSymbols(level, map)).ToList())
        {
            world.AddDoor(BuildDoor(level, definitions, placement));
            // Thin walls reach into the doorway to meet the door.
            map.MarkJoint((int)placement.Tile.X, (int)placement.Tile.Y);
        }

        foreach (LevelProp prop in level.Props)
        {
            RectF bounds = RectF.FromCenter(level.ToWorld(prop.Position), prop.Size.X * level.TileSize, prop.Size.Y * level.TileSize);
            world.AddProp(new Prop(prop, bounds));
        }

        foreach (LevelItem item in level.Items) world.AddItem(new ItemPickup(item, level.ToWorld(item.Position)));
        foreach (LevelInteractable interactable in level.Interactables)
            world.AddInteractable(new Interactable(interactable, level.ToWorld(interactable.Position)));

        foreach ((string name, LevelZone zone) in level.Zones)
            world.AddZone(name, new RectF(zone.X * level.TileSize, zone.Y * level.TileSize, zone.Width * level.TileSize, zone.Height * level.TileSize));

        world.FinishBuilding();
        return world;
    }

    private static TileMap BuildMap(LevelDefinition level, DefinitionSet definitions)
    {
        if (level.Width == 0) throw new LevelFormatException($"Level '{level.Id}' has an empty map.");
        var map = new TileMap(level.Width, level.Height, level.TileSize, TileDefinition.Void);
        for (int y = 0; y < level.Height; y++)
        {
            string row = level.Map[y];
            for (int x = 0; x < row.Length; x++)
            {
                string symbol = row[x].ToString();
                if (level.DoorSymbols.TryGetValue(symbol, out LevelDoorSymbol? door))
                {
                    map.Set(x, y, ResolveTile(definitions, door.Tile, level));
                    continue;
                }

                if (!level.Legend.TryGetValue(symbol, out string? tileId))
                {
                    if (symbol == " ") continue;
                    throw new LevelFormatException($"Level '{level.Id}': character '{symbol}' at ({x}, {y}) is not in the legend.");
                }

                map.Set(x, y, ResolveTile(definitions, tileId, level));
            }
        }

        return map;
    }

    /// <summary>Doors drawn in the map with a door symbol; walls on the left and right mean a horizontal door.</summary>
    private static IEnumerable<LevelDoor> DoorsFromSymbols(LevelDefinition level, TileMap map)
    {
        if (level.DoorSymbols.Count == 0) yield break;
        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Map[y].Length; x++)
            {
                if (!level.DoorSymbols.TryGetValue(level.Map[y][x].ToString(), out LevelDoorSymbol? symbol)) continue;
                // Neighbouring door symbols count as wall, so double doors get the wall's axis.
                bool WallOrDoor(int nx, int ny) => map.Blocks(nx, ny, Blocking.Movement)
                    || (ny >= 0 && ny < level.Height && nx >= 0 && nx < level.Map[ny].Length && level.DoorSymbols.ContainsKey(level.Map[ny][nx].ToString()));
                bool vertical = WallOrDoor(x, y - 1) && WallOrDoor(x, y + 1);
                bool horizontal = !vertical && WallOrDoor(x - 1, y) && WallOrDoor(x + 1, y);
                yield return new LevelDoor
                {
                    Definition = symbol.Door,
                    Tile = new Vector2(x, y),
                    Axis = horizontal ? DoorAxis.Horizontal : DoorAxis.Vertical,
                    Hinge = symbol.Hinge,
                    Locked = symbol.Locked,
                };
            }
        }
    }

    private static TileDefinition ResolveTile(DefinitionSet definitions, string tileId, LevelDefinition level)
    {
        if (definitions.Tiles.TryGetValue(tileId, out TileDefinition? tile)) return tile;
        if (tileId == TileDefinition.Void.Id) return TileDefinition.Void;
        throw new LevelFormatException($"Level '{level.Id}': unknown tile '{tileId}'.");
    }

    private static void SpawnPlayer(GameWorld world, LevelDefinition level, DefinitionSet definitions, ModifierDefinition modifier)
    {
        ActorDefinition definition = Find(definitions.Actors, level.Player.Definition, "actor", level);
        Actor player = world.SpawnActor(definition, true, level.ToWorld(level.Player.Position), Geo.DegToRad(level.Player.FacingDegrees));
        player.SpeedMultiplier = modifier.MoveSpeed;

        string? weaponId = modifier.StartingWeaponId ?? level.Player.WeaponId ?? definition.WeaponId;
        if (weaponId is not null && weaponId != NoWeapon)
        {
            WeaponDefinition weapon = Find(definitions.Weapons, weaponId, "weapon", level);
            if (modifier.AllowFirearms || !weapon.IsFirearm) player.Weapon = new WeaponInstance(weapon);
        }

        world.GiveLoot(level.Player.Inventory, player.Position, announce: false);
    }

    internal static Actor SpawnEnemy(GameWorld world, LevelDefinition level, DefinitionSet definitions, LevelActor placement)
    {
        ActorDefinition definition = Find(definitions.Actors, placement.Definition, "actor", level);
        float facing = Geo.DegToRad(placement.FacingDegrees);
        Actor actor = world.SpawnActor(definition, false, level.ToWorld(placement.Position), facing);
        actor.LevelId = placement.Id;
        actor.Tag = placement.Tag;

        string? weaponId = placement.WeaponId ?? definition.WeaponId;
        if (weaponId is not null && weaponId != NoWeapon)
            actor.Weapon = new WeaponInstance(Find(definitions.Weapons, weaponId, "weapon", level));

        List<Vector2> patrol = placement.Patrol.Select(level.ToWorld).ToList();
        actor.Brain = new EnemyBrain(actor, world, placement.Behavior ?? definition.Ai.Behavior, patrol);
        return actor;
    }

    private static Door BuildDoor(LevelDefinition level, DefinitionSet definitions, LevelDoor placement)
    {
        DoorDefinition definition = Find(definitions.Doors, placement.Definition, "door", level);
        float size = level.TileSize;
        int tileX = (int)placement.Tile.X;
        int tileY = (int)placement.Tile.Y;
        bool start = placement.Hinge == DoorHinge.Start;

        (Vector2 hinge, float angle) = placement.Axis == DoorAxis.Vertical
            ? (new Vector2((tileX + 0.5f) * size, (start ? tileY : tileY + 1) * size), start ? MathF.PI / 2f : -MathF.PI / 2f)
            : (new Vector2((start ? tileX : tileX + 1) * size, (tileY + 0.5f) * size), start ? 0f : MathF.PI);

        return new Door(definition, hinge, angle, size, placement.Locked, placement.Id);
    }

    private static T Find<T>(IReadOnlyDictionary<string, T> table, string id, string kind, LevelDefinition level) =>
        table.TryGetValue(id, out T? value)
            ? value
            : throw new LevelFormatException($"Level '{level.Id}' references unknown {kind} '{id}'.");
}
