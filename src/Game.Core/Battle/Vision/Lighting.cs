using Game.Core.Common;

namespace Game.Core.Battle.Vision;

/// <summary>
/// Light level of every tile (0–15), OXCE "classic" lighting: the maximum of the ambient light,
/// terrain light sources, burning tiles and the lights carried by units. A source of power p
/// lights tiles at distance d (in tiles, a level counting 1.5) with p − d; walls do not stop it.
/// Light only matters for seeing units, not terrain.
/// </summary>
public static class Lighting
{
    public const int MaxLight = 15;

    /// <summary>Ambient light under a roof is this much lower in daylight.</summary>
    private const int RoofShadow = 2;

    /// <summary>Light of player units when their armour says nothing (OXCE personalLight).</summary>
    private const int DefaultPlayerLight = 15;

    public static void Compute(Battle battle)
    {
        BattleMap map = battle.Map;
        int ambient = Math.Clamp(map.GlobalLight, 0, MaxLight);
        foreach (Tile tile in map.Tiles)
        {
            Position p = tile.Position;
            bool roofed = false;
            for (int z = p.Z + 1; z < map.Height && !roofed; z++) roofed = map[p.X, p.Y, z]?.Floor is not null;
            int light = roofed && ambient >= MaxLight - 4 ? ambient - RoofShadow : ambient;
            if (tile.Fire > 0) light = MaxLight;
            battle.SetLight(p, light);
        }

        foreach (Tile tile in map.Tiles)
        {
            int power = 0;
            foreach (var part in new[] { tile.Floor, tile.WestWall, tile.NorthWall, tile.Object })
                if (part is not null) power = Math.Max(power, part.Light);
            if (power > 0) Spread(battle, tile.Position, power);
        }

        foreach (BattleUnit unit in battle.Units)
        {
            if (!unit.IsActive) continue;
            int power = unit.Armor.PersonalLight ?? (unit.Faction == Faction.Player ? DefaultPlayerLight : 0);
            if (power > 0) Spread(battle, unit.Position, power);
        }
    }

    private static void Spread(Battle battle, Position source, int power)
    {
        BattleMap map = battle.Map;
        int radius = power - 1;
        for (int z = 0; z < map.Height; z++)
        {
            for (int y = Math.Max(0, source.Y - radius); y <= Math.Min(map.Length - 1, source.Y + radius); y++)
            {
                for (int x = Math.Max(0, source.X - radius); x <= Math.Min(map.Width - 1, source.X + radius); x++)
                {
                    double dz = (z - source.Z) * 1.5;
                    int distance = (int)Math.Round(Math.Sqrt((x - source.X) * (x - source.X) + (y - source.Y) * (y - source.Y) + dz * dz));
                    int light = power - distance;
                    var p = new Position(x, y, z);
                    if (light > battle.LightAt(p)) battle.SetLight(p, light);
                }
            }
        }
    }

    /// <summary>OXCE: a target is in the dark when the shade of its tile (15 − light) is above the threshold.</summary>
    public static bool IsDark(Battle battle, Position p) =>
        MaxLight - battle.LightAt(p) > battle.Rules.Battle.DarknessThreshold;
}
