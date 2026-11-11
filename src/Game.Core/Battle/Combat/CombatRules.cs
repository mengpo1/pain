using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Costs and accuracy of attacks, following OXCE BattleUnit::getActionTUs and
/// getFiringAccuracy. All computations are integer, in the order of the original code, because
/// the truncations matter (docs/mechanics.md).
/// </summary>
public static class CombatRules
{
    /// <summary>TU cost of an attack mode; 0 when the weapon has no such mode.</summary>
    public static int AttackTu(BattleUnit unit, ItemDefinition weapon, AttackMode mode)
    {
        AttackModeDefinition? definition = weapon.Mode(mode);
        if (definition is null || definition.Tu <= 0) return 0;
        return weapon.FlatTu ? definition.Tu : Percent(unit.MaxTu, definition.Tu);
    }

    /// <summary>OXCE applyPercentages: max(1, floor(stat × percent / 100)).</summary>
    public static int Percent(int stat, int percent) => Math.Max(1, stat * percent / 100);

    /// <summary>
    /// Accuracy in percent before distance: skill × weapon accuracy, then × kneeling bonus
    /// (ranged modes), × one-handed penalty (two-handed weapon with both hands busy), × health and
    /// wounds modifier. This is what the action menu shows; it is not a chance to hit.
    /// </summary>
    public static int Accuracy(Battle battle, BattleUnit unit, BattleItem weapon, AttackMode mode)
    {
        BattleRules rules = battle.Rules.Battle;
        ItemDefinition definition = weapon.Definition;
        int accuracy;
        if (mode == AttackMode.Melee)
        {
            accuracy = unit.Stats[StatIds.Melee] * (definition.Melee?.Accuracy ?? 0) / 100;
        }
        else if (mode == AttackMode.Throw)
        {
            accuracy = unit.Stats[StatIds.Throwing] * 100 / 100;
        }
        else
        {
            accuracy = unit.Stats[StatIds.Firing] * (definition.Mode(mode)?.Accuracy ?? 0) / 100;
            if (unit.Kneeling) accuracy = accuracy * rules.KneelAccuracyPercent / 100;
        }

        if (mode != AttackMode.Throw && definition.TwoHanded && BothHandsBusy(unit))
            accuracy = accuracy * rules.OneHandedPenaltyPercent / 100;

        return accuracy * HealthModifier(unit, weapon) / 100;
    }

    /// <summary>
    /// OXCE getAccuracyModifier: max(10, 25 × health / max + 75 − 10 × wounds), counting head wounds
    /// and the wounds of the arm(s) holding the weapon.
    /// </summary>
    public static int HealthModifier(BattleUnit unit, BattleItem? weapon)
    {
        int wounds = unit.Wounds(BodyPart.Head);
        if (weapon is not null && weapon.Owner == unit)
        {
            if (weapon.Definition.TwoHanded) wounds += unit.Wounds(BodyPart.RightArm) + unit.Wounds(BodyPart.LeftArm);
            else if (weapon.Slot == Inventory.RightHand) wounds += unit.Wounds(BodyPart.RightArm);
            else if (weapon.Slot == Inventory.LeftHand) wounds += unit.Wounds(BodyPart.LeftArm);
        }

        int health = unit.MaxHealth <= 0 ? 0 : 25 * Math.Max(0, unit.Health) / unit.MaxHealth;
        return Math.Max(10, health + 75 - 10 * wounds);
    }

    public static bool BothHandsBusy(BattleUnit unit) =>
        unit.ItemInSlot(Inventory.RightHand) is not null && unit.ItemInSlot(Inventory.LeftHand) is not null;

    /// <summary>
    /// Accuracy as a fraction after the range drop-off (OXCE applyAccuracy): distance in tiles is the
    /// 3-D voxel distance / 16 (a level counts 1.5 tiles); beyond the upper range (aimed range,
    /// or snap/auto ranges with the extended option) and below the minimum range, the weapon loses
    /// "dropoff" points per tile.
    /// </summary>
    public static double AccuracyAtDistance(BattleRules rules, ItemDefinition weapon, AttackMode mode, double accuracyPercent, double distanceVoxels)
    {
        double accuracy = accuracyPercent / 100.0;
        if (mode is AttackMode.Melee or AttackMode.Throw) return accuracy;
        RangeDefinition range = weapon.Range;
        int upper = range.Aimed;
        if (rules.ExtendedRangeAccuracy)
        {
            if (mode == AttackMode.Auto) upper = range.Auto;
            else if (mode == AttackMode.Snap) upper = range.Snap;
        }

        double tiles = distanceVoxels / Voxel.TileWidth;
        if (tiles < range.Min) accuracy = Math.Max(0, accuracy - range.Dropoff * (range.Min - tiles) / 100.0);
        else if (tiles > upper) accuracy = Math.Max(0, accuracy - range.Dropoff * (tiles - upper) / 100.0);
        return accuracy;
    }

    /// <summary>
    /// Out of range when the squared tile distance (levels counting 1.5 tiles) exceeds the
    /// weapon's maximum range squared.
    /// </summary>
    public static bool InRange(ItemDefinition weapon, Position from, Position to)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, dz = (to.Z - from.Z) * 1.5;
        int max = weapon.Range.Max;
        return dx * dx + dy * dy + dz * dz <= max * max;
    }

    /// <summary>Reaction score (OXCE getReactionScore): reactions × current TU / maximum TU.</summary>
    public static double ReactionScore(BattleUnit unit) =>
        unit.MaxTu <= 0 ? 0 : unit.Stats[StatIds.Reactions] * (double)unit.Tu / unit.MaxTu;
}
