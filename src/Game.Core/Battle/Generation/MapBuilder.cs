using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Core.Battle.Generation;

/// <summary>
/// Builds battle maps from map blocks: one fixed block, or a grid of blocks picked at random
/// from a terrain (OpenXcom's principle, without its map scripts for now). The same seed always
/// gives the same map.
/// </summary>
public static class MapBuilder
{
    public static BattleMap FromBlock(MapBlockDefinition block, Ruleset rules)
    {
        var map = new BattleMap(block.Width, block.Length, block.Height);
        Stamp(map, block, rules, 0, 0);
        return map;
    }

    /// <summary>
    /// Fills a grid of <paramref name="blocksX"/> × <paramref name="blocksY"/> cells of the
    /// terrain's block size. Required blocks are placed first, then weighted random picks; a
    /// block larger than one cell (20 × 20 for 10 × 10 cells) takes several cells.
    /// </summary>
    public static BattleMap Generate(TerrainDefinition terrain, int blocksX, int blocksY, int levels, Ruleset rules, GameRandom random)
    {
        int size = terrain.BlockSize;
        var map = new BattleMap(blocksX * size, blocksY * size, levels);
        var used = new bool[blocksX, blocksY];
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (TerrainBlockEntry entry in terrain.Blocks)
        {
            for (int i = 0; i < entry.Required; i++)
            {
                MapBlockDefinition block = rules.MapBlocks[entry.Block];
                if (!TryPlaceRandomly(map, block, size, used, rules, random))
                    throw new InvalidOperationException($"Terrain '{terrain.Id}': no room for required block '{block.Id}'.");
                counts[entry.Block] = counts.GetValueOrDefault(entry.Block) + 1;
            }
        }

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (used[bx, by]) continue;
                List<TerrainBlockEntry> candidates = terrain.Blocks
                    .Where(e => e.Weight > 0 && (e.Max == 0 || counts.GetValueOrDefault(e.Block) < e.Max))
                    .Where(e => Fits(rules.MapBlocks[e.Block], size, bx, by, used))
                    .ToList();
                if (candidates.Count == 0)
                    throw new InvalidOperationException($"Terrain '{terrain.Id}': no block fits the cell ({bx}, {by}).");

                TerrainBlockEntry pick = PickWeighted(candidates, random);
                MapBlockDefinition block = rules.MapBlocks[pick.Block];
                Place(map, block, size, bx, by, used, rules);
                counts[pick.Block] = counts.GetValueOrDefault(pick.Block) + 1;
            }
        }

        return map;
    }

    /// <summary>Copies a block's parts and markers into the map at the given tile offset.</summary>
    public static void Stamp(BattleMap map, MapBlockDefinition block, Ruleset rules, int offsetX, int offsetY)
    {
        for (int z = 0; z < block.Levels.Count && z < map.Height; z++)
        {
            IReadOnlyList<string> rows = block.Levels[z];
            for (int y = 0; y < rows.Count; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    string symbol = row[x].ToString();
                    if (!block.Legend.TryGetValue(symbol, out MapCellDefinition? cell)) continue;
                    var position = new Position(offsetX + x, offsetY + y, z);
                    Tile? tile = map[position];
                    if (tile is null) continue;
                    SetPart(tile, TilePartSlot.Floor, cell.Floor, rules);
                    SetPart(tile, TilePartSlot.WestWall, cell.West, rules);
                    SetPart(tile, TilePartSlot.NorthWall, cell.North, rules);
                    SetPart(tile, TilePartSlot.Object, cell.Object, rules);
                    if (cell.Zone is not null) tile.Zone = cell.Zone;
                    if (cell.Patrol) map.PatrolPoints.Add(position);
                    if (cell.Spawn is not null && Enum.TryParse(cell.Spawn, ignoreCase: true, out Faction faction))
                        map.Spawns.Add(new SpawnPoint(position, faction));
                }
            }
        }
    }

    private static void SetPart(Tile tile, TilePartSlot slot, string? partId, Ruleset rules)
    {
        if (partId is null) return;
        tile[slot] = rules.TileParts[partId];
    }

    private static int Cells(int tiles, int cellSize) => Math.Max(1, (tiles + cellSize - 1) / cellSize);

    private static bool Fits(MapBlockDefinition block, int cellSize, int bx, int by, bool[,] used)
    {
        int w = Cells(block.Width, cellSize), l = Cells(block.Length, cellSize);
        if (bx + w > used.GetLength(0) || by + l > used.GetLength(1)) return false;
        for (int y = by; y < by + l; y++)
            for (int x = bx; x < bx + w; x++)
                if (used[x, y]) return false;
        return true;
    }

    private static void Place(BattleMap map, MapBlockDefinition block, int cellSize, int bx, int by, bool[,] used, Ruleset rules)
    {
        int w = Cells(block.Width, cellSize), l = Cells(block.Length, cellSize);
        for (int y = by; y < by + l; y++)
            for (int x = bx; x < bx + w; x++)
                used[x, y] = true;
        Stamp(map, block, rules, bx * cellSize, by * cellSize);
    }

    private static bool TryPlaceRandomly(BattleMap map, MapBlockDefinition block, int cellSize, bool[,] used, Ruleset rules, GameRandom random)
    {
        var free = new List<(int X, int Y)>();
        for (int y = 0; y < used.GetLength(1); y++)
            for (int x = 0; x < used.GetLength(0); x++)
                if (Fits(block, cellSize, x, y, used)) free.Add((x, y));
        if (free.Count == 0) return false;
        (int bx, int by) = random.Pick(free);
        Place(map, block, cellSize, bx, by, used, rules);
        return true;
    }

    private static TerrainBlockEntry PickWeighted(List<TerrainBlockEntry> entries, GameRandom random)
    {
        int total = entries.Sum(e => e.Weight);
        int roll = random.Next(1, total);
        foreach (TerrainBlockEntry entry in entries)
        {
            roll -= entry.Weight;
            if (roll <= 0) return entry;
        }

        return entries[^1];
    }
}
