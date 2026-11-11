using System.Numerics;
using TopDown.Core.Definitions;

namespace TopDown.Content;

/// <summary>manifest.json at the root of a content pack or mod.</summary>
public sealed class PackManifest
{
    /// <summary>Content format understood by this engine version.</summary>
    public const int SupportedFormatVersion = 1;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public int FormatVersion { get; set; } = SupportedFormatVersion;
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>A pack found on disk.</summary>
public sealed record ContentPack(PackManifest Manifest, string RootDirectory, bool IsBase);

/// <summary>game.json: everything that defines "which game" is played, merged across packs.</summary>
public sealed class GameSettings
{
    public string TitleKey { get; set; } = "game.title";
    public string DefaultLanguage { get; set; } = "en";
    public string FallbackLanguage { get; set; } = "en";
    public List<string> Campaign { get; set; } = new();
    /// <summary>Extra levels listed after the campaign (test scenes, challenges).</summary>
    public List<string> ExtraLevels { get; set; } = new();
    public string Theme { get; set; } = "default";
    public string? MenuMusic { get; set; }
    public GameRules Rules { get; set; } = new();
    public CameraSettings Camera { get; set; } = new();
    public GameSounds Sounds { get; set; } = new();
    public FeelSettings Feel { get; set; } = new();
}

/// <summary>
/// Game feel (presentation only, never gameplay rules): micro-freezes, camera shake and flashes on
/// impacts. All durations in seconds; shake from 0 (none) to 1 (strong).
/// </summary>
public sealed class FeelSettings
{
    /// <summary>A melee hit that does not floor the target.</summary>
    public ImpactFeel Hit { get; set; } = new() { Hitstop = 0.02f, Shake = 0.06f };
    public ImpactFeel Knockdown { get; set; } = new() { Hitstop = 0.045f, Shake = 0.16f };
    public ImpactFeel Kill { get; set; } = new() { Hitstop = 0.06f, Shake = 0.22f };
    public ImpactFeel ExecutionStrike { get; set; } = new() { Hitstop = 0.045f, Shake = 0.4f, Punch = 4f };
    public ImpactFeel ExecutionFinish { get; set; } = new() { Hitstop = 0.1f, Shake = 0.75f, Punch = 9f };
    /// <summary>The player is hit, knocked down or killed.</summary>
    public ImpactFeel PlayerHurt { get; set; } = new() { Hitstop = 0.06f, Shake = 0.45f };
    /// <summary>Longest total freeze, however many impacts happen at once.</summary>
    public float MaxHitstop { get; set; } = 0.1f;
    /// <summary>White flash on a character that is hit.</summary>
    public float HitFlash { get; set; } = 0.08f;
    /// <summary>Time for a character to visibly hit the floor.</summary>
    public float FallDuration { get; set; } = 0.09f;
}

public sealed class ImpactFeel
{
    public float Hitstop { get; set; }
    public float Shake { get; set; }
    /// <summary>Camera jolt in the direction of the blow, in world units.</summary>
    public float Punch { get; set; }
}

/// <summary>Sounds of generic events (not tied to a weapon or a character).</summary>
public sealed class GameSounds
{
    public string? UiMove { get; set; }
    public string? UiSelect { get; set; }
    public string? Pickup { get; set; }
    public string? Throw { get; set; }
    public string? Bounce { get; set; }
    public string? Impact { get; set; }
    public string? Footstep { get; set; }
    public string? ObjectiveComplete { get; set; }
    public string? LevelComplete { get; set; }
    /// <summary>A head that gives way (last blow of an execution).</summary>
    public string? Crush { get; set; }
    /// <summary>A limb torn off.</summary>
    public string? Tear { get; set; }
}

public sealed class CameraSettings
{
    /// <summary>World units visible vertically at zoom 1 (keeps the view identical on every screen).</summary>
    public float ViewHeight { get; set; } = 420f;
    public float Zoom { get; set; } = 1f;
    /// <summary>How fast the camera catches up with its target (higher = tighter).</summary>
    public float FollowSharpness { get; set; } = 10f;
    /// <summary>Fraction of the cursor offset the camera leans toward.</summary>
    public float LookAhead { get; set; } = 0.2f;
    public float LookAheadMax { get; set; } = 90f;
    /// <summary>Same while the "look further" key is held.</summary>
    public float FarLook { get; set; } = 0.6f;
    public float FarLookMax { get; set; } = 320f;
    public float ShakeDecay { get; set; } = 4f;
    public float MaxShake { get; set; } = 12f;
}

/// <summary>Visual identity of a game: colours, fonts, post-processing. Never gameplay.</summary>
public sealed class ThemeDefinition
{
    public string Id { get; set; } = "";
    public string Background { get; set; } = "#000000";

