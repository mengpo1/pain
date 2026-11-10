namespace TopDown.Core.Definitions;

/// <summary>What a tile, a door or a prop stops. Each property is independent.</summary>
[Flags]
public enum Blocking
{
    None = 0,
    Movement = 1,
    Projectiles = 2,
    Vision = 4,
    All = Movement | Projectiles | Vision,
}

public enum TileLayer
{
    Floor,
    Wall,
}

/// <summary>
/// A tile type. Its look (<see cref="SpriteId"/>) is unrelated to its physical properties.
/// </summary>
public sealed class TileDefinition
{
    public string Id { get; set; } = "";
    public string SpriteId { get; set; } = "";
    public TileLayer Layer { get; set; } = TileLayer.Floor;
    public bool BlocksMovement { get; set; }
    public bool BlocksProjectiles { get; set; }
    public bool BlocksVision { get; set; }

    /// <summary>Destroyed by bullets and thrown weapons (glass, for instance).</summary>
    public bool Destructible { get; set; }

    /// <summary>Tile that replaces this one once destroyed.</summary>
    public string? BreaksInto { get; set; }

    public string? BreakSound { get; set; }
    public float BreakNoiseRadius { get; set; } = 250f;

    public Blocking Blocking =>
        (BlocksMovement ? Blocking.Movement : Blocking.None)
        | (BlocksProjectiles ? Blocking.Projectiles : Blocking.None)
        | (BlocksVision ? Blocking.Vision : Blocking.None);

    /// <summary>Tile used outside the map: blocks everything and is never drawn.</summary>
    public static TileDefinition Void { get; } = new()
    {
        Id = "void",
        Layer = TileLayer.Wall,
        BlocksMovement = true,
        BlocksProjectiles = true,
        BlocksVision = true,
    };
}

public sealed class DoorDefinition
{
    public string Id { get; set; } = "";
    public string SpriteId { get; set; } = "";
    public float Thickness { get; set; } = 6f;
    public float MaxAngleDegrees { get; set; } = 95f;

    /// <summary>Angular speed given to the door when pushed (degrees/s).</summary>
    public float PushSpeedDegrees { get; set; } = 600f;

    /// <summary>Exponential slow-down of the swing.</summary>
    public float Damping { get; set; } = 2.5f;

    /// <summary>Pulls the door back toward closed. 0 = stays where it is.</summary>
    public float CloseSpring { get; set; }

    /// <summary>A door swinging faster than this knocks characters down (degrees/s).</summary>
    public float KnockdownSpeedDegrees { get; set; } = 250f;
    public float KnockdownDuration { get; set; } = 2.5f;

    public bool BlocksVision { get; set; } = true;
    public bool BlocksProjectiles { get; set; } = true;
    public float HitNoiseRadius { get; set; } = 220f;
    public string? HitSound { get; set; }
}

public sealed class ModifierDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public float MoveSpeed { get; set; } = 1f;
    public float DamageTaken { get; set; } = 1f;
    public float Spread { get; set; } = 1f;

    /// <summary>Multiplies the vision range of enemies.</summary>
    public float EnemyVision { get; set; } = 1f;

    /// <summary>Above 1, executions are faster.</summary>
    public float ExecutionSpeed { get; set; } = 1f;

    public bool AllowFirearms { get; set; } = true;
    public string? StartingWeaponId { get; set; }

    public static ModifierDefinition None { get; } = new() { Id = "none" };
}
