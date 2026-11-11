using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Core.Definitions;
using TopDown.Desktop.Audio;
using TopDown.Desktop.Graphics;
using TopDown.Desktop.Input;
using TopDown.Desktop.Ui;

namespace TopDown.Desktop.Screens;

/// <summary>Main menu with sub-pages. Every label comes from the localisation files.</summary>
public sealed class MenuScreen : Screen
{
    private const float ItemSpacing = 44f;
    private readonly PostProcessor _post;
    private readonly Camera2D _camera = new();
    private readonly Stack<(Page Page, int Index)> _history = new();
    private List<MenuItem> _items = new();
    private Page _page = Page.Main;
    private int _index;
    private int _scroll;
    private float _time;

    public MenuScreen(GameHost host) : base(host)
    {
        _post = new PostProcessor(host.GraphicsDevice, Session.Assets, Session.Content.Theme.Post);
        Session.Audio.PlayMusic(Session.Content.Game.MenuMusic);
        Session.Audio.PlayAmbience(null);
        Build();
    }

    private enum Page
    {
        Main,
        Levels,
        Settings,
        Mods,
        Diagnostics,
    }

    private Localizer T => Session.Texts;
    private UserSettings Settings => Host.Settings;
    private InputState Input => Host.Input;

    private sealed record MenuItem(Func<string> Label, Action? Select = null, Action<int>? Adjust = null, bool Enabled = true)
    {
        /// <summary>Extra line shown under the menu while the item is selected.</summary>
        public Func<string?>? Hint { get; init; }
    }

    private void Build()
    {
        _items = _page switch
        {
            Page.Main => MainItems(),
            Page.Levels => LevelItems(),
            Page.Settings => SettingsItems(),
            Page.Mods => [Back()],
            Page.Diagnostics => [Back()],
            _ => [],
        };
        _index = Math.Clamp(_index, 0, Math.Max(0, _items.Count - 1));
    }

    private List<MenuItem> MainItems()
    {
        var items = new List<MenuItem>
        {
            new(() => T.Get("menu.play"), () => Open(Page.Levels)),
            new(() => $"{T.Get("menu.modifier")} : {ModifierName()}", () => CycleModifier(1), CycleModifier) { Hint = ModifierDescription },
            new(() => $"{T.Get("menu.profile")} : {ProfileName(Session.Profile)}", () => CycleProfile(1), CycleProfile),
            new(() => $"{T.Get("menu.language")} : {LanguageName(T.Language)}", () => CycleLanguage(1), CycleLanguage),
            new(() => T.Get("menu.settings"), () => Open(Page.Settings)),
            new(() => T.Get("menu.mods"), () => Open(Page.Mods)),
        };

        DiagnosticList diagnostics = Session.Content.Diagnostics;
        if (diagnostics.Items.Any(d => d.Severity != Severity.Info))
        {
            items.Add(new MenuItem(() => T.Format("menu.diagnostics", ("errors", diagnostics.Errors.Count()), ("warnings", diagnostics.Warnings.Count())),
                () => Open(Page.Diagnostics)));
        }

        items.Add(new MenuItem(() => T.Get("menu.quit"), Host.Exit));
        return items;
    }

    private List<MenuItem> LevelItems()
    {
        IReadOnlyList<LevelDefinition> campaign = Session.Content.PlayableCampaign;
        var items = new List<MenuItem>();
        for (int i = 0; i < campaign.Count; i++)
        {
            int index = i;
            items.Add(new MenuItem(() => $"{index + 1}. {T.Get(campaign[index].NameKey)}", () => StartLevel(campaign, index)));
        }

        IReadOnlyList<LevelDefinition> extra = Session.Content.PlayableExtraLevels;
        foreach (LevelDefinition level in extra)
            items.Add(new MenuItem(() => $"* {T.Get(level.NameKey)}", () => StartLevel([level], 0)));

        foreach (string invalid in Session.Content.InvalidLevels)
            items.Add(new MenuItem(() => T.Format("menu.invalidLevel", ("level", invalid)), Enabled: false));
        items.Add(Back());
        return items;
    }