    /// <summary>Colour multiplied with sprites, per role: floor, wall, actor, weapon, decor, fx, blood.</summary>
    public Dictionary<string, string> Tints { get; set; } = new();

    public ShadowStyle Shadow { get; set; } = new();
    public UiStyle Ui { get; set; } = new();
    public PostEffects Post { get; set; } = new();
}

public sealed class ShadowStyle
{
    public bool Enabled { get; set; } = true;
    public Vector2 Offset { get; set; } = new(3f, 3f);
    public string Color { get; set; } = "#00000066";
}

public sealed class UiStyle
{
    public string Font { get; set; } = "font.ui";
    public string Text { get; set; } = "#ffffff";
    public string Accent { get; set; } = "#ff4f9a";
    public string Secondary { get; set; } = "#4ff0ff";
    public string Danger { get; set; } = "#ff3030";
    public string Panel { get; set; } = "#000000aa";
    public string Shadow { get; set; } = "#000000";
    /// <summary>"default", or "surveillance" for a camera-feed overlay (REC, clock, label).</summary>
    public string HudStyle { get; set; } = "default";
    public string? OverlayLabelKey { get; set; }
    /// <summary>Animated wave on titles (0 = static).</summary>
    public float TitleWave { get; set; } = 1f;
}

public sealed class PostEffects
{
    public float Vignette { get; set; } = 0.35f;
    public float Grain { get; set; }
    public float Scanlines { get; set; }
    /// <summary>Random brightness pulses of the whole image.</summary>
    public float Flicker { get; set; }
    /// <summary>Occasional horizontal glitch bands.</summary>
    public float VideoNoise { get; set; }
    /// <summary>0 = fully lit world, 1 = only lights are visible.</summary>
    public float Darkness { get; set; }
    public string DarknessColor { get; set; } = "#000000";
    /// <summary>Light carried by the player, in world units (0 = none).</summary>
    public float PlayerLightRadius { get; set; }
    public float PlayerLightIntensity { get; set; } = 1f;
    /// <summary>Muzzle flashes light up the surroundings.</summary>
    public float MuzzleFlashLight { get; set; } = 1f;
    /// <summary>Whole-image colour multiply (colour grading without shaders).</summary>
    public string ColorMultiply { get; set; } = "#ffffff";
    /// <summary>Characters outside the player's line of sight are not drawn.</summary>
    public bool HideUnseenActors { get; set; }
}

public sealed class AnimationDefinition
{
    /// <summary>Logical texture id of the sprite sheet.</summary>
    public string Texture { get; set; } = "";
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    /// <summary>Frame indices (left to right, top to bottom). Empty = every frame of the sheet.</summary>
    public List<int> Frames { get; set; } = new();
    public float FrameDuration { get; set; } = 0.1f;
    public bool Loop { get; set; } = true;
    /// <summary>Rotation origin in pixels; null = frame centre.</summary>
    public Vector2? Origin { get; set; }
    /// <summary>Frame index → event name (footstep sounds…). Purely cosmetic.</summary>
    public Dictionary<string, string> Events { get; set; } = new();
}

/// <summary>A selectable set of mods (an "experience" built on the same engine).</summary>
public sealed class ProfileDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public List<string> Mods { get; set; } = new();
}
