using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// Doors are wall parts with an "open" variant (OpenXcom Alt_MCD). Walking through opens them;
/// swing doors stay open, sliding doors close at the end of the turn when the doorway is free.
/// </summary>
public static class Doors
{
    internal static void Open(Battle battle, Common.Position tilePosition, TilePartSlot slot)
    {
        Tile? tile = battle.Map[tilePosition];
        if (tile?[slot] is not { Door: not DoorKind.None, OpenInto: { } openId } || !battle.Rules.TileParts.TryGetValue(openId, out TilePartDefinition? open))
            return;
        tile[slot] = open;
        battle.Emit(new DoorOpened(tilePosition, slot));
    }

    internal static void CloseSlidingDoors(Battle battle)
    {
        foreach (Tile tile in battle.Map.Tiles)
        {
            foreach (TilePartSlot slot in new[] { TilePartSlot.WestWall, TilePartSlot.NorthWall })
            {
                if (tile[slot] is not { ClosedInto: { } closedId }) continue;
                if (!battle.Rules.TileParts.TryGetValue(closedId, out TilePartDefinition? closed) || closed.Door != DoorKind.Sliding) continue;
                // The doorway is the edge between this tile and its west or north neighbour.
                Common.Position other = slot == TilePartSlot.WestWall ? tile.Position.Offset(-1, 0) : tile.Position.Offset(0, -1);
                if (tile.Unit is not null || battle.UnitAt(other) is not null) continue;
                tile[slot] = closed;
                battle.Emit(new DoorClosed(tile.Position, slot));
            }
        }
    }
}
