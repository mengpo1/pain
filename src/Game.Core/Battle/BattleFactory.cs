using Game.Core.Battle.Generation;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// Builds a ready-to-play battle from a mission: map (fixed or generated with the seed), units
/// placed on the map's deployment markers, starting equipment.
/// </summary>
public static class BattleFactory
{
    public static Battle Create(Ruleset rules, MissionDefinition mission, ulong seed)
    {
        var random = new GameRandom(seed);
        BattleMap map;
        int? terrainLight = null;
        if (mission.Map.Block is { } blockId)
        {
            map = MapBuilder.FromBlock(rules.MapBlocks[blockId], rules);
        }
        else
        {
            TerrainDefinition terrain = rules.Terrains[mission.Map.Terrain!];
            map = MapBuilder.Generate(terrain, mission.Map.BlocksX, mission.Map.BlocksY, mission.Map.Levels, rules, random);
            terrainLight = terrain.Light;
        }

        map.GlobalLight = mission.Light ?? terrainLight ?? 15;
        var battle = new Battle(rules, mission, map, random);
        Deploy(battle, Faction.Player, mission.Player);
        Deploy(battle, Faction.Hostile, mission.Hostile);
        Deploy(battle, Faction.Neutral, mission.Neutral);
        battle.Start();
        return battle;
    }

    private static void Deploy(Battle battle, Faction faction, IReadOnlyList<DeploymentEntry> entries)
    {
        BattleMap map = battle.Map;
        List<Position> spawns = map.Spawns.Where(s => s.Faction == faction).Select(s => s.Position).ToList();
        Shuffle(spawns, battle.Random);
        var queue = new Queue<Position>(spawns);
        var center = new Position(map.Width / 2, map.Length / 2, 0);

        foreach (DeploymentEntry entry in entries)
        {
            UnitDefinition definition = battle.Rules.Units[entry.Unit];
            for (int i = 0; i < entry.Count; i++)
            {
                Position? place = NextFree(battle, queue, spawns);
                if (place is null) throw new InvalidOperationException($"No room to deploy '{entry.Unit}' for {faction}: add spawn markers to the map.");
                BattleUnit unit = battle.AddUnit(definition, faction, place.Value, Directions.Towards(place.Value, center with { Z = place.Value.Z }));
                Equip(battle, unit);
            }
        }
    }

    /// <summary>The next free spawn marker, then free tiles spreading out from the markers.</summary>
    private static Position? NextFree(Battle battle, Queue<Position> queue, List<Position> spawns)
    {
        while (queue.TryDequeue(out Position p))
            if (CanStand(battle, p)) return p;

        var seen = new HashSet<Position>(spawns);
        var frontier = new Queue<Position>(spawns);
        while (frontier.TryDequeue(out Position p))
        {
            if (CanStand(battle, p)) return p;
            for (int d = 0; d < Directions.Count; d += 2)
            {
                Position next = p.Step(d);
                if (battle.Map.InBounds(next) && seen.Add(next)) frontier.Enqueue(next);
            }
        }

        return null;
    }

    private static bool CanStand(Battle battle, Position p)
    {
        Tile? tile = battle.Map[p];
        return tile?.Floor is not null && tile.Unit is null && tile.Object is not { TuWalk: TilePartDefinition.Impassable };
    }

    /// <summary>Gives a unit its loadout: firearms come loaded with the listed ammo.</summary>
    public static void Equip(Battle battle, BattleUnit unit)
    {
        foreach (LoadoutEntry entry in unit.Definition.Loadout)
        {
            BattleItem item = battle.CreateItem(battle.Rules.Items[entry.Item]);
            if (entry.Ammo is { } ammoId) Inventory.Load(null, item, battle.CreateItem(battle.Rules.Items[ammoId]));
            bool placed = entry.Slot is { } slot ? Inventory.TryPlace(battle, unit, item, slot) : Inventory.TryPlaceAnywhere(battle, unit, item);
            if (!placed) battle.DropItem(item, unit.Position);
        }
    }

    private static void Shuffle<T>(List<T> list, GameRandom random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