    private List<MenuItem> SettingsItems() =>
    [
        Volume("settings.master", () => Settings.MasterVolume, v => Settings.MasterVolume = v),
        Volume("settings.music", () => Settings.MusicVolume, v => Settings.MusicVolume = v),
        Volume("settings.ambience", () => Settings.AmbienceVolume, v => Settings.AmbienceVolume = v),
        Volume("settings.effects", () => Settings.EffectsVolume, v => Settings.EffectsVolume = v),
        Volume("settings.interface", () => Settings.UiVolume, v => Settings.UiVolume = v),
        new(() => $"{T.Get("settings.fullscreen")} : {OnOff(Settings.Fullscreen)}", Host.ToggleFullscreen, _ => Host.ToggleFullscreen()),
        Volume("settings.shake", () => Settings.ScreenShake, v => Settings.ScreenShake = v),
        new(() => $"{T.Get("settings.zoom")} : {Percent(Settings.Zoom)}", () => AdjustZoom(1), AdjustZoom),
        Back(),
    ];

    private MenuItem Volume(string key, Func<float> get, Action<float> set) =>
        new(() => $"{T.Get(key)} : {Percent(get())}", () => set(get() >= 0.999f ? 0f : MathF.Min(1f, get() + 0.1f)),
            step => set(Math.Clamp(MathF.Round((get() + step * 0.1f) * 10f) / 10f, 0f, 1f)));

    private MenuItem Back() => new(() => T.Get("menu.back"), GoBack);

    private void AdjustZoom(int step) => Settings.Zoom = Math.Clamp(MathF.Round((Settings.Zoom + step * 0.1f) * 10f) / 10f, 0.6f, 1.6f);

    private string Percent(float value) => string.Format(CultureInfo.InvariantCulture, "{0:0} %", value * 100f);

    private string OnOff(bool value) => T.Get(value ? "settings.on" : "settings.off");

    private string ModifierName() =>
        Session.Content.Definitions.Modifiers.TryGetValue(Settings.Modifier ?? "", out ModifierDefinition? modifier)
            ? T.Get(modifier.NameKey)
            : T.Get("modifier.none");

    private string? ModifierDescription() =>
        Session.Content.Definitions.Modifiers.TryGetValue(Settings.Modifier ?? "", out ModifierDefinition? modifier)
            ? T.Get(modifier.DescriptionKey)
            : null;

    private string ProfileName(ProfileDefinition profile) => T.Has(profile.NameKey) ? T.Get(profile.NameKey) : profile.Id;

    private string LanguageName(string language) => T.Has($"language.{language}") ? T.Get($"language.{language}") : language;

    private void CycleModifier(int step)
    {
        List<string?> ids = [null, .. Session.Content.Definitions.Modifiers.Keys.Order(StringComparer.Ordinal)];
        int current = Math.Max(0, ids.IndexOf(Settings.Modifier));
        Settings.Modifier = ids[(current + step + ids.Count) % ids.Count];
        Host.SaveSettings();
    }

    private void CycleProfile(int step)
    {
        IReadOnlyList<ProfileDefinition> profiles = Host.Profiles;
        if (profiles.Count < 2) return;
        int current = Math.Max(0, profiles.ToList().FindIndex(p => p.Id == Session.Profile.Id));
        Host.ChangeProfile(profiles[(current + step + profiles.Count) % profiles.Count]);
    }

    private void CycleLanguage(int step)
    {
        List<string> languages = T.Languages.Order(StringComparer.Ordinal).ToList();
        int current = Math.Max(0, languages.IndexOf(T.Language));
        string next = languages[(current + step + languages.Count) % languages.Count];
        T.SetLanguage(next);
        Settings.Language = next;
        Host.SaveSettings();
        Host.UpdateTitle();
    }

    private void StartLevel(IReadOnlyList<LevelDefinition> campaign, int index)
    {
        Session.Content.Definitions.Modifiers.TryGetValue(Settings.Modifier ?? "", out ModifierDefinition? modifier);
        Host.SetScreen(new PlayScreen(Host, campaign, index, modifier ?? ModifierDefinition.None, autopilot: false));
    }

