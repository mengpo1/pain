using Game.Core.Content;
using Game.Core.Definitions;
using Game.Desktop.Input;
using Game.Desktop.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace Game.Desktop;

/// <summary>Command-line options.</summary>
public sealed class LaunchOptions
{
    public string? ContentRoot { get; set; }
    public string? ModsRoot { get; set; }
    public string? Pack { get; set; }
    public List<string> Mods { get; } = new();
    public string? Mission { get; set; }
    public ulong? Seed { get; set; }
    public string? Language { get; set; }
    public string? Screenshot { get; set; }
    public int? Frames { get; set; }

    /// <summary>The AI also plays the player's side (demonstrations, smoke tests).</summary>
    public bool Autoplay { get; set; }

    public bool Mute { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>The MonoGame application: window, main loop, current screen and loaded game pack.</summary>
public sealed class GameHost : XnaGame
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _batch = null!;
    private Screen? _screen;
    private Screen? _nextScreen;
    private int _frame;
    private bool _audioAvailable;

    public GameHost(LaunchOptions options)
    {
        Options = options;
        Paths = new AppPaths(options.ContentRoot, options.ModsRoot);
        Log.Open(Paths.LogFile);
        Log.Info($"User folder (settings, log): {Paths.UserDirectory}");
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
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => OnResize();
        Input = new InputState(InputBindings.Create(Settings.Bindings, Log.Warning));
    }

    public LaunchOptions Options { get; }
    public AppPaths Paths { get; }
    public UserSettings Settings { get; }
    public InputState Input { get; }
    public GameSession Session { get; private set; } = null!;

    /// <summary>Game packs found in the content folder.</summary>
    public IReadOnlyList<ContentPack> GamePacks { get; private set; } = [];

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _audioAvailable = !Options.Mute && ProbeAudio();
        var diagnostics = new DiagnosticList();
        GamePacks = ContentLoader.DiscoverGamePacks(Paths.ContentRoot, diagnostics);
        foreach (ContentDiagnostic diagnostic in diagnostics.Items) Log.Warning(diagnostic.ToString());
        if (GamePacks.Count == 0) throw new ContentLoadException($"No game pack found in '{Paths.ContentRoot}'.");

        string pack = Options.Pack ?? Settings.GamePack ?? GamePacks[0].Manifest.Id;
        if (GamePacks.All(p => p.Manifest.Id != pack))
        {
            Log.Error($"Game pack '{pack}' not found; using '{GamePacks[0].Manifest.Id}'.");
            pack = GamePacks[0].Manifest.Id;
        }

        LoadSession(pack, Options.Mods.Count > 0 ? Options.Mods : Settings.Mods);
        SetScreen(CreateFirstScreen());
    }

    private Screen CreateFirstScreen()
    {
        if (Options.Mission is null) return new MenuScreen(this);
        if (!Session.Content.Rules.Missions.TryGetValue(Options.Mission, out MissionDefinition? mission) || Session.Content.InvalidMissions.Contains(mission.Id))
        {
            Log.Error($"Mission '{Options.Mission}' does not exist or is invalid; opening the menu.");
            return new MenuScreen(this);
        }

        return BattleScreen.Start(this, mission, Options.Seed ?? (ulong)Environment.TickCount64);
    }

    /// <summary>Loads (or reloads) a game pack with its mods; every screen is rebuilt afterwards.</summary>
    public void LoadSession(string pack, IReadOnlyList<string> mods)
    {
        Session?.Dispose();
        Session = GameSession.Load(GraphicsDevice, Paths, Settings, pack, mods, Input, _audioAvailable);
        Settings.GamePack = pack;
        Window.Title = Session.Texts.Get(Session.Content.Game.TitleKey);
        OnResize();
        Session.Audio.PlayMusic(Session.Content.Game.Sounds.MenuMusic);
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

    public void SaveSettings() => Settings.Save(Paths.SettingsFile);

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

        Session.Ui.Scale = MathF.Max(0.75f, GraphicsDevice.Viewport.Height / 720f);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_nextScreen is not null)
        {
            _screen?.Dispose();
            _screen = _nextScreen;
            _nextScreen = null;
        }

        float dt = Options.Screenshot is not null ? 1f / 60f : (float)gameTime.ElapsedGameTime.TotalSeconds;
        Input.Enabled = IsActive || Options.Autoplay;
        Input.Update();
        if (Input.KeyPressed(Keys.F11)) ToggleFullscreen();
        _screen?.Update(dt);
        Session.Audio.Update(dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        // Widgets report the mouse during Draw; Update reads last frame's value, so a click on the
        // interface is never also taken as a click on the map below.
        Session.Ui.MouseOverUi = false;
        _screen?.Draw(_batch);
        base.Draw(gameTime);

        _frame++;
        if (Options.Frames is { } frames && _frame >= frames)
        {
            if (Options.Screenshot is { } path) SaveScreenshot(path);
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
        SaveSettings();
        Log.Close();
        base.UnloadContent();
    }
}
