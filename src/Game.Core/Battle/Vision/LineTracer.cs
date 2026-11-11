using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Vision;

/// <summary>What a voxel ray stopped on.</summary>
public enum HitKind
{
    Nothing,
    Floor,
    WestWall,
    NorthWall,
    Object,
    Unit,
    OutOfMap,
}

/// <summary>Result of a voxel trace: the first solid voxel met, and what it belongs to.</summary>
public readonly record struct TraceHit(HitKind Kind, Voxel Voxel, Position Tile, BattleUnit? Unit)
{
    public bool IsBlocked => Kind != HitKind.Nothing;
}

/// <summary>
/// 3-D Bresenham walks through voxels and tiles (OpenXcom calculateLine). The voxel walk also
/// tests the intermediate voxels when the minor axes step, so a ray never slips diagonally
/// through a thin wall.
/// </summary>
public static class LineTracer
{
    /// <summary>
    /// Follows the voxel line from <paramref name="from"/> to <paramref name="to"/> and returns the
    /// first solid voxel (terrain shape or unit body), skipping the voxels of <paramref name="ignore"/>.
    /// With <paramref name="sightOnly"/>, terrain parts that do not block sight are transparent.
    /// <paramref name="onVoxel"/> is called for every free voxel visited (smoke accounting).
    /// </summary>
    public static TraceHit TraceVoxels(Battle battle, Voxel from, Voxel to, BattleUnit? ignore, bool sightOnly, Action<Voxel>? onVoxel = null)
    {
        TraceHit result = new(HitKind.Nothing, to, to.Tile, null);
        Bresenham(from, to, voxel =>
        {
            if (voxel == from) return true;
            TraceHit hit = Check(battle, voxel, ignore, sightOnly);
            if (hit.IsBlocked)
            {
                result = hit;
                return false;
            }

            onVoxel?.Invoke(voxel);
            return true;
        }, superCover: true);
        return result;
    }

    /// <summary>What occupies one voxel.</summary>
    public static TraceHit Check(Battle battle, Voxel voxel, BattleUnit? ignore, bool sightOnly)
    {
        Position tilePosition = voxel.Tile;
        BattleMap map = battle.Map;
        if (!map.InBounds(tilePosition)) return new TraceHit(HitKind.OutOfMap, voxel, tilePosition, null);
        Tile tile = map[tilePosition]!;
        int lx = voxel.X - tilePosition.X * Voxel.TileWidth;
        int ly = voxel.Y - tilePosition.Y * Voxel.TileWidth;
        int lz = voxel.Z - tilePosition.Z * Voxel.TileHeight;

        if (Solid(battle, tile.Floor, lx, ly, lz, sightOnly)) return new TraceHit(HitKind.Floor, voxel, tilePosition, null);
        if (Solid(battle, tile.WestWall, lx, ly, lz, sightOnly)) return new TraceHit(HitKind.WestWall, voxel, tilePosition, null);
        if (Solid(battle, tile.NorthWall, lx, ly, lz, sightOnly)) return new TraceHit(HitKind.NorthWall, voxel, tilePosition, null);
        if (Solid(battle, tile.Object, lx, ly, lz, sightOnly)) return new TraceHit(HitKind.Object, voxel, tilePosition, null);

        BattleUnit? unit = UnitAtVoxel(battle, tile, voxel, lx, ly);
        if (unit is null && tilePosition.Z > 0)
        {
            // A unit standing on raised ground below can reach into this level.
            Tile? below = map[tilePosition.X, tilePosition.Y, tilePosition.Z - 1];
            if (below is not null) unit = UnitAtVoxel(battle, below, voxel, lx, ly);
        }

        if (unit is not null && unit != ignore) return new TraceHit(HitKind.Unit, voxel, unit.Position, unit);
        return new TraceHit(HitKind.Nothing, voxel, tilePosition, null);
    }

