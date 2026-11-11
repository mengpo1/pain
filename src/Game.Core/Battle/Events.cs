using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// Something that happened in the simulation, in order. The interface reads them to animate,
/// play sounds and log; the simulation never depends on them being read. A plain list, no bus.
/// </summary>
public abstract record BattleEvent;

public sealed record TurnStarted(int Turn, Faction Faction) : BattleEvent;

public sealed record UnitMoved(int UnitId, Position From, Position To) : BattleEvent;

public sealed record UnitTurned(int UnitId, int Direction) : BattleEvent;

public sealed record UnitKneeled(int UnitId, bool Kneeling) : BattleEvent;

public sealed record DoorOpened(Position Tile, TilePartSlot Slot) : BattleEvent;

public sealed record DoorClosed(Position Tile, TilePartSlot Slot) : BattleEvent;

/// <summary>A projectile left the weapon; <see cref="Impact"/> is where it will stop.</summary>
public sealed record ShotFired(int ShooterId, int WeaponId, AttackMode Mode, Voxel Origin, Voxel Impact, bool Reaction) : BattleEvent;

/// <summary>A projectile or blow reached its end point; <see cref="UnitId"/> if it hit a unit.</summary>
public sealed record ProjectileHit(int WeaponId, Voxel Impact, int? UnitId) : BattleEvent;

public sealed record UnitDamaged(int UnitId, int Damage, int StunDamage, ArmorSide Side, BodyPart Part, int Wounds) : BattleEvent;

public sealed record UnitDied(int UnitId, int? KillerId) : BattleEvent;

public sealed record UnitKnockedOut(int UnitId) : BattleEvent;

public sealed record TilePartDestroyed(Position Tile, TilePartSlot Slot, string PartId, string? ReplacedBy) : BattleEvent;

public sealed record ExplosionStarted(Voxel Center, int Radius, string DamageType) : BattleEvent;

/// <summary>A side sees an enemy it did not see a moment ago.</summary>
public sealed record EnemySpotted(Faction Faction, int SpotterId, int UnitId) : BattleEvent;

public sealed record ReactionTriggered(int ReactorId, int TargetId) : BattleEvent;

public sealed record WeaponReloaded(int UnitId, int WeaponId, int AmmoId) : BattleEvent;

/// <summary>A queued action stopped before its end (TU exhausted, path blocked, enemy spotted).</summary>
public sealed record ActionInterrupted(int UnitId, string ReasonKey) : BattleEvent;

public sealed record BattleEnded(BattleOutcome Outcome) : BattleEvent;