    private void Open(Page page)
    {
        _history.Push((_page, _index));
        _page = page;
        _index = 0;
        _scroll = 0;
        Build();
    }

    private void GoBack()
    {
        if (_page == Page.Settings) Host.SaveSettings();
        if (_history.Count == 0) return;
        (_page, _index) = _history.Pop();
        Build();
    }

    public override void Update(float dt)
    {
        _time += dt;
        Host.Debug.Update(dt);
        Host.Debug.HandleInput(Input);
        if (_items.Count == 0) return;

        int previous = _index;
        if (Input.Pressed(GameAction.MenuDown)) _index = NextEnabled(1);
        if (Input.Pressed(GameAction.MenuUp)) _index = NextEnabled(-1);
        if (_page == Page.Diagnostics)
        {
            if (Input.Pressed(GameAction.MenuDown)) _scroll++;
            if (Input.Pressed(GameAction.MenuUp)) _scroll = Math.Max(0, _scroll - 1);
            _scroll = Math.Max(0, _scroll - Math.Sign(Input.ScrollDelta));
        }

        if (Input.MouseMoved)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Enabled && ItemArea(i).Contains(Input.MousePosition)) _index = i;
        }

        if (_index != previous) Session.Audio.Play(Session.Content.Game.Sounds.UiMove, channel: AudioChannel.Interface);

        MenuItem item = _items[_index];
        bool clicked = Input.LeftClicked && ItemArea(_index).Contains(Input.MousePosition);
        if (item.Enabled && (Input.Pressed(GameAction.Confirm) || clicked))
        {
            Session.Audio.Play(Session.Content.Game.Sounds.UiSelect, channel: AudioChannel.Interface);
            item.Select?.Invoke();
        }
        else if (item.Adjust is not null && (Input.Pressed(GameAction.MenuLeft) || Input.Pressed(GameAction.MenuRight)))
        {
            Session.Audio.Play(Session.Content.Game.Sounds.UiMove, channel: AudioChannel.Interface);
            item.Adjust(Input.Pressed(GameAction.MenuRight) ? 1 : -1);
        }
        else if (Input.Pressed(GameAction.Back))
        {
            GoBack();
        }
    }

    private int NextEnabled(int direction)
    {
        int index = _index;
        for (int i = 0; i < _items.Count; i++)
        {
            index = (index + direction + _items.Count) % _items.Count;
            if (_items[index].Enabled) return index;
        }

        return _index;
    }

    private Rectangle ItemArea(int index)
    {
        Rectangle screen = Host.GraphicsDevice.Viewport.Bounds;
        float scale = Session.Ui.Scale;
        float top = ListTop(screen);
        return new Rectangle(screen.Width / 2 - (int)(320 * scale), (int)(top + index * ItemSpacing * scale) - 4, (int)(640 * scale), (int)(40 * scale));
    }

    private float ListTop(Rectangle screen) =>
        _page is Page.Mods or Page.Diagnostics ? screen.Height - 90f * Session.Ui.Scale : screen.Height * 0.36f;

    public override void Draw(SpriteBatch batch, float dt)
    {
        Rectangle screen = Host.GraphicsDevice.Viewport.Bounds;
        UiContext ui = Session.Ui;
        _camera.SetViewport(screen.Width, screen.Height, Session.Content.Game.Camera, 1f);
        _post.BeginScene(ui.Colors.Background);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawBackdrop(batch, screen);
        batch.End();
        _post.EndScene(batch, _camera, [], dt);

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        ui.Title(batch, T.Get(Session.Content.Game.TitleKey), new Vector2(screen.Width * 0.5f, screen.Height * 0.12f), 72f, ui.Colors.Accent, _time);
        string subtitle = _page switch
        {
            Page.Levels => T.Get("menu.chooseLevel"),
            Page.Settings => T.Get("menu.settings"),
            Page.Mods => T.Get("menu.mods"),
            Page.Diagnostics => T.Get("menu.diagnosticsTitle"),
            _ => ProfileName(Session.Profile),
        };
        ui.Text(batch, subtitle, new Vector2(screen.Width * 0.5f, screen.Height * 0.25f), 24f, ui.Colors.Secondary, Align.Center);

        if (_page == Page.Mods) DrawMods(batch, screen);
        if (_page == Page.Diagnostics) DrawDiagnostics(batch, screen);

        for (int i = 0; i < _items.Count; i++)
        {
            Rectangle area = ItemArea(i);
            bool selected = i == _index;
            Color color = !_items[i].Enabled ? ui.Colors.Text * 0.4f : selected ? ui.Colors.Secondary : ui.Colors.Text;
            string label = _items[i].Label();
            if (selected) label = $"> {label} <";
            ui.Text(batch, label, new Vector2(screen.Width * 0.5f, area.Y + 4), selected ? 32f : 28f, color, Align.Center);
        }

        if (_items.Count > 0 && _items[_index].Hint?.Invoke() is { } itemHint)
        {
            Rectangle last = ItemArea(_items.Count - 1);
            ui.Text(batch, itemHint, new Vector2(screen.Width * 0.5f, last.Bottom + 24f * ui.Scale), 20f, ui.Colors.Accent, Align.Center);
        }

        if (_page == Page.Settings)
        {
            string hint = T.Format("settings.bindingsHint", ("path", Host.Paths.SettingsFile));
            ui.Text(batch, hint, new Vector2(screen.Width * 0.5f, screen.Height - 40f * ui.Scale), 16f, ui.Colors.Text * 0.7f, Align.Center);
        }

        Host.Debug.DrawPanel(batch, ui, null, null);
        batch.End();
    }

    private void DrawBackdrop(SpriteBatch batch, Rectangle screen)
    {
        UiContext ui = Session.Ui;
        float spacing = 48f * ui.Scale;
        float offset = _time * 12f % spacing;
        for (float x = -spacing + offset; x < screen.Width + spacing; x += spacing)
            Primitives.Line(batch, Session.Assets.Pixel, new Vector2(x, 0), new Vector2(x - screen.Height * 0.3f, screen.Height), ui.Colors.Accent * 0.08f, 2f);
    }

    private void DrawMods(SpriteBatch batch, Rectangle screen)
    {
        UiContext ui = Session.Ui;
        float y = screen.Height * 0.33f;
        IReadOnlyList<ContentPack> mods = Session.Content.InstalledMods;
        if (mods.Count == 0) ui.Text(batch, T.Get("mods.none"), new Vector2(screen.Width * 0.5f, y), 22f, ui.Colors.Text, Align.Center);
        foreach (ContentPack mod in mods)
        {
            bool enabled = Session.Content.LoadedPacks.Contains(mod);
            string status = T.Get(enabled ? "mods.enabled" : "mods.disabled");
            ui.Text(batch, $"{mod.Manifest.Name} {mod.Manifest.Version} — {mod.Manifest.Author} [{status}]", new Vector2(screen.Width * 0.5f, y), 22f,
                enabled ? ui.Colors.Secondary : ui.Colors.Text, Align.Center);
            y += 28f * ui.Scale;
            ui.Text(batch, mod.Manifest.Description, new Vector2(screen.Width * 0.5f, y), 16f, ui.Colors.Text * 0.8f, Align.Center);
            y += 34f * ui.Scale;
        }

        ui.Text(batch, T.Get("mods.hint"), new Vector2(screen.Width * 0.5f, y + 10f), 16f, ui.Colors.Text * 0.7f, Align.Center);
    }

    private void DrawDiagnostics(SpriteBatch batch, Rectangle screen)
    {
        UiContext ui = Session.Ui;
        float y = screen.Height * 0.32f;
        float bottom = screen.Height - 110f * ui.Scale;
        IEnumerable<ContentDiagnostic> lines = Session.Content.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Skip(_scroll);
        foreach (ContentDiagnostic diagnostic in lines)
        {
            if (y > bottom) break;
            Color color = diagnostic.Severity == Severity.Error ? ui.Colors.Danger : ui.Colors.Text;
            string text = diagnostic.ToString();
            if (text.Length > 150) text = text[..147] + "…";
            ui.Text(batch, text, new Vector2(40f, y), 15f, color, shadow: 1f);
            y += 20f * ui.Scale;
        }
    }

    public override void Dispose() => _post.Dispose();
}
