using Game.Core.Battle;
using Game.Core.Battle.Movement;
using Game.Core.Common;
using Game.Core.Definitions;
using Game.Desktop.Graphics;

namespace Game.Desktop.Tactical;

/// <summary>What the player is looking at and pointing to: presentation state only.</summary>
public sealed class BattleView
{
    public IsoCamera Camera { get; } = new();

    /// <summary>Highest level drawn (levels above are hidden, as in OpenXcom).</summary>
    public int Level { get; set; }

    public BattleUnit? Selected { get; set; }

    /// <summary>Tile under the mouse at the current level, if inside the map.</summary>
    public Position? Hovered { get; set; }

    /// <summary>Path preview of the selected unit towards the hovered tile.</summary>
    public PathResult? Path { get; set; }

    /// <summary>Weapon and mode chosen in the action menu; the next click picks the target.</summary>
    public (BattleItem Weapon, AttackMode Mode)? Targeting { get; set; }

    /// <summary>Hand whose action menu is open, if any.</summary>
    public BattleItem? ActionMenu { get; set; }

    /// <summary>The inventory panel of the selected unit is open.</summary>
    public bool InventoryOpen { get; set; }

    /// <summary>Shows every unit and tile (debug, F1).</summary>
    public bool RevealAll { get; set; }
}
