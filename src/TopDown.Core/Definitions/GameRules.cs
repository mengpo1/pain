namespace TopDown.Core.Definitions;

public enum PickupWhileArmed
{
    /// <summary>Drops the current weapon and takes the one on the floor.</summary>
    Swap,
    /// <summary>Always throws the current weapon; picking up requires empty hands.</summary>
    Throw,
}

/// <summary>Global gameplay rules of a game. Content packs can override any value.</summary>
public sealed class GameRules
{
    /// <summary>Which kinds of attack can hurt characters of the same faction.</summary>
    public FriendlyFireRules FriendlyFire { get; set; } = new();
    public string UnarmedWeaponId { get; set; } = "unarmed";
    public float PickupRadius { get; set; } = 24f;
    public PickupWhileArmed PickupWhileArmed { get; set; } = PickupWhileArmed.Swap;
    public bool DropWeaponOnKnockdown { get; set; } = true;
    public bool DropWeaponOnDeath { get; set; } = true;
    public bool DoorsKnockDown { get; set; } = true;

    /// <summary>Enemies never run out of ammunition (the weapon keeps its ammo when dropped).</summary>
    public bool EnemiesHaveInfiniteAmmo { get; set; } = true;

    /// <summary>Faster than this, a thrown weapon still hurts (units/s).</summary>
    public float ThrownHarmSpeed { get; set; } = 260f;

    public ExecutionRules Execution { get; set; } = new();
    public CombatRules Combat { get; set; } = new();
    public ScoreRules Score { get; set; } = new();
}

/// <summary>
/// Damage between allies, per kind of attack. Allies always collide physically; these flags only
/// decide whether an attack can hurt them.
/// </summary>
public sealed class FriendlyFireRules
{
    public bool Melee { get; set; }
    public bool Projectiles { get; set; } = true;
    public bool Thrown { get; set; }
    /// <summary>Reserved for explosive weapons (none in the current content).</summary>
    public bool Explosions { get; set; } = true;
    /// <summary>A door kicked open knocks down whoever stands behind it, allies included.</summary>
    public bool Doors { get; set; } = true;
}

public sealed class CombatRules
{
    /// <summary>An attack, pickup or interaction pressed slightly too early is kept this long (seconds).</summary>
    public float InputBuffer { get; set; } = 0.15f;
    /// <summary>Bullets hit characters lying on the floor (independent from executions).</summary>
    public bool BulletsHitDowned { get; set; } = true;
    /// <summary>Any hit on a dazed (stunned) character knocks it down.</summary>
    public bool HitOnStunnedKnocksDown { get; set; } = true;
}

public sealed class ExecutionRules
{
    public bool Enabled { get; set; } = true;
    /// <summary>Maximum gap between the executioner's edge and the target's edge.</summary>
    public float Range { get; set; } = 24f;
    /// <summary>Multiplies every execution timing (wind-up, intervals, recovery).</summary>
    public float DurationMultiplier { get; set; } = 1f;
    /// <summary>Moving cancels the execution (off: an execution is a commitment).</summary>
    public bool CancelByMoving { get; set; }
    /// <summary>Dazed characters still standing can be executed.</summary>
    public bool AllowOnStunned { get; set; }
    /// <summary>Enemies finish off a player on the floor.</summary>
    public bool EnemiesCanExecute { get; set; } = true;
}

public sealed class ScoreRules
{
    public bool Enabled { get; set; } = true;
    /// <summary>Kills closer together than this extend the combo (seconds).</summary>
    public float ComboWindow { get; set; } = 3f;
    /// <summary>Points per kill method: melee, firearm, thrown, door, execution.</summary>
    public Dictionary<string, int> Points { get; set; } = new()
    {
        ["melee"] = 400,
        ["firearm"] = 300,
        ["thrown"] = 600,
        ["door"] = 800,
        ["execution"] = 500,
    };
}
