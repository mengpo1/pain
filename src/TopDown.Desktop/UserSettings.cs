using System.Text.Json;
using TopDown.Content.Json;

namespace TopDown.Desktop;

/// <summary>Per-player preferences saved in the user folder (never in the content folders).</summary>
public sealed class UserSettings
{
    public string? Language { get; set; }
    public string Profile { get; set; } = "action";
    public string? Modifier { get; set; }
    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.6f;
    public float AmbienceVolume { get; set; } = 0.8f;
    public float EffectsVolume { get; set; } = 0.9f;
    public float UiVolume { get; set; } = 0.7f;
    public bool Fullscreen { get; set; }
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public bool VSync { get; set; } = true;
    public float ScreenShake { get; set; } = 1f;
    /// <summary>Multiplies the content's camera zoom.</summary>
    public float Zoom { get; set; } = 1f;
    /// <summary>Action name → bindings ("W", "Mouse:Left"…). Missing actions keep their defaults.</summary>
    public Dictionary<string, List<string>> Bindings { get; set; } = new();

    public static UserSettings Load(string path)
    {
        if (!File.Exists(path)) return new UserSettings();
        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions(ContentJson.Options) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip };
            return JsonSerializer.Deserialize<UserSettings>(json, options) ?? new UserSettings();
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
            File.WriteAllText(path, JsonSerializer.Serialize(this, ContentJson.Options));
        }
        catch (IOException exception)
        {
            Log.Warning($"Cannot save settings to '{path}': {exception.Message}");
        }
    }
}
