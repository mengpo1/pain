using Game.Core.Content;

namespace Game.Core.Definitions;

/// <summary>What ends a battle in the player's favour.</summary>
public enum ObjectiveKind
{
    /// <summary>Every hostile unit is dead or unconscious.</summary>
    EliminateHostiles,

    /// <summary>At least one player unit is still standing at the end of turn <see cref="ObjectiveDefinition.Turns"/>.</summary>
    Survive,

    /// <summary>A number of player units stand in the zone at the end of a player turn.</summary>
    ReachZone,
}

public sealed class ObjectiveDefinition
{
    public ObjectiveKind Kind { get; init; } = ObjectiveKind.EliminateHostiles;

    /// <summary>Survive: number of turns.</summary>
    public int Turns { get; init; }

    /// <summary>ReachZone: zone name (map legend "zone").</summary>
    public string? Zone { get; init; }

    /// <summary>ReachZone: units needed in the zone (0 = every surviving player unit).</summary>
    public int Units { get; init; }

    /// <summary>Text shown in the briefing and the interface.</summary>
    public string DescriptionKey { get; init; } = "";
}

/// <summary>Units of one side deployed at the start of a mission.</summary>
public sealed class DeploymentEntry
{
    public string Unit { get; init; } = "";
    public int Count { get; init; } = 1;
}

/// <summary>
/// How the battlefield is made: a single fixed block, or a terrain whose blocks are assembled at
/// random (with the battle seed) into a grid of <see cref="BlocksX"/> × <see cref="BlocksY"/>.
/// </summary>
public sealed class MissionMapDefinition
{
    /// <summary>Fixed map: one map block id.</summary>
    public string? Block { get; init; }

    /// <summary>Generated map: terrain id.</summary>
    public string? Terrain { get; init; }

    public int BlocksX { get; init; } = 3;
    public int BlocksY { get; init; } = 3;

    /// <summary>Number of levels of a generated map (blocks shorter than this get empty levels).</summary>
    public int Levels { get; init; } = 2;
}

/// <summary>A tactical mission: map, forces, light and objective.</summary>
public sealed class MissionDefinition : Definition
{
    public string NameKey { get; init; } = "";
    public string BriefingKey { get; init; } = "";

    public MissionMapDefinition Map { get; init; } = new();

    /// <summary>Global light (0 = darkest night, 15 = full day). Null = terrain value or day.</summary>
    public int? Light { get; init; }

    public IReadOnlyList<DeploymentEntry> Player { get; init; } = [];
    public IReadOnlyList<DeploymentEntry> Hostile { get; init; } = [];
    public IReadOnlyList<DeploymentEntry> Neutral { get; init; } = [];

    public ObjectiveDefinition Objective { get; init; } = new();

    /// <summary>The player loses if the battle is still running after this turn (0 = no limit).</summary>
    public int TurnLimit { get; init; }

    /// <summary>Fixed battle seed (reproducible demo); null = random at each start.</summary>
    public ulong? Seed { get; init; }

    /// <summary>Localization keys naming the sides in this mission.</summary>
    public string PlayerNameKey { get; init; } = "faction.player";
    public string HostileNameKey { get; init; } = "faction.hostile";
    public string NeutralNameKey { get; init; } = "faction.neutral";
}
