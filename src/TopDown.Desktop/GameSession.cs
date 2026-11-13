using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Desktop.Audio;
using TopDown.Desktop.Graphics;
using TopDown.Desktop.Ui;

namespace TopDown.Desktop;

/// <summary>
/// Everything that depends on the loaded content: rebuilt when the profile changes or when
/// content is reloaded, disposed as a whole.
/// </summary>
public sealed class GameSession : IDisposable
{
    private GameSession(ProfileDefinition profile, ContentDatabase content, AssetCache assets, AudioPlayer audio, Localizer texts, UiContext ui)
    {
        Profile = profile;
        Content = content;
        Assets = assets;
        Audio = audio;
        Texts = texts;
        Ui = ui;
    }

    public ProfileDefinition Profile { get; }
    public ContentDatabase Content { get; }
    public AssetCache Assets { get; }
    public AudioPlayer Audio { get; }
    public Localizer Texts { get; }
    public UiContext Ui { get; }

    public static GameSession Load(GraphicsDevice device, AppPaths paths, UserSettings settings, ProfileDefinition profile, bool audioAvailable)
    {
        Log.Info($"Loading content profile '{profile.Id}' (mods: {(profile.Mods.Count == 0 ? "none" : string.Join(", ", profile.Mods))}).");
        ContentDatabase content = ContentLoader.Load(new ContentLoadOptions
        {
            ContentRoot = paths.ContentRoot,
            ModsRoot = paths.ModsRoot,
            EnabledMods = profile.Mods,
        });

        foreach (ContentDiagnostic diagnostic in content.Diagnostics.Items)
        {
            if (diagnostic.Severity == Severity.Error) Log.Error(diagnostic.ToString());
            else if (diagnostic.Severity == Severity.Warning) Log.Warning(diagnostic.ToString());
        }

        Log.Info($"Packs loaded: {string.Join(", ", content.LoadedPacks.Select(p => $"{p.Manifest.Id} {p.Manifest.Version}"))}.");
        var assets = new AssetCache(device, content.Assets, audioAvailable);
        var audio = new AudioPlayer(assets, settings, audioAvailable);
        Localizer texts = content.CreateLocalizer(settings.Language);
        texts.MissingKey += Log.Warning;
        var ui = new UiContext(assets, content.Theme, texts);
        return new GameSession(profile, content, assets, audio, texts, ui);
    }

    public void Dispose()
    {
        Audio.Dispose();
        Assets.Dispose();
    }
}
