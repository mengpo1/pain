using System.Text.Json;
using Game.Core.Content;

namespace Game.Desktop;

/// <summary>Per-player preferences saved in the user folder (never in the content folders).</summary>
public sealed class UserSettings
{
    public string? Language { get; set; }

    /// <summary>Game pack played (folder name in Content/).</summary>
    public string? GamePack { get; set; }

    /// <summary>Mods enabled on top of the game pack, in load order.</summary>
    public List<string> Mods { get; set; } = new();

    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.5f;
    public float EffectsVolume { get; set; } = 0.9f;
    public bool Fullscreen { get; set; }
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 800;
    public bool VSync { get; set; } = true;

    /// <summary>Camera speed with the keyboard or the screen edges (pixels per second).</summary>
    public float ScrollSpeed { get; set; } = 900f;

    /// <summary>Scroll when the mouse touches the window edge.</summary>
    public bool EdgeScroll { get; set; } = true;

    /// <summary>Delay between animation steps of the simulation (seconds per tile walked).</summary>
    public float AnimationStep { get; set; } = 0.12f;

    /// <summary>Action name → bindings ("W", "Mouse:Left"…). Missing actions keep their defaults.</summary>
    public Dictionary<string, List<string>> Bindings { get; set; } = new();

    public static UserSettings Load(string path)
    {
        if (!File.Exists(path)) return new UserSettings();
        try
        {
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), ContentJson.LenientOptions) ?? new UserSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            Log.Warning($"Settings file '{path}' is invalid ({exception.Message}); defaults are used.");
            return new UserSettings();
        }
    }

    public void Save(string path)
    {
        try
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, ContentJson.Options));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"Cannot save settings to '{path}': {exception.Message}");
        }
    }
}
