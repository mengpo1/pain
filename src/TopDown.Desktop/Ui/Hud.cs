using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using TopDown.Desktop.Graphics;
using TopDown.Desktop.Input;

namespace TopDown.Desktop.Ui;

/// <summary>
/// In-game information: objective, weapon and ammo, score and combo, context prompts and
/// timed messages. Two styles: "default" and "surveillance" (camera-feed overlay).
/// </summary>
public sealed class Hud
{
    private readonly UiContext _ui;
    private readonly InputBindings _bindings;
    private readonly List<(string Text, float TimeLeft)> _messages = new();
    private float _comboPulse;
    private int _lastCombo;

    public Hud(UiContext ui, InputBindings bindings)
    {
        _ui = ui;
        _bindings = bindings;
    }

    private Localizer T => _ui.Texts;
    private ThemeColors C => _ui.Colors;
    private bool Surveillance => _ui.Theme.Ui.HudStyle == "surveillance";

    public void ShowMessage(string text, float duration = 3f)
    {
        _messages.RemoveAll(m => m.Text == text);
        _messages.Add((text, duration));
        if (_messages.Count > 4) _messages.RemoveAt(0);
    }

    public void Clear() => _messages.Clear();

    public void Update(float dt, GameWorld world)
    {
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            (string text, float left) = _messages[i];
            if (left - dt <= 0f) _messages.RemoveAt(i);
            else _messages[i] = (text, left - dt);
        }

