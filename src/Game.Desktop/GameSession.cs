using Game.Core.Content;
using Game.Desktop.Audio;
using Game.Desktop.Graphics;
using Game.Desktop.Input;
using Game.Desktop.Ui;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop;

/// <summary>
/// Everything that depends on the loaded game pack: rebuilt when the pack or the language
/// changes, disposed as a whole.
/// </summary>
public sealed class GameSession : IDisposable
{
    private GameSession(ContentDatabase content, AssetCache assets, AudioPlayer audio, Localizer texts, UiContext ui)
    {
        Content = content;
        Assets = assets;
        Audio = audio;
        Texts = texts;
        Ui = ui;
    }

    public ContentDatabase Content { get; }
    public AssetCache Assets { get; }
    public AudioPlayer Audio { get; }
    public Localizer Texts { get; }
    public UiContext Ui { get; }

    public static GameSession Load(GraphicsDevice device, AppPaths paths, UserSettings settings, string pack, IReadOnlyList<string> mods, InputState input, bool audioAvailable)
    {
        Log.Info($"Loading game pack '{pack}' (mods: {(mods.Count == 0 ? "none" : string.Join(", ", mods))}).");
        ContentDatabase content = ContentLoader.Load(new ContentLoadOptions
        {
            ContentRoot = paths.ContentRoot,
            ModsRoot = paths.ModsRoot,
            GamePack = pack,
            EnabledMods = mods,
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
        var ui = new UiContext(assets, content.Game.Ui, texts, input);
        return new GameSession(content, assets, audio, texts, ui);
    }

    public void Dispose()
    {
        Audio.Dispose();
        Assets.Dispose();
    }
}
