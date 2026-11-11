using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Area damage, after OXCE TileEngine::explode with its default flat explosions
/// (battleExplosionHeight 0): rays leave the centre every 3°; on each ray the power drops by the
/// type's radius reduction (10) per tile and by twice the explosion blockage of the walls and
/// objects crossed. Every tile reached once takes randomised damage on its unit and items; the
/// terrain takes the highest power that reached it × toTile, destroying layers whose armour it
/// covers.
/// </summary>
public static class Explosions
{
    private const int RayStepDegrees = 3;

    /// <summary>True if this damage explodes (an item radius, or a type with radius effectiveness).</summary>
    public static bool IsExplosive(DamageDefinition damage, DamageTypeDefinition type) =>
        damage.Radius is > 0 || (damage.Radius is null && type.RadiusEffectiveness > 0);

    /// <summary>Item radius, or power × radius effectiveness (OXCE: HE power / 20), between 1 and the maximum.</summary>
    public static int Radius(BattleRules rules, DamageDefinition damage, DamageTypeDefinition type) =>
        Math.Clamp(damage.Radius ?? (int)(damage.Power * type.RadiusEffectiveness), 1, rules.MaxExplosionRadius);

    public static void Detonate(Battle battle, Position center, DamageDefinition damage, BattleUnit? attacker)
    {
        if (!battle.Rules.DamageTypes.TryGetValue(damage.Type, out DamageTypeDefinition? type)) return;
        BattleRules rules = battle.Rules.Battle;
        int radius = Radius(rules, damage, type);
        var tileDamage = new Dictionary<Position, int>();
        var visited = new HashSet<Position>();

        for (int degrees = 0; degrees <= 360; degrees += RayStepDegrees)
        {
            double angle = degrees * Math.PI / 180.0;
            double power = damage.Power;
            Position current = center;
            for (int step = 0; step <= radius && power > 0; step++)
            {
                Position tile = step == 0 ? center : new Position(
                    (int)Math.Floor(center.X + 0.5 + step * Math.Sin(angle)),
                    (int)Math.Floor(center.Y + 0.5 + step * Math.Cos(angle)),
                    center.Z);
                if (!battle.Map.InBounds(tile)) break;
                if (tile != current)
                {
                    if (Math.Abs(tile.X - current.X) > 1 || Math.Abs(tile.Y - current.Y) > 1) break;
                    power -= 2 * Blockage(battle.Map, current, tile, skipStartObject: step == 1);
                    if (power <= 0) break;
                }

                int local = (int)power;
                int tilePower = (int)Math.Round(local * type.ToTile, MidpointRounding.AwayFromZero);
                tileDamage[tile] = Math.Max(tileDamage.GetValueOrDefault(tile), tilePower);
                if (visited.Add(tile)) HitTile(battle, center, tile, local, type, attacker);
                current = tile;
                power -= type.RadiusReduction;
            }
        }

        foreach ((Position tile, int energy) in tileDamage.OrderBy(t => t.Key.Z).ThenBy(t => t.Key.Y).ThenBy(t => t.Key.X))
            Demolish(battle, tile, energy);
        battle.RefreshVision();
    }

    /// <summary>A tile reached by the blast for the first time: its unit, bodies lying there, and items.</summary>
    private static void HitTile(Battle battle, Position center, Position tile, int power, DamageTypeDefinition type, BattleUnit? attacker)
    {
        BattleRules rules = battle.Rules.Battle;
        int damage = DamageRules.RandomDamage(rules, type, power, battle.Random);
        if (battle.Map[tile]!.Unit is { IsActive: true } unit)
        {
            // OXCE: on the centre or right beside it the underside is hit; further away, the side facing the blast.
            bool under = tile.HorizontalDistanceSquared(center) < 4;
            int rx = under ? 0 : center.X - tile.X, ry = under ? 0 : center.Y - tile.Y, rz = under ? -1 : 5;
            DamageRules.Damage(battle, attacker, unit, rx, ry, rz, damage, type);
        }

        foreach (BattleItem item in battle.Map[tile]!.Items.ToList())
        {
            if (item.Body is { Status: UnitStatus.Unconscious } body)
            {
                DamageRules.Damage(battle, attacker, body, 0, 0, 0, damage, type);
                continue;
            }

            if (Math.Round(damage * type.ToItem, MidpointRounding.AwayFromZero) > rules.ItemArmor) battle.DestroyItem(item);
        }
    }

    /// <summary>
    /// OXCE explosion blockage between neighbouring tiles: the wall on the edge crossed, or for a
    /// diagonal the average of the two L-shaped paths plus the average of the corner objects; plus
    /// the object of the tile left (except on the first step).
    /// </summary>
    public static int Blockage(BattleMap map, Position from, Position to, bool skipStartObject)
    {
        int direction = Directions.FromStep(to.X - from.X, to.Y - from.Y);
        int block;
        if (!Directions.IsDiagonal(direction))
        {
            block = map.EdgeWall(from, direction)?.ExplosionBlock ?? 0;
        }
        else
        {
            int first = Directions.Normalize(direction - 1), second = Directions.Normalize(direction + 1);
            Position a = from.Step(first), b = from.Step(second);
            int pathA = Edge(map, from, first) + Edge(map, a, second);
            int pathB = Edge(map, from, second) + Edge(map, b, first);
            block = pathA / 2 + pathB / 2 + ((map[a]?.Object?.ExplosionBlock ?? 0) + (map[b]?.Object?.ExplosionBlock ?? 0)) / 2;
        }

        if (!skipStartObject) block += map[from]?.Object?.ExplosionBlock ?? 0;
        return block;
    }

    private static int Edge(BattleMap map, Position from, int orthogonal) =>
        map.InBounds(from.Step(orthogonal)) ? map.EdgeWall(from, orthogonal)?.ExplosionBlock ?? 0 : 0;

    /// <summary>
    /// OXCE detonate: each part of the tile (object, walls, the walls on its east and south edges,
    /// and the floor above) loses layers while the energy covers their armour. Parts with no armour
    /// are indestructible here.
    /// </summary>
    private static void Demolish(Battle battle, Position position, int energy)
    {
        BattleMap map = battle.Map;
        (Tile? Tile, TilePartSlot Slot)[] parts =
        [
            (map[position], TilePartSlot.Object),
            (map[position], TilePartSlot.NorthWall),
            (map[position], TilePartSlot.WestWall),
            (map[position], TilePartSlot.Floor),
            (map[position.X, position.Y + 1, position.Z], TilePartSlot.NorthWall),
            (map[position.X + 1, position.Y, position.Z], TilePartSlot.WestWall),
            (map[position.X, position.Y, position.Z + 1], TilePartSlot.Floor),
        ];
        foreach ((Tile? tile, TilePartSlot slot) in parts)
        {
            if (tile is null) continue;
            int left = energy;
            while (tile[slot] is { Armor: > 0 } part && part.Armor <= left)
            {
                left -= part.Armor;
                DamageRules.DestroyPart(battle, tile, slot);
            }
        }
    }
}
