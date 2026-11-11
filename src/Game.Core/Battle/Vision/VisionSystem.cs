using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Vision;

/// <summary>
/// Who sees what, following OpenXcom Extended (TileEngine::calculateFOV, see docs/mechanics.md):
/// <list type="bullet">
/// <item>a 90° view sector depending on facing, 20 tiles away at most;</item>
/// <item>terrain is revealed tile by tile along 3-D tile lines stopped by sight-blocking walls,
/// objects and floors; light and smoke do not matter for terrain;</item>
/// <item>units are seen along voxel rays from the eye to the body, shortened by smoke, with a
/// shorter range when the target stands in the dark.</item>
/// </list>
/// </summary>
public static class VisionSystem
{
    /// <summary>Voxels per tile used to turn view ranges into ray lengths (+4 voxels of tolerance).</summary>
    private const int RangeSlack = 4;

    /// <summary>Smoke within the first 32 voxels of the ray (two tiles around the eye) counts half.</summary>
    private const int NearSmokeVoxels = 32;

    /// <summary>Smoke scale of OXCE's visibility formula.</summary>
    private const int SmokeDivisor = 96000;

    /// <summary>
    /// The 90° sector in front of a unit (OXCE checkViewSector): facing a cardinal direction the
    /// cone spans ±45° and excludes the tiles directly beside the unit; facing a diagonal it is the
    /// whole quadrant. The unit's own tile is always inside.
    /// </summary>
    public static bool InViewSector(int direction, Position from, Position to)
    {
        int dx = to.X - from.X, north = from.Y - to.Y;
        if (dx == 0 && north == 0) return true;
        return Directions.Normalize(direction) switch
        {
            0 => north >= Math.Abs(dx),
            1 => dx >= 0 && north >= 0,
            2 => dx >= Math.Abs(north),
            3 => dx >= 0 && north <= 0,
            4 => -north >= Math.Abs(dx),
            5 => dx <= 0 && north <= 0,
            6 => -dx >= Math.Abs(north),
            _ => dx <= 0 && north >= 0,
        };
    }

    /// <summary>Recomputes everything: light, revealed terrain, units seen by every unit.</summary>
    public static void Refresh(Battle battle)
    {
        Lighting.Compute(battle);
        battle.ClearCurrentlyVisibleTiles();
        foreach (BattleUnit unit in battle.Units)
        {
            if (!unit.IsActive) continue;
            if (unit.Faction == Faction.Player) RevealTerrain(battle, unit);
        }

        foreach (BattleUnit observer in battle.Units) UpdateUnitsSeenBy(battle, observer);
    }

    /// <summary>Reveals the tiles a player unit sees (sets "discovered" and "currently visible").</summary>
    public static void RevealTerrain(Battle battle, BattleUnit unit)
    {
        BattleMap map = battle.Map;
        int range = battle.Rules.Battle.MaxViewDistance;
        Position origin = unit.Position;
        battle.MarkVisible(unit.Faction, origin);
        for (int y = Math.Max(0, origin.Y - range); y <= Math.Min(map.Length - 1, origin.Y + range); y++)
        {
            for (int x = Math.Max(0, origin.X - range); x <= Math.Min(map.Width - 1, origin.X + range); x++)
            {
                var column = new Position(x, y, origin.Z);
                if (origin.HorizontalDistanceSquared(column) > range * range) continue;
                if (!InViewSector(unit.Direction, origin, column)) continue;
                for (int z = 0; z < map.Height; z++) TraceTiles(battle, unit.Faction, origin, new Position(x, y, z));
            }
        }
    }

    /// <summary>Walks a tile line, revealing tiles until terrain blocks the view.</summary>
    private static void TraceTiles(Battle battle, Faction faction, Position from, Position to)
    {
        Position previous = from;
        LineTracer.Bresenham(new Voxel(from.X, from.Y, from.Z), new Voxel(to.X, to.Y, to.Z), point =>
        {
            var current = new Position(point.X, point.Y, point.Z);
            if (current == from) return true;
            SightBlock block = Blockage(battle.Map, previous, current);
            if (block == SightBlock.Blocked) return false;
            battle.MarkVisible(faction, current);
            previous = current;
            return block == SightBlock.Open;
        }, superCover: false);
    }

