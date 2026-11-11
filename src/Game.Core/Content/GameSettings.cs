using Game.Core.Definitions;

namespace Game.Core.Content;

/// <summary>
/// game.json: what defines "which game" is played (title, languages, global rules, interface
/// colours), merged across packs like any definition.
/// </summary>
public sealed class GameSettings
{
    public string TitleKey { get; init; } = "game.title";
    public string SubtitleKey { get; init; } = "";
    public string DefaultLanguage { get; init; } = "en";
    public string FallbackLanguage { get; init; } = "en";

    /// <summary>Missions offered in the main menu, in order.</summary>
    public IReadOnlyList<string> Missions { get; init; } = [];

    public BattleRules Battle { get; init; } = new();
    public UiStyle Ui { get; init; } = new();
    public GameSounds Sounds { get; init; } = new();
}

/// <summary>Interface look. Colours are "#rrggbb" or "#rrggbbaa".</summary>
public sealed class UiStyle
{
    public string Font { get; init; } = "font.ui";
    public string Background { get; init; } = "#101418";
    public string Panel { get; init; } = "#1c242ccc";
    public string PanelBorder { get; init; } = "#4a6070";
    public string Text { get; init; } = "#e8eef2";
    public string Muted { get; init; } = "#8a9aa6";
    public string Accent { get; init; } = "#f0c040";
    public string Friendly { get; init; } = "#58b0ff";
    public string Hostile { get; init; } = "#ff5a4a";
    public string Neutral { get; init; } = "#c0c070";
    public string Good { get; init; } = "#60d070";
    public string Bad { get; init; } = "#e04040";

    /// <summary>
    /// Pixel of a map sprite placed on the north corner of its tile's floor diamond, for
    /// textures that do not declare their own anchor (64 × 80 frames by default: 32, 48).
    /// </summary>
    public int[] MapSpriteAnchor { get; init; } = [32, 48];
}

/// <summary>Sounds of generic events (interface, footsteps). Ids from assets.json.</summary>
public sealed class GameSounds
{
    public string? Click { get; init; }
    public string? Footstep { get; init; }
    public string? Door { get; init; }
    public string? Explosion { get; init; }
    public string? Death { get; init; }
    public string? TurnStart { get; init; }
    public string? Victory { get; init; }
    public string? Defeat { get; init; }
    public string? MenuMusic { get; init; }
    public string? BattleMusic { get; init; }
}
