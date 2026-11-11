namespace Game.Core.Definitions;

/// <summary>
/// Global tactical constants (game.json, section "battle"). Defaults are OpenXcom Extended's
/// defaults as read in its sources; see docs/mechanics.md for each value's origin.
/// </summary>
public sealed class BattleRules
{
    // ---- Movement (Pathfinding::getTUCost, UnitWalkBState) ----

    /// <summary>A step whose summed costs are 0 costs this much instead (OXCE).</summary>
    public int MinStepTu { get; init; } = 4;

    /// <summary>Diagonal steps cost this percentage of the orthogonal cost (truncated).</summary>
    public int DiagonalCostPercent { get; init; } = 150;

    /// <summary>Energy spent per step, as a percentage of its TU cost (truncated).</summary>
    public int StepEnergyPercent { get; init; } = 50;

    /// <summary>A step may rise at most this many voxels (stairs are built from 8-voxel steps).</summary>
    public int MaxStepUp { get; init; } = 8;

    /// <summary>From a surface at least this high, a unit can step onto the floor of the level above.</summary>
    public int ClimbHeight { get; init; } = 16;

    /// <summary>A unit walking onto a tile without floor goes down a level if the surface below is at least this high.</summary>
    public int DescendHeight { get; init; } = 12;

    /// <summary>Extra TU for climbing to the level above.</summary>
    public int ClimbTu { get; init; } = 1;

    /// <summary>TU per eighth of a turn when turning on the spot (turning while walking is free).</summary>
    public int TurnTu { get; init; } = 1;

    public int KneelTu { get; init; } = 4;
    public int StandUpTu { get; init; } = 8;

    // ---- Attacks (BattleUnit::getFiringAccuracy, Projectile::applyAccuracy) ----

    /// <summary>Accuracy percentage applied when kneeling (snap, aimed, auto).</summary>
    public int KneelAccuracyPercent { get; init; } = 115;

    /// <summary>Accuracy percentage of a two-handed weapon when both hands hold something.</summary>
    public int OneHandedPenaltyPercent { get; init; } = 80;

    /// <summary>Throwing and priming cost these percentages of the unit's maximum TU.</summary>
    public int ThrowTuPercent { get; init; } = 25;
    public int PrimeTuPercent { get; init; } = 50;

    /// <summary>
    /// When true, snap and auto shots lose accuracy beyond the weapon's snap/auto ranges (OXCE
    /// option "battleUFOExtenderAccuracy", off by default); otherwise only the aimed range counts.
    /// </summary>
    public bool ExtendedRangeAccuracy { get; init; }

    /// <summary>Projectiles fly until they hit something, up to this many voxels.</summary>
    public int MaxProjectileVoxels { get; init; } = 16000;

    // ---- Damage (RuleDamageType, difficulty constants) ----

    /// <summary>Standard damage spread: power ± this percentage (UFO 100 → 0–200 %, TFTD 50).</summary>
    public int DamageRangePercent { get; init; } = 100;

    /// <summary>Explosion damage spread (±50 % → 50–150 %).</summary>
    public int ExplosiveDamageRangePercent { get; init; } = 50;

    /// <summary>Fire damage range, whatever the power.</summary>
    public int FireDamageMin { get; init; } = 5;
    public int FireDamageMax { get; init; } = 10;

    /// <summary>Largest explosion radius in tiles.</summary>
    public int MaxExplosionRadius { get; init; } = 11;

    /// <summary>Armour of items lying on the ground against explosions.</summary>
    public int ItemArmor { get; init; } = 20;

    // ---- Morale (BattleUnit::moraleChange, BattlescapeGame::checkForCasualties) ----

    /// <summary>Morale lost by allies of a unit that dies, before bravery (OXCE moraleLossWhenKilled).</summary>
    public int MoraleLossWhenKilled { get; init; } = 100;

    /// <summary>Morale gained by the other sides when a unit dies.</summary>
    public int MoraleGainOnEnemyDeath { get; init; } = 10;

    /// <summary>Morale gained by the killer of an enemy.</summary>
    public int MoraleGainForKiller { get; init; } = 20;

    /// <summary>Morale regained when a panic episode ends.</summary>
    public int MoraleAfterPanic { get; init; } = 15;

    // ---- Reaction fire (TileEngine::checkReactionFire) ----

    /// <summary>A reacting unit fires at most this many times at one triggering action.</summary>
    public int MaxReactionsPerTrigger { get; init; } = 10;

    // ---- Vision (TileEngine::calculateFOV) ----

    /// <summary>Maximum sight distance in tiles.</summary>
    public int MaxViewDistance { get; init; } = 20;

    /// <summary>Units standing in darkness are only seen within this many tiles.</summary>
    public int DarkViewDistance { get; init; } = 9;

    /// <summary>Tiles lit below this level count as dark.</summary>
    public int DarknessThreshold { get; init; } = 9;

    // ---- Turn upkeep (BattleUnit::prepareNewTurn) ----

    /// <summary>Energy recovered per turn by units without their own value.</summary>
    public int EnergyRecovery { get; init; } = 30;

    /// <summary>Stun points recovered per turn.</summary>
    public int StunRecovery { get; init; } = 1;

    /// <summary>Health lost per fatal wound at the start of each turn.</summary>
    public int BleedPerWound { get; init; } = 1;

    /// <summary>TU lost per leg wound, in percent of the turn's TU.</summary>
    public int LegWoundTuPercent { get; init; } = 10;
}