    private enum SightBlock
    {
        Open,

        /// <summary>The next tile is seen (a solid object, a wall face) but nothing beyond it.</summary>
        RevealOnly,

        Blocked,
    }

    /// <summary>OXCE horizontal/vertical blockage between two neighbouring tiles, for sight.</summary>
    private static SightBlock Blockage(BattleMap map, Position a, Position b)
    {
        if (!map.InBounds(b)) return SightBlock.Blocked;
        Position column = a;
        if (b.Z != a.Z)
        {
            // Every real floor blocks vertical sight.
            Tile? floorTile = b.Z < a.Z ? map[a] : map[a.X, a.Y, a.Z + 1];
            if (floorTile?.Floor is not null) return SightBlock.Blocked;
            if (b.Z < a.Z && map[a]?.Object is { BlocksSight: true }) return SightBlock.Blocked;
            column = new Position(a.X, a.Y, b.Z);
        }

        if (column.X != b.X || column.Y != b.Y)
        {
            int direction = Directions.FromStep(b.X - column.X, b.Y - column.Y);
            if (!HorizontalOpen(map, column, direction)) return SightBlock.Blocked;
        }

        return map[b]?.Object is { BlocksSight: true } ? SightBlock.RevealOnly : SightBlock.Open;
    }

    /// <summary>
    /// Sight across tile edges. Diagonals use OXCE's "two ways" rule: open if either L-shaped
    /// path around the corner (two edges and the corner tile's object) is clear.
    /// </summary>
    private static bool HorizontalOpen(BattleMap map, Position from, int direction)
    {
        if (!Directions.IsDiagonal(direction)) return !EdgeBlocksSight(map, from, direction);
        int first = Directions.Normalize(direction - 1), second = Directions.Normalize(direction + 1);
        return LOpen(map, from, first, second) || LOpen(map, from, second, first);
    }

    private static bool LOpen(BattleMap map, Position from, int firstLeg, int secondLeg)
    {
        Position corner = from.Step(firstLeg);
        Tile? cornerTile = map[corner];
        if (cornerTile is null) return false;
        return !EdgeBlocksSight(map, from, firstLeg) && !EdgeBlocksSight(map, corner, secondLeg) && cornerTile.Object is not { BlocksSight: true };
    }

    private static bool EdgeBlocksSight(BattleMap map, Position from, int orthogonal) =>
        map.InBounds(from.Step(orthogonal)) ? map.EdgeWall(from, orthogonal) is { BlocksSight: true } : true;

    /// <summary>Updates the enemies an observer sees now, and the side's knowledge of them.</summary>
    public static void UpdateUnitsSeenBy(Battle battle, BattleUnit observer)
    {
        observer.VisibleUnits.Clear();
        if (!observer.IsActive) return;
        foreach (BattleUnit target in battle.Units)
        {
            if (target == observer || target.Status == UnitStatus.Dead || !Factions.AreEnemies(observer.Faction, target.Faction)) continue;
            if (!InViewSector(observer.Direction, observer.Position, target.Position)) continue;
            if (!CanSee(battle, observer, target)) continue;
            observer.VisibleUnits.Add(target);
            battle.RecordSighting(observer, target);
        }
    }

