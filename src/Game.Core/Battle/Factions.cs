namespace Game.Core.Battle;

/// <summary>
/// The sides of a battle, as in OpenXcom: the player, the hostiles (AI) and neutrals (civilians,
/// AI). What each side is called comes from the mission and the localization.
/// </summary>
public enum Faction
{
    Player,
    Hostile,
    Neutral,
}

public static class Factions
{
    public const int Count = 3;

    public static readonly IReadOnlyList<Faction> All = [Faction.Player, Faction.Hostile, Faction.Neutral];

    /// <summary>Neutrals are the hostiles' targets but never shoot the player.</summary>
    public static bool AreEnemies(Faction a, Faction b) => a != b && (a == Faction.Hostile || b == Faction.Hostile);
}
