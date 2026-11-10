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
    public bool FriendlyFire { get; set; } = true;
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
    public ScoreRules Score { get; set; } = new();
}

public sealed class ExecutionRules
{
    public bool Enabled { get; set; } = true;
    /// <summary>Maximum gap between the player's edge and the target's edge.</summary>
    public float Range { get; set; } = 20f;
    public float DurationMultiplier { get; set; } = 1f;
    /// <summary>Moving cancels the execution.</summary>
    public bool CancelByMoving { get; set; } = true;
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