        if (world.Score.Combo > _lastCombo) _comboPulse = 1f;
        _lastCombo = world.Score.Combo;
        _comboPulse = MathF.Max(0f, _comboPulse - dt * 3f);
    }

    public void Draw(SpriteBatch batch, GameWorld world, Rectangle screen, float time, string levelName)
    {
        float margin = 24f * _ui.Scale;
        if (Surveillance) DrawSurveillanceFrame(batch, screen, time, levelName, margin);

        if (world.Objectives.Current is { } objective && world.Outcome == WorldOutcome.Playing)
        {
            string text = T.Get(objective.TextKey);
            if (objective.Type == ObjectiveType.Survive)
                text += $"  {MathF.Max(0f, objective.Duration - world.Objectives.Elapsed):0}s";
            float y = Surveillance ? margin + 34f * _ui.Scale : margin;
            _ui.Text(batch, text, new Vector2(margin, y), 22f, C.Text);
        }

        DrawWeapon(batch, world, screen, margin);
        if (world.Score.Enabled) DrawScore(batch, world, screen, margin);
        DrawPrompt(batch, world, screen);

        float messageY = screen.Height * 0.2f;
        foreach ((string text, float left) in _messages)
        {
            float alpha = Math.Clamp(left * 2f, 0f, 1f);
            Vector2 size = _ui.Text(batch, text, new Vector2(screen.Width * 0.5f, messageY), 26f, C.Text * alpha, Align.Center);
            messageY += size.Y + 6f;
        }
    }

    private void DrawWeapon(SpriteBatch batch, GameWorld world, Rectangle screen, float margin)
    {
        Actor player = world.Player;
        string label;
        if (player.Weapon is { } weapon)
        {
            label = T.Get(weapon.Definition.NameKey);
            if (weapon.Definition.IsFirearm) label += $"  {weapon.Ammo}/{weapon.Definition.MagazineSize}";
            if (weapon.IsEmpty) label += "  " + T.Get("hud.empty");
        }
        else
        {
            label = T.Get(world.Combat.NaturalWeapon(player).NameKey);
        }

        float y = screen.Height - margin - 34f * _ui.Scale;
        Color color = player.Weapon is { IsEmpty: true } ? C.Danger : C.Secondary;
        _ui.Text(batch, label, new Vector2(margin, y), 28f, color);
    }

    private void DrawScore(SpriteBatch batch, GameWorld world, Rectangle screen, float margin)
    {
        float right = screen.Width - margin;
        _ui.Text(batch, T.Format("hud.score", ("points", world.Score.Total)), new Vector2(right, margin), 24f, C.Text, Align.Right);
        if (world.Score.Combo < 2) return;
        float size = 30f + _comboPulse * 12f;
        _ui.Text(batch, T.Format("hud.combo", ("count", world.Score.Combo)), new Vector2(right, margin + 32f * _ui.Scale), size, C.Accent, Align.Right);
        float fraction = world.Score.ComboTimeLeft / MathF.Max(0.01f, world.Score.ComboWindow);
        float width = 140f * _ui.Scale;
        float barY = margin + 32f * _ui.Scale + size * _ui.Scale + 8f;
        Primitives.FillRect(batch, _ui.Assets.Pixel, new Vector2(right - width, barY), new Vector2(width, 4f), C.Shadow * 0.6f);
        Primitives.FillRect(batch, _ui.Assets.Pixel, new Vector2(right - width * fraction, barY), new Vector2(width * fraction, 4f), C.Accent);
    }

    /// <summary>"[Space] Finish him", "[Right click] Pick up the bat"…</summary>
    private void DrawPrompt(SpriteBatch batch, GameWorld world, Rectangle screen)
    {
        Actor player = world.Player;
        if (world.Outcome != WorldOutcome.Playing) return;

        // During an execution that needs blows: show how many are left.
        if (player.State == ActorState.Executing && !player.ExecutionFinished && player.ExecutionWeapon is { Execution.StrikeOnInput: true } executionWeapon)
        {
            int left = Math.Max(1, executionWeapon.Execution.Strikes) - player.ExecutionStrikesDone;
            string strike = $"[{KeyLabel(GameAction.Attack)}] {T.Format("prompt.strike", ("count", left))}";
            _ui.Text(batch, strike, new Vector2(screen.Width * 0.5f, screen.Height - 70f * _ui.Scale), 22f, C.Accent, Align.Center);
            return;
        }

        if (!player.CanAct) return;
        string? prompt = null;
        if (world.Rules.Execution.Enabled && world.Combat.FindExecutionTarget(player) is not null)
            prompt = $"[{KeyLabel(GameAction.Interact)}] {T.Get("prompt.execute")}";
        else if (world.Combat.FindInteractable(player) is { } interactable)
            prompt = $"[{KeyLabel(GameAction.Interact)}] {T.Get(interactable.Definition.PromptKey)}";
        else if (world.Combat.FindPickup(player) is { } weapon)
            prompt = $"[{KeyLabel(GameAction.PickupThrow)}] {T.Format("prompt.pickup", ("weapon", T.Get(weapon.Weapon.Definition.NameKey)))}";

        if (prompt is not null)
            _ui.Text(batch, prompt, new Vector2(screen.Width * 0.5f, screen.Height - 70f * _ui.Scale), 20f, C.Text, Align.Center);
    }

    public string KeyLabel(GameAction action)
    {
        IReadOnlyList<InputBinding> bindings = _bindings.For(action);
        if (bindings.Count == 0) return "?";
        string name = bindings[0].ToString().Replace("Mouse:", "Mouse", StringComparison.Ordinal);
        string key = $"input.{name}";
        return T.Has(key) ? T.Get(key) : name;
    }

    private void DrawSurveillanceFrame(SpriteBatch batch, Rectangle screen, float time, string levelName, float margin)
    {
        Texture2D pixel = _ui.Assets.Pixel;
        Color line = C.Text * 0.7f;
        float length = 40f * _ui.Scale;
        float thickness = 2f;
        var corners = new[]
        {
            (new Vector2(margin * 0.5f, margin * 0.5f), 1f, 1f),
            (new Vector2(screen.Width - margin * 0.5f, margin * 0.5f), -1f, 1f),
            (new Vector2(margin * 0.5f, screen.Height - margin * 0.5f), 1f, -1f),
            (new Vector2(screen.Width - margin * 0.5f, screen.Height - margin * 0.5f), -1f, -1f),
        };
        foreach ((Vector2 corner, float sx, float sy) in corners)
        {
            Primitives.Line(batch, pixel, corner, corner + new Vector2(length * sx, 0f), line, thickness);
            Primitives.Line(batch, pixel, corner, corner + new Vector2(0f, length * sy), line, thickness);
        }

        bool blink = (int)(time * 1.5f) % 2 == 0;
        if (blink) Primitives.FillRect(batch, pixel, new Vector2(margin, margin + 6f), new Vector2(12f, 12f) * _ui.Scale, C.Danger);
        _ui.Text(batch, T.Get("hud.rec"), new Vector2(margin + 20f * _ui.Scale, margin), 20f, C.Text, shadow: 0f);

        var clock = TimeSpan.FromHours(3) + TimeSpan.FromSeconds(time);
        _ui.Text(batch, $"{clock:hh\\:mm\\:ss}", new Vector2(screen.Width - margin, margin), 20f, C.Text, Align.Right, 0f);
        string label = _ui.Theme.Ui.OverlayLabelKey is { } key ? T.Format(key, ("level", levelName)) : levelName;
        _ui.Text(batch, label, new Vector2(screen.Width - margin, screen.Height - margin - 26f * _ui.Scale), 18f, C.Text * 0.8f, Align.Right, 0f);
    }

    public void DrawDeath(SpriteBatch batch, Rectangle screen, float sinceDeath)
    {
        float alpha = Math.Clamp(sinceDeath * 3f, 0f, 1f);
        batch.Draw(_ui.Assets.Pixel, screen, C.Shadow * (0.35f * alpha));
        _ui.Title(batch, T.Get("game.over"), new Vector2(screen.Width * 0.5f, screen.Height * 0.38f), 64f, C.Danger * alpha, sinceDeath);
        _ui.Text(batch, T.Format("game.restartHint", ("key", KeyLabel(GameAction.Restart))), new Vector2(screen.Width * 0.5f, screen.Height * 0.55f), 26f, C.Text * alpha, Align.Center);
    }

    public void DrawVictory(SpriteBatch batch, Rectangle screen, GameWorld world, float time, bool hasNext)
    {
        batch.Draw(_ui.Assets.Pixel, screen, C.Shadow * 0.45f);
        _ui.Title(batch, T.Get("game.levelComplete"), new Vector2(screen.Width * 0.5f, screen.Height * 0.25f), 60f, C.Accent, time);
        float y = screen.Height * 0.42f;
        var lines = new List<string>
        {
            T.Format("stats.kills", ("count", world.Score.Kills)),
            T.Format("stats.time", ("time", $"{TimeSpan.FromSeconds(world.Time):mm\\:ss}")),
        };
        if (world.Score.Enabled)
        {
            lines.Add(T.Format("stats.score", ("points", world.Score.Total)));
            lines.Add(T.Format("stats.bestCombo", ("count", world.Score.BestCombo)));
        }

        foreach (string line in lines)
        {
            _ui.Text(batch, line, new Vector2(screen.Width * 0.5f, y), 28f, C.Text, Align.Center);
            y += 38f * _ui.Scale;
        }

        string hint = T.Format(hasNext ? "game.nextHint" : "game.menuHint", ("key", KeyLabel(GameAction.Confirm)));
        _ui.Text(batch, hint, new Vector2(screen.Width * 0.5f, y + 24f * _ui.Scale), 24f, C.Secondary, Align.Center);
    }
}