    private static bool Solid(Battle battle, TilePartDefinition? part, int x, int y, int z, bool sightOnly)
    {
        if (part is null || part.Shape.Length == 0) return false;
        if (sightOnly && !part.BlocksSight) return false;
        return battle.Rules.Shapes.TryGetValue(part.Shape, out ShapeDefinition? shape) && shape.Contains(x, y, z);
    }

    /// <summary>
    /// Units are vertical cylinders: radius <see cref="UnitRadius"/> voxels around the tile centre,
    /// from their feet (raised by the surface they stand on) to the top of their head.
    /// </summary>
    public const int UnitRadius = 4;

    private static BattleUnit? UnitAtVoxel(Battle battle, Tile tile, Voxel voxel, int lx, int ly)
    {
        BattleUnit? unit = tile.Unit;
        if (unit is null || unit.Status == UnitStatus.Dead) return null;
        int bottom = UnitBottom(battle, unit);
        int top = bottom + (unit.IsActive ? unit.Height : LyingHeight);
        if (voxel.Z <= bottom || voxel.Z > top) return null;
        int dx = lx - Voxel.TileWidth / 2, dy = ly - Voxel.TileWidth / 2;
        return dx * dx + dy * dy <= UnitRadius * UnitRadius ? unit : null;
    }

    /// <summary>Height of a unit lying on the floor (unconscious).</summary>
    public const int LyingHeight = 4;

    /// <summary>Voxel height of a unit's feet.</summary>
    public static int UnitBottom(Battle battle, BattleUnit unit) =>
        unit.Position.Z * Voxel.TileHeight + battle.Map.StandElevation(unit.Position);

    /// <summary>
    /// Bresenham over integer 3-D points. With <paramref name="superCover"/>, when a minor axis
    /// steps, the intermediate point is visited too (OpenXcom's voxel check does the same).
    /// The visitor returns false to stop.
    /// </summary>
    public static void Bresenham(Voxel from, Voxel to, Func<Voxel, bool> visit, bool superCover)
    {
        int x0 = from.X, y0 = from.Y, z0 = from.Z;
        int dx = Math.Abs(to.X - x0), dy = Math.Abs(to.Y - y0), dz = Math.Abs(to.Z - z0);
        int sx = Math.Sign(to.X - x0), sy = Math.Sign(to.Y - y0), sz = Math.Sign(to.Z - z0);
        int steps = Math.Max(dx, Math.Max(dy, dz));
        if (!visit(from)) return;
        if (steps == 0) return;

        // Errors of the two minor axes against the dominant one, starting at half its length.
        int x = x0, y = y0, z = z0;
        if (dx >= dy && dx >= dz)
        {
            int ey = dx / 2, ez = dx / 2;
            for (int i = 0; i < steps; i++)
            {
                x += sx;
                ey -= dy; ez -= dz;
                if (ey < 0) { if (superCover && !visit(new Voxel(x, y, z))) return; y += sy; ey += dx; }
                if (ez < 0) { if (superCover && !visit(new Voxel(x, y, z))) return; z += sz; ez += dx; }
                if (!visit(new Voxel(x, y, z))) return;
            }
        }
        else if (dy >= dx && dy >= dz)
        {
            int ex = dy / 2, ez = dy / 2;
            for (int i = 0; i < steps; i++)
            {
                y += sy;
                ex -= dx; ez -= dz;
                if (ex < 0) { if (superCover && !visit(new Voxel(x, y, z))) return; x += sx; ex += dy; }
                if (ez < 0) { if (superCover && !visit(new Voxel(x, y, z))) return; z += sz; ez += dy; }
                if (!visit(new Voxel(x, y, z))) return;
            }
        }
        else
        {
            int ex = dz / 2, ey = dz / 2;
            for (int i = 0; i < steps; i++)
            {
                z += sz;
                ex -= dx; ey -= dy;
                if (ex < 0) { if (superCover && !visit(new Voxel(x, y, z))) return; x += sx; ex += dz; }
                if (ey < 0) { if (superCover && !visit(new Voxel(x, y, z))) return; y += sy; ey += dz; }
                if (!visit(new Voxel(x, y, z))) return;
            }
        }
    }
}
