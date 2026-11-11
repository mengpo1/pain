using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TopDown.Content;
using TopDown.Content.Json;
using TopDown.Core.Definitions;
using TopDown.Desktop.Input;
using TopDown.Desktop.Screens;
using TopDown.Desktop.Ui;

namespace TopDown.Desktop;

/// <summary>Command-line options.</summary>
public sealed class LaunchOptions
{
    public string? ContentRoot { get; set; }
    public string? ModsRoot { get; set; }
    public string? Profile { get; set; }
    public List<string> ExtraMods { get; } = new();
    public string? Level { get; set; }
    public string? Language { get; set; }
    public string? Screenshot { get; set; }
    public int? Frames { get; set; }
    public bool Autopilot { get; set; }
    public string? Modifier { get; set; }
    public bool Mute { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>The MonoGame application: window, main loop, current screen and content session.</summary>
public sealed class GameHost : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly LaunchOptions _options;
    private SpriteBatch _batch = null!;
    private Screen? _screen;
    private Screen? _nextScreen;
    private int _frame;
    private bool _audioAvailable;

    public GameHost(LaunchOptions options)
    {
        _options = options;
        Paths = new AppPaths(options.ContentRoot, options.ModsRoot);
        Log.Open(Paths.LogFile);
        Settings = UserSettings.Load(Paths.SettingsFile);
        if (options.Language is not null) Settings.Language = options.Language;

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = options.Width ?? Settings.WindowWidth,
            PreferredBackBufferHeight = options.Height ?? Settings.WindowHeight,
            IsFullScreen = Settings.Fullscreen && options.Screenshot is null,
            SynchronizeWithVerticalRetrace = Settings.VSync,
            HardwareModeSwitch = false,
        };
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => OnResize();
        Input = new InputState(InputBindings.Create(Settings.Bindings, Log.Warning));
    }

    public AppPaths Paths { get; }
    public UserSettings Settings { get; }
    public InputState Input { get; }
    public DebugOverlay Debug { get; } = new();
    public GameSession Session { get; private set; } = null!;
    public IReadOnlyList<ProfileDefinition> Profiles { get; private set; } = [];

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _audioAvailable = !_options.Mute && ProbeAudio();
        Profiles = LoadProfiles();
        ProfileDefinition profile = Profiles.FirstOrDefault(p => p.Id == (_options.Profile ?? Settings.Profile)) ?? Profiles[0];
        if (_options.ExtraMods.Count > 0)
            profile = new ProfileDefinition { Id = profile.Id, NameKey = profile.NameKey, Mods = [.. profile.Mods, .. _options.ExtraMods] };