    /// <summary>
    /// OXCE TileEngine::visible without the sector test: distance, a voxel ray from the eye to the
    /// body, then Q = Vmax − length − smoke cost &gt; 0, where Vmax is shorter when the target
    /// stands in the dark.
    /// </summary>
    public static bool CanSee(Battle battle, BattleUnit observer, BattleUnit target)
    {
        BattleRules rules = battle.Rules.Battle;
        if (observer.Position.HorizontalDistanceSquared(target.Position) > rules.MaxViewDistance * rules.MaxViewDistance) return false;

        Voxel eye = EyeVoxel(battle, observer);
        int range = Lighting.IsDark(battle, target.Position) ? DarkRange(battle, observer) : rules.MaxViewDistance;
        int maxVoxels = Math.Min(rules.MaxViewDistance, range) * Voxel.TileWidth + RangeSlack;

        foreach (Voxel point in TargetPoints(battle, observer, eye, target))
        {
            double length = 0;
            double smoke = 0, nearSmoke = 0;
            Voxel previous = eye;
            TraceHit hit = LineTracer.TraceVoxels(battle, eye, point, observer, sightOnly: true, voxel =>
            {
                double step = Math.Sqrt((voxel.X - previous.X) * (voxel.X - previous.X) + (voxel.Y - previous.Y) * (voxel.Y - previous.Y) + (voxel.Z - previous.Z) * (voxel.Z - previous.Z));
                previous = voxel;
                length += step;
                int density = battle.Map[voxel.Tile]?.Smoke ?? 0;
                smoke += step * density;
                if (length <= NearSmokeVoxels) nearSmoke += step * density;
            });
            if (hit.Unit != target) continue;
            double quality = maxVoxels - eye.DistanceTo(hit.Voxel) - (smoke - nearSmoke / 2) * 100 * maxVoxels / SmokeDivisor;
            if (quality > 0) return true;
        }

        return false;
    }

    /// <summary>Dark-vision range: the armour's value, else 9 tiles for the player and neutrals, 20 for hostiles.</summary>
    public static int DarkRange(Battle battle, BattleUnit observer)
    {
        if (observer.Armor.VisibilityAtDark > 0) return observer.Armor.VisibilityAtDark;
        return observer.Faction == Faction.Hostile ? battle.Rules.Battle.MaxViewDistance : battle.Rules.Battle.DarkViewDistance;
    }

    /// <summary>Eye position: tile centre, feet height plus the unit's eye height, kept under the ceiling.</summary>
    public static Voxel EyeVoxel(Battle battle, BattleUnit unit)
    {
        Position p = unit.Position;
        int z = LineTracer.UnitBottom(battle, unit) + unit.EyeHeight;
        int ceiling = (p.Z + 1) * Voxel.TileHeight - 1;
        if (z > ceiling && battle.Map[p.X, p.Y, p.Z + 1]?.Floor is not null) z = ceiling - 1;
        return new Voxel(p.X * Voxel.TileWidth + Voxel.TileWidth / 2, p.Y * Voxel.TileWidth + Voxel.TileWidth / 2, z);
    }

    /// <summary>
    /// Points of the target's body tried by sight and aiming (simplified OXCE canTargetUnit): the
    /// body axis at a few heights from the middle outwards, then the same shifted sideways.
    /// </summary>
    public static IEnumerable<Voxel> TargetPoints(Battle battle, BattleUnit observer, Voxel origin, BattleUnit target)
    {
        int bottom = LineTracer.UnitBottom(battle, target);
        int height = target.IsActive ? target.Height : LineTracer.LyingHeight;
        int middle = bottom + Math.Max(1, height / 2);
        var center = new Voxel(target.Position.X * Voxel.TileWidth + Voxel.TileWidth / 2, target.Position.Y * Voxel.TileWidth + Voxel.TileWidth / 2, middle);

        // Unit vector across the line of sight, in the horizontal plane.
        double vx = center.X - origin.X, vy = center.Y - origin.Y;
        double norm = Math.Sqrt(vx * vx + vy * vy);
        int px = norm > 0 ? (int)Math.Round(-vy / norm * (LineTracer.UnitRadius - 1)) : 0;
        int py = norm > 0 ? (int)Math.Round(vx / norm * (LineTracer.UnitRadius - 1)) : 0;

        int[] offsets = [0, -2, 2, -4, 4, -6, 6, -8, 8];
        int count = Math.Clamp(height / 2, 1, offsets.Length);
        for (int i = 0; i < count; i++)
        {
            int z = middle + offsets[i];
            if (z <= bottom || z > bottom + height) continue;
            yield return center with { Z = z };
            yield return new Voxel(center.X + px, center.Y + py, z);
            yield return new Voxel(center.X - px, center.Y - py, z);
        }
    }
}
