using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Where shots start, what they aim at and where they land. There is no "chance to hit" roll for
/// firearms (OXCE Projectile::applyAccuracy): the aimed voxel is moved by a random deviation that
/// shrinks with accuracy, the line is extended beyond it, and the first solid voxel met is the
/// impact — a miss can still hit someone else, a good shot can still clip a wall.
/// </summary>
public static class Ballistics
{
    // OXCE getOriginVoxel: where the muzzle is inside the tile, by firing direction.
    private static readonly int[] OriginX = [8, 14, 15, 15, 8, 1, 1, 1];
    private static readonly int[] OriginY = [1, 1, 8, 15, 15, 15, 8, 1];

    /// <summary>Muzzle position: towards the target's side of the tile, 4 voxels below the head.</summary>
    public static Voxel Origin(Battle battle, BattleUnit shooter, Position target, bool thrown = false)
    {
        Position p = shooter.Position;
        int direction = p == target ? shooter.Direction : Directions.Towards(p, target);
        int z = LineTracer.UnitBottom(battle, shooter) + shooter.Height - (thrown ? 3 : 4);
        int ceiling = (p.Z + 1) * Voxel.TileHeight - 1;
        if (z > ceiling && battle.Map[p.X, p.Y, p.Z + 1]?.Floor is not null) z = ceiling - 4;
        return new Voxel(p.X * Voxel.TileWidth + OriginX[direction], p.Y * Voxel.TileWidth + OriginY[direction], z);
    }

    /// <summary>
    /// Voxel aimed at on a tile: a visible point of the unit standing there, else the object, the
    /// north wall, the west wall or the floor, else the middle of the tile (OXCE canTargetUnit /
    /// canTargetTile, simplified sampling). Null when a unit is there but no point of it can be hit.
    /// </summary>
    public static Voxel? AimPoint(Battle battle, BattleUnit shooter, Voxel origin, Position target)
    {
        BattleUnit? unit = battle.UnitAt(target);
        if (unit is not null && unit != shooter && battle.IsSpottedBy(shooter.Faction, unit))
        {
            foreach (Voxel point in VisionSystem.TargetPoints(battle, shooter, origin, unit))
                if (LineTracer.TraceVoxels(battle, origin, point, shooter, sightOnly: false).Unit == unit) return point;
            return null;
        }

        Tile? tile = battle.Map[target];
        int bx = target.X * Voxel.TileWidth, by = target.Y * Voxel.TileWidth, bz = target.Z * Voxel.TileHeight;
        if (tile?.Object is not null) return new Voxel(bx + 8, by + 8, bz + 10);
        if (tile?.NorthWall is not null) return new Voxel(bx + 8, by + 0, bz + 9);
        if (tile?.WestWall is not null) return new Voxel(bx + 0, by + 8, bz + 9);
        if (tile?.Floor is not null) return new Voxel(bx + 8, by + 8, bz + 2);
        return new Voxel(bx + 8, by + 8, bz + 12);
    }

    /// <summary>
    /// OXCE applyAccuracy: deviates the aimed voxel. The spread grows with distance and is
    /// deliberately anisotropic (half as wide along X as along Y), as in the original.
    /// </summary>
    public static Voxel Deviate(Voxel origin, Voxel target, double accuracy, GameRandom random)
    {
        int dx = Math.Abs(origin.X - target.X), dy = Math.Abs(origin.Y - target.Y), dz = Math.Abs(origin.Z - target.Z);
        int xyShift = dx / 2 <= dy ? dx / 4 + dy : (dx + dy) / 2;
        int zShift = xyShift <= dz ? xyShift / 2 + dz : xyShift + dz / 2;

        int deviation = (int)(random.Next(0, 100) - accuracy * 100);
        deviation += deviation >= 0 ? 50 : 10;
        deviation = Math.Max(1, zShift * deviation / 200);

        int x = target.X + random.Next(0, deviation) - deviation / 2;
        int y = target.Y + random.Next(0, deviation) - deviation / 2;
        int z = target.Z + random.Next(0, deviation / 2) / 2 - deviation / 8;
        return new Voxel(x, y, z);
    }

    /// <summary>Extends the line origin → target to the projectile's maximum flight and traces it.</summary>
    public static TraceHit Fly(Battle battle, BattleUnit shooter, Voxel origin, Voxel target)
    {
        double vx = target.X - origin.X, vy = target.Y - origin.Y, vz = target.Z - origin.Z;
        double length = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        if (length < 0.5) return LineTracer.TraceVoxels(battle, origin, target, shooter, sightOnly: false);
        double scale = battle.Rules.Battle.MaxProjectileVoxels / length;
        var end = new Voxel(origin.X + (int)(vx * scale), origin.Y + (int)(vy * scale), origin.Z + (int)(vz * scale));
        return LineTracer.TraceVoxels(battle, origin, ClampToMapBox(battle, origin, end), shooter, sightOnly: false);
    }

    /// <summary>
    /// Shortens a far end point to the edge of the map volume (plus a margin) so traces stay short;
    /// the direction is unchanged.
    /// </summary>
    private static Voxel ClampToMapBox(Battle battle, Voxel origin, Voxel end)
    {
        double maxX = (battle.Map.Width + 1) * Voxel.TileWidth, maxY = (battle.Map.Length + 1) * Voxel.TileWidth, maxZ = (battle.Map.Height + 1) * Voxel.TileHeight;
        double t = 1.0;
        void Limit(double start, double delta, double min, double max)
        {
            if (delta > 0 && start + delta * t > max) t = Math.Min(t, (max - start) / delta);
            if (delta < 0 && start + delta * t < min) t = Math.Min(t, (min - start) / delta);
        }

        double dx = end.X - origin.X, dy = end.Y - origin.Y, dz = end.Z - origin.Z;
        Limit(origin.X, dx, -Voxel.TileWidth, maxX);
        Limit(origin.Y, dy, -Voxel.TileWidth, maxY);
        Limit(origin.Z, dz, -Voxel.TileHeight, maxZ);
        t = Math.Max(t, 0);
        return new Voxel(origin.X + (int)(dx * t), origin.Y + (int)(dy * t), origin.Z + (int)(dz * t));
    }

    /// <summary>
    /// The player's pre-check before the first shot (OXCE): the straight line to the aimed point
    /// must reach the target first. Refused shots cost nothing.
    /// </summary>
    public static bool HasLineOfFire(Battle battle, BattleUnit shooter, Voxel origin, Voxel aim, Position target)
    {
        TraceHit hit = LineTracer.TraceVoxels(battle, origin, aim, shooter, sightOnly: false);
        if (!hit.IsBlocked) return true;
        if (hit.Unit is { } unit) return unit.Position == target;
        return hit.Tile == target;
    }
}
