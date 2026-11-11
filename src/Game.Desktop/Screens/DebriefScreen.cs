using Game.Core.Battle;
using Game.Core.Content;
using Game.Desktop.Input;
using Game.Desktop.Tactical;
using Game.Desktop.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Desktop.Screens;

/// <summary>Mission result: outcome, turns, what became of each unit, hostiles neutralised.</summary>
public sealed class DebriefScreen : Screen
{
    private readonly Battle _battle;

    public DebriefScreen(GameHost host, Battle battle) : base(host)
    {
        _battle = battle;
        Session.Audio.PlayMusic(Session.Content.Game.Sounds.MenuMusic);
    }

    public override void Update(float dt)
    {
        if (Host.Input.Pressed(GameAction.Confirm) || Host.Input.Pressed(GameAction.Back)) Host.SetScreen(new MenuScreen(Host));
    }

    public override void Draw(SpriteBatch batch)
    {
        UiContext ui = Session.Ui;
        Localizer texts = Session.Texts;
        Rectangle screen = batch.GraphicsDevice.Viewport.Bounds;
        BattleOutcome outcome = _battle.Outcome!;
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(ui.Assets.Pixel, screen, ui.Colors.Background);

        string title = texts.Get(outcome.Victory ? "debrief.title_victory" : "debrief.title_defeat");
        ui.Text(batch, title, new Vector2(screen.Center.X, ui.S(40)), 40f, outcome.Victory ? ui.Colors.Good : ui.Colors.Bad, Align.Center);
        ui.Text(batch, texts.Get(outcome.ReasonKey), new Vector2(screen.Center.X, ui.S(100)), 18f, ui.Colors.Text, Align.Center);
        ui.Text(batch, texts.Format("debrief.turns", ("turn", outcome.Turn)), new Vector2(screen.Center.X, ui.S(130)), 15f, ui.Colors.Muted, Align.Center);

        var panel = new Rectangle(screen.Center.X - ui.S(320), ui.S(170), ui.S(640), screen.Height - ui.S(280));
        ui.Panel(batch, panel);
        float y = panel.Y + ui.S(14);
        foreach (BattleUnit unit in _battle.UnitsOf(Faction.Player).OrderBy(u => u.Id))
        {
            (string key, Color color) = unit.Status switch
            {
                UnitStatus.Dead => ("debrief.status_dead", ui.Colors.Bad),
                UnitStatus.Unconscious => ("debrief.status_unconscious", ui.Colors.Accent),
                _ when unit.Health < unit.MaxHealth => ("debrief.status_wounded", ui.Colors.Accent),
                _ => ("debrief.status_ok", ui.Colors.Good),
            };
            ui.Text(batch, UnitNames.Of(unit, texts), new Vector2(panel.X + ui.S(20), y), 16f, ui.Colors.Text);
            ui.Text(batch, texts.Format(key, ("health", Math.Max(0, unit.Health)), ("max", unit.MaxHealth)), new Vector2(panel.Right - ui.S(20), y), 16f, color, Align.Right);
            y += ui.S(28);
        }

        int kills = _battle.UnitsOf(Faction.Hostile).Count(u => !u.IsActive);
        ui.Text(batch, texts.Format("debrief.kills", ("count", kills)), new Vector2(panel.X + ui.S(20), panel.Bottom - ui.S(36)), 16f, ui.Colors.Accent);

        if (ui.Button(batch, new Rectangle(screen.Center.X - ui.S(130), screen.Height - ui.S(84), ui.S(260), ui.S(46)), texts.Get("debrief.continue")))
            Host.SetScreen(new MenuScreen(Host));
        batch.End();
    }
}
