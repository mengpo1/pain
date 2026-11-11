using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// An intention of the player or of the AI. The battle validates it against the rules; a click
/// in the interface never changes the state directly.
/// </summary>
public abstract record BattleCommand(int UnitId);

/// <summary>Walk to a tile along the cheapest path (stops early if TU run out or an enemy appears).</summary>
public sealed record MoveCommand(int UnitId, Position Destination) : BattleCommand(UnitId);

/// <summary>Turn to face a direction (1 TU per eighth of a turn).</summary>
public sealed record TurnCommand(int UnitId, int Direction) : BattleCommand(UnitId);

/// <summary>Kneel down, or stand up when already kneeling.</summary>
public sealed record KneelCommand(int UnitId) : BattleCommand(UnitId);

/// <summary>Attack a tile with a weapon held in hand, in a given mode (snap, aimed, auto, melee).</summary>
public sealed record AttackCommand(int UnitId, int WeaponId, AttackMode Mode, Position Target) : BattleCommand(UnitId);

/// <summary>Load a clip carried by the unit into a weapon held in hand.</summary>
public sealed record ReloadCommand(int UnitId, int WeaponId) : BattleCommand(UnitId);

/// <summary>Ends the current faction's turn (UnitId is ignored).</summary>
public sealed record EndTurnCommand() : BattleCommand(0);

/// <summary>
/// Answer to a command: accepted, or refused with a localization key explaining why
/// (e.g. <c>error.not_enough_tu</c>) and optional arguments for that text.
/// </summary>
public sealed record CommandResult(bool Accepted, string? ReasonKey = null, int? Value = null)
{
    public static readonly CommandResult Ok = new(true);

    public static CommandResult Refused(string reasonKey, int? value = null) => new(false, reasonKey, value);
}