        Session = GameSession.Load(GraphicsDevice, Paths, Settings, profile, _audioAvailable);
        UpdateTitle();
        OnResize();
        SetScreen(CreateFirstScreen());
    }

    private Screen CreateFirstScreen()
    {
        if (_options.Level is null) return new MenuScreen(this);
        ContentDatabase content = Session.Content;
        if (!content.Levels.TryGetValue(_options.Level, out LevelDefinition? level) || content.InvalidLevels.Contains(level.Id))
        {
            Log.Error($"Level '{_options.Level}' does not exist or is invalid; opening the menu.");
            return new MenuScreen(this);
        }

        List<LevelDefinition> playlist = content.PlayableCampaign.ToList();
        int index = playlist.FindIndex(l => l.Id == level.Id);
        if (index < 0)
        {
            playlist = [level];
            index = 0;
        }

        content.Definitions.Modifiers.TryGetValue(_options.Modifier ?? Settings.Modifier ?? "", out ModifierDefinition? modifier);
        return new PlayScreen(this, playlist, index, modifier ?? ModifierDefinition.None, _options.Autopilot);
    }

    /// <summary>Profiles from Content/profiles.json; "base only" if the file is missing.</summary>
    private List<ProfileDefinition> LoadProfiles()
    {
        var fallback = new List<ProfileDefinition> { new() { Id = "action", NameKey = "profile.action" } };
        if (!File.Exists(Paths.ProfilesFile)) return fallback;
        try
        {
            var node = ContentJson.ParseFile(Paths.ProfilesFile);
            List<ProfileDefinition>? profiles = node?["profiles"] is { } list ? ContentJson.Deserialize<List<ProfileDefinition>>(list) : null;
            return profiles is { Count: > 0 } ? profiles : fallback;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or IOException or InvalidOperationException)
        {
            Log.Error($"Invalid {Paths.ProfilesFile}: {exception.Message}");
            return fallback;
        }
    }

    private static bool ProbeAudio()
    {
        try
        {
            _ = SoundEffect.MasterVolume;
            using var probe = new DynamicSoundEffectInstance(22050, AudioChannels.Mono);
            return true;
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InvalidOperationException or TypeInitializationException or DllNotFoundException)
        {
            Log.Warning($"Audio unavailable, the game runs silently: {exception.Message}");
            return false;
        }
    }

    public void SetScreen(Screen screen) => _nextScreen = screen;

    /// <summary>Reloads all content with another set of mods (menu "Profile" option).</summary>
    public void ChangeProfile(ProfileDefinition profile)
    {
        Settings.Profile = profile.Id;
        SaveSettings();
        ReloadSession(profile);
    }

    /// <summary>Re-reads content files from disk (F9): edit JSON, press F9, see the change.</summary>
    public void ReloadSession(ProfileDefinition? profile = null)
    {
        _screen?.Dispose();
        _screen = null;
        _nextScreen?.Dispose();
        _nextScreen = null;
        Session.Dispose();
        Session = GameSession.Load(GraphicsDevice, Paths, Settings, profile ?? Session.Profile, _audioAvailable);
        UpdateTitle();
        OnResize();
        SetScreen(new MenuScreen(this));
    }

    public void SaveSettings() => Settings.Save(Paths.SettingsFile);

    public void UpdateTitle() => Window.Title = Session.Texts.Get(Session.Content.Game.TitleKey);

    public void ToggleFullscreen()
    {
        Settings.Fullscreen = !Settings.Fullscreen;
        _graphics.IsFullScreen = Settings.Fullscreen;
        _graphics.ApplyChanges();
        SaveSettings();
        OnResize();
    }

    private void OnResize()
    {
        if (Session is null) return;
        Rectangle bounds = Window.ClientBounds;
        if (!_graphics.IsFullScreen && bounds.Width > 0 && bounds.Height > 0 &&
            (bounds.Width != _graphics.PreferredBackBufferWidth || bounds.Height != _graphics.PreferredBackBufferHeight))
        {
            _graphics.PreferredBackBufferWidth = bounds.Width;
            _graphics.PreferredBackBufferHeight = bounds.Height;
            _graphics.ApplyChanges();
        }

        Session.Ui.Scale = MathF.Max(0.6f, GraphicsDevice.Viewport.Height / 720f);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_nextScreen is not null)
        {
            _screen?.Dispose();
            _screen = _nextScreen;
            _nextScreen = null;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Input.Enabled = IsActive || _options.Autopilot;
        Input.Update();
        if (Input.KeyPressed(Keys.F11)) ToggleFullscreen();
        if (Input.KeyPressed(Keys.F9))
        {
            Log.Info("Reloading content (F9).");
            ReloadSession();
            return;
        }

        if (_options.Screenshot is not null) dt = 1f / 60f;
        _screen?.Update(dt);
        IsMouseVisible = _screen?.ShowSystemCursor ?? true;
        Session.Audio.Update(dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        float dt = _options.Screenshot is not null ? 1f / 60f : (float)gameTime.ElapsedGameTime.TotalSeconds;
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _screen?.Draw(_batch, dt);
        base.Draw(gameTime);

        _frame++;
        if (_options.Frames is { } frames && _frame >= frames)
        {
            if (_options.Screenshot is { } path) SaveScreenshot(path);
            Log.Info($"Stopping after {_frame} frames as requested.");
            Exit();
        }
    }

    private void SaveScreenshot(string path)
    {
        int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var data = new Color[width * height];
        GraphicsDevice.GetBackBufferData(data);
        using var texture = new Texture2D(GraphicsDevice, width, height);
        texture.SetData(data);
        using FileStream stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
        Log.Info($"Screenshot saved to {path}.");
    }

    protected override void UnloadContent()
    {
        _screen?.Dispose();
        Session?.Dispose();
        _batch?.Dispose();
        Log.Close();
        base.UnloadContent();
    }
}
