using System.Numerics;
using TopDown.Core.Definitions;

namespace TopDown.Core.World;

/// <summary>
/// Something that happened during a simulation step. The presentation layer reads these
/// to play sounds, spawn particles and shake the camera; the simulation never depends on them.
/// </summary>
public abstract record GameEvent;

public enum HitMethod
{
    Melee,
    Firearm,
    Thrown,
    Door,
    Execution,
}

public enum SurfaceKind
{
    Wall,
    Door,
    Prop,
    Actor,
}

public sealed record ShotFired(Actor Shooter, WeaponDefinition Weapon, Vector2 Muzzle, float Angle) : GameEvent;

/// <summary>Visual trace of an instant (hitscan) bullet.</summary>
public sealed record BulletTrace(Vector2 From, Vector2 To) : GameEvent;

public sealed record BulletImpact(Vector2 Position, Vector2 Normal, SurfaceKind Surface) : GameEvent;

public sealed record MeleeSwing(Actor Attacker, WeaponDefinition Weapon) : GameEvent;

public sealed record WeaponEmpty(Actor Owner, WeaponDefinition Weapon) : GameEvent;

public sealed record ActorHit(Actor Target, Actor? Source, Vector2 Direction, HitMethod Method, string DamageType, float Damage, WeaponDefinition? Weapon) : GameEvent;

public sealed record ActorKnockedDown(Actor Target, Actor? Source, HitMethod Method) : GameEvent;

/// <summary>A hit that would have killed left the target wounded, crawling on the floor.</summary>
public sealed record ActorWounded(Actor Target, Actor? Source, Vector2 Direction, HitMethod Method) : GameEvent;

/// <param name="Severed">Indices (in the victim's sprite parts) of the body parts the blow cut off.</param>
public sealed record ActorKilled(Actor Victim, Actor? Killer, Vector2 Direction, HitMethod Method, string DamageType,
    WeaponDefinition? Weapon = null, IReadOnlyList<int>? Severed = null) : GameEvent;

public sealed record WeaponPickedUp(Actor Actor, WeaponDefinition Weapon) : GameEvent;

public sealed record WeaponThrown(Actor Actor, WeaponDefinition Weapon) : GameEvent;

public sealed record ThrownWeaponBounced(Vector2 Position, WeaponDefinition Weapon) : GameEvent;

public sealed record DoorHit(Door Door, float Strength) : GameEvent;

public sealed record DoorPushed(Door Door) : GameEvent;

public sealed record TileBroken(int X, int Y, TileDefinition Tile, Vector2 Position) : GameEvent;

public sealed record NoiseMade(Vector2 Position, float Radius) : GameEvent;

public sealed record EnemyAlerted(Actor Enemy) : GameEvent;

public sealed record ExecutionStarted(Actor Executioner, Actor Victim, ExecutionAnimation Animation) : GameEvent;

/// <summary>One blow (or the shot) of an execution. <paramref name="Final"/> is the killing blow.</summary>
public sealed record ExecutionStrike(Actor Executioner, Actor Victim, WeaponDefinition Weapon, int Index, bool Final, bool Shot, ExecutionAnimation Animation) : GameEvent;

public sealed record ItemCollected(Actor Actor, ItemPickup Item) : GameEvent;

public sealed record Activated(Interactable Interactable) : GameEvent;

public sealed record ObjectiveCompleted(int Index, ObjectiveDefinition Objective) : GameEvent;

public sealed record LevelCompleted : GameEvent;

public sealed record PlayerDied(Actor Player) : GameEvent;

/// <summary>A level script asks to show a message.</summary>
public sealed record MessageRequested(string TextKey, float Duration) : GameEvent;

/// <summary>A level script asks to play a sound.</summary>
public sealed record SoundRequested(string SoundId, Vector2? Position) : GameEvent;

/// <summary>The player received something (ammunition, medkits, a mission item; weapons lie on the floor).</summary>
public sealed record LootReceived(IReadOnlyList<LootEntry> Loot, Vector2 Position) : GameEvent;

/// <summary>A message from the observer, shown in the radio panel.</summary>
public sealed record TransmissionRequested(string TextKey, float Duration, string? Sound) : GameEvent;

/// <summary>A character appeared during the level (trigger).</summary>
public sealed record ActorSpawned(Actor Actor) : GameEvent;

public sealed record ReloadStarted(Actor Actor, WeaponDefinition Weapon) : GameEvent;

public sealed record ReloadFinished(Actor Actor, WeaponDefinition Weapon, int Rounds) : GameEvent;

public sealed record Healed(Actor Actor, float Amount) : GameEvent;

/// <summary>A creature crouches before leaping at its target (the warning).</summary>
public sealed record LeapStarted(Actor Actor, Actor Target) : GameEvent;

public sealed record LeapLaunched(Actor Actor, Vector2 Direction) : GameEvent;

public sealed record LeapLanded(Actor Actor, bool HitSomeone) : GameEvent;
