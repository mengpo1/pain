using Game.Core.Content;

namespace Game.Core.Definitions;

/// <summary>
/// Identifiers of the statistics the rules use. Their names, display order and caps come from
/// content (<c>stats/*.json</c>); a pack may declare more statistics, which are stored, saved and
/// shown like the others but have no effect until a rule reads them.
/// </summary>
public static class StatIds
{
    public const string TimeUnits = "tu";
    public const string Stamina = "stamina";
    public const string Health = "health";
    public const string Bravery = "bravery";
    public const string Reactions = "reactions";
    public const string Firing = "firing";
    public const string Throwing = "throwing";
    public const string Strength = "strength";
    public const string Melee = "melee";
    public const string PsiStrength = "psiStrength";
    public const string PsiSkill = "psiSkill";

    /// <summary>Statistics every pack must declare because the rules read them.</summary>
    public static readonly IReadOnlyList<string> Required =
        [TimeUnits, Stamina, Health, Bravery, Reactions, Firing, Throwing, Strength, Melee];
}

public sealed class StatDefinition : Definition
{
    public string NameKey { get; init; } = "";

    /// <summary>Short label for compact displays (HUD bars).</summary>
    public string ShortKey { get; init; } = "";

    /// <summary>Highest value training can reach.</summary>
    public int Max { get; init; } = 255;

    /// <summary>Position in stat lists (lower first).</summary>
    public int Order { get; init; }
}
