using Game.Core.Content;
using Game.Core.Definitions;
using Game.Desktop.Input;
using Game.Desktop.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Screens;

/// <summary>Main menu: choose the game pack, the language and a mission.</summary>
public sealed class MenuScreen : Screen
{
    private int _selected;

    public MenuScreen(GameHost host) : base(host)
    {
    }

    private IReadOnlyList<MissionDefinition> Missions => Session.Content.PlayableMissions;

    public override void Update(float dt)
    {
        InputState input = Host.Input;
        if (Missions.Count == 0) return;
        if (input.Pressed(GameAction.MenuDown)) _selected = (_selected + 1) % Missions.Count;
        if (input.Pressed(GameAction.MenuUp)) _selected = (_selected + Missions.Count - 1) % Missions.Count;
        if (input.Pressed(GameAction.Confirm)) StartMission();
    }

    private void StartMission()
    {
        Session.Audio.Play(Session.Content.Game.Sounds.Click);
        Host.SetScreen(BattleScreen.Start(Host, Missions[_selected], Host.Options.Seed ?? (ulong)Environment.TickCount64));
    }

    public override void Draw(SpriteBatch batch)
    {
        UiContext ui = Session.Ui;
        Localizer texts = Session.Texts;
        Rectangle screen = batch.GraphicsDevice.Viewport.Bounds;
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(ui.Assets.Pixel, screen, ui.Colors.Background);

        ui.Text(batch, texts.Get(Session.Content.Game.TitleKey), new Vector2(screen.Center.X, ui.S(40)), 54f, ui.Colors.Accent, Align.Center);
        if (Session.Content.Game.SubtitleKey.Length > 0)
            ui.Text(batch, texts.Get(Session.Content.Game.SubtitleKey), new Vector2(screen.Center.X, ui.S(112)), 18f, ui.Colors.Muted, Align.Center);

        int left = ui.S(60), top = ui.S(170), width = ui.S(360), row = ui.S(46);
        ui.Text(batch, texts.Get("menu.missions"), new Vector2(left, top - ui.S(34)), 20f, ui.Colors.Text);
        for (int i = 0; i < Missions.Count; i++)
        {
            var area = new Rectangle(left, top + i * row, width, row - ui.S(8));
            if (ui.Button(batch, area, texts.Get(Missions[i].NameKey), active: i == _selected, size: 16f))
            {
                if (i == _selected) StartMission();
                _selected = i;
            }
        }

        DrawBriefing(batch, ui, texts, new Rectangle(left + width + ui.S(40), top - ui.S(34), screen.Width - left * 2 - width - ui.S(40), ui.S(330)));

        int bottom = screen.Height - ui.S(70);
        int buttonWidth = ui.S(250);
        string packName = Session.Content.GamePack.Manifest.Name;
        if (ui.Button(batch, new Rectangle(left, bottom, buttonWidth, ui.S(40)), texts.Format("menu.pack", ("name", packName))))
            CyclePack();
        string languageName = texts.Language.ToUpperInvariant();
        if (ui.Button(batch, new Rectangle(left + buttonWidth + ui.S(16), bottom, ui.S(200), ui.S(40)), texts.Format("menu.language", ("name", languageName))))
            CycleLanguage();
        if (ui.Button(batch, new Rectangle(screen.Width - left - ui.S(160), bottom, ui.S(160), ui.S(40)), texts.Get("menu.quit")))
            Host.Exit();

        int problems = Session.Content.Diagnostics.Errors.Count();
        if (problems > 0)
            ui.Text(batch, texts.Format("menu.errors", ("count", problems)), new Vector2(left, bottom - ui.S(34)), 14f, ui.Colors.Bad);
        batch.End();
    }

    private void DrawBriefing(SpriteBatch batch, UiContext ui, Localizer texts, Rectangle area)
    {
        ui.Panel(batch, area);
        if (Missions.Count == 0) return;
        MissionDefinition mission = Missions[_selected];
        float y = area.Y + ui.S(14);
        ui.Text(batch, texts.Get(mission.NameKey), new Vector2(area.X + ui.S(16), y), 22f, ui.Colors.Accent);
        y += ui.S(40);
        foreach (string line in TextLayout.Wrap(ui, texts.Get(mission.BriefingKey), 15f, area.Width - ui.S(32)))
        {
            ui.Text(batch, line, new Vector2(area.X + ui.S(16), y), 15f, ui.Colors.Text);
            y += ui.S(22);
        }

        y += ui.S(14);
        ui.Text(batch, texts.Get("menu.objective"), new Vector2(area.X + ui.S(16), y), 15f, ui.Colors.Muted);
        y += ui.S(22);
        if (mission.Objective.DescriptionKey.Length > 0)
            ui.Text(batch, texts.Get(mission.Objective.DescriptionKey), new Vector2(area.X + ui.S(16), y), 15f, ui.Colors.Text);

        if (ui.Button(batch, new Rectangle(area.Right - ui.S(236), area.Bottom - ui.S(56), ui.S(220), ui.S(42)), texts.Get("menu.start"), size: 17f))
            StartMission();
    }

    private void CyclePack()
    {
        IReadOnlyList<ContentPack> packs = Host.GamePacks;
        int index = packs.ToList().FindIndex(p => p.Manifest.Id == Session.Content.GamePack.Manifest.Id);
        string next = packs[(index + 1) % packs.Count].Manifest.Id;
        Host.Settings.Mods.Clear();
        Host.LoadSession(next, []);
        Host.SaveSettings();
        Host.SetScreen(new MenuScreen(Host));
    }

    private void CycleLanguage()
    {
        List<string> languages = Session.Texts.Languages.Order(StringComparer.Ordinal).ToList();
        int index = languages.IndexOf(Session.Texts.Language);
        string next = languages[(index + 1) % languages.Count];
        Session.Texts.SetLanguage(next);
        Host.Settings.Language = next;
        Host.SaveSettings();
    }
}
