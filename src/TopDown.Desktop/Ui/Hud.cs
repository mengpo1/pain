using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TopDown.Content;
using TopDown.Core.Ai;
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
    private readonly bool _survival;
    private readonly List<(string Text, float TimeLeft)> _messages = new();
    private readonly Queue<(string Text, float Duration)> _transmissions = new();
    private (string Text, float Duration, float Elapsed)? _transmission;
    private float _comboPulse;
    private int _lastCombo;

    public Hud(UiContext ui, InputBindings bindings, bool survival = false)
    {
        _ui = ui;
        _bindings = bindings;
        _survival = survival;
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

    public void Clear()
    {
        _messages.Clear();
        _transmissions.Clear();
        _transmission = null;
    }

    /// <summary>A message from the observer: typed out in the radio panel, one at a time.</summary>
    public void ShowTransmission(string text, float duration)
    {
        if (_transmission is null) _transmission = (text, MathF.Max(1f, duration), 0f);
        else _transmissions.Enqueue((text, MathF.Max(1f, duration)));
        // A backlog would make messages arrive late: shorten the one on screen.
        if (_transmissions.Count > 1 && _transmission is { } current)
            _transmission = (current.Text, MathF.Min(current.Duration, current.Elapsed + 0.8f), current.Elapsed);
    }

    /// <summary>"+12 9 mm rounds", "+1 medkit", "Radio battery"…</summary>
    public void ShowLoot(IReadOnlyList<LootEntry> loot)
    {
        foreach (LootEntry entry in loot)
        {
            if (entry.Ammo is { } type && entry.Amount > 0)
                ShowMessage(T.Format("hud.loot.ammo", ("count", entry.Amount), ("ammo", AmmoName(type))), 2.5f);
            if (entry.Medkits > 0) ShowMessage(T.Format("hud.loot.medkits", ("count", entry.Medkits)), 2.5f);
            if (entry.Weapon is not null) ShowMessage(T.Get("hud.loot.weapon"), 2.5f);
            if (entry.Item is { } item) ShowMessage(T.Format("hud.collected", ("item", entry.NameKey is { } key ? T.Get(key) : item)), 3f);
        }
    }

    private string AmmoName(string type) => T.Has($"ammo.{type}") ? T.Get($"ammo.{type}") : type;

    public void Update(float dt, GameWorld world)
    {
        if (_transmission is { } current)
        {
            float elapsed = current.Elapsed + dt;
            _transmission = elapsed < current.Duration + TypingTime(current.Text) ? (current.Text, current.Duration, elapsed)
                : _transmissions.TryDequeue(out var next) ? (next.Text, next.Duration, 0f) : null;
        }

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
        if (Surveillance)
        {
            DrawSurveillanceFrame(batch, screen, time, levelName, margin);
            DrawTelemetry(batch, world, screen, time, margin);
        }

        if (world.Objectives.Current is { } objective && world.Outcome == WorldOutcome.Playing)
        {
            string text = T.Get(objective.TextKey);
            if (objective.Type == ObjectiveType.Survive)
                text += $"  {MathF.Max(0f, objective.Duration - world.Objectives.Elapsed):0}s";
            float y = Surveillance ? margin + 34f * _ui.Scale : margin;
            _ui.Text(batch, text, new Vector2(margin, y), 22f, C.Text);
        }

        DrawWeapon(batch, world, screen, margin);
        if (_survival) DrawVitals(batch, world, screen, margin);
        if (world.Score.Enabled) DrawScore(batch, world, screen, margin);
        DrawPrompt(batch, world, screen);
        if (world.Outcome == WorldOutcome.Playing) DrawTransmission(batch, screen, time);

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
            if (weapon.Definition.AmmoType is { } type) label += $"  +{world.Inventory.AmmoOf(type)}";
            if (player.IsReloading) label += "  " + T.Get("hud.reloading");
            else if (weapon.IsEmpty) label += "  " + T.Get("hud.empty");
        }
        else
        {
            label = T.Get(world.Combat.NaturalWeapon(player).NameKey);
        }

        float y = screen.Height - margin - 34f * _ui.Scale;
        Color color = player.Weapon is { IsEmpty: true } && !player.IsReloading ? C.Danger : C.Secondary;
        _ui.Text(batch, label, new Vector2(margin, y), 28f, color);
    }

    /// <summary>Health and stamina bars, medkits and mission items, above the weapon.</summary>
    private void DrawVitals(SpriteBatch batch, GameWorld world, Rectangle screen, float margin)
    {
        Actor player = world.Player;
        Texture2D pixel = _ui.Assets.Pixel;
        float scale = _ui.Scale;
        float width = 190f * scale;
        float y = screen.Height - margin - 58f * scale;

        float health = Math.Clamp(player.Health / MathF.Max(1f, player.Definition.Health), 0f, 1f);
        bool low = health < 0.35f;
        Primitives.FillRect(batch, pixel, new Vector2(margin, y), new Vector2(width, 9f * scale), C.Shadow * 0.7f);
        Primitives.FillRect(batch, pixel, new Vector2(margin, y), new Vector2(width * health, 9f * scale), low ? C.Danger : C.Text);
        _ui.Text(batch, T.Format("hud.medkits", ("count", world.Inventory.Medkits)), new Vector2(margin + width + 12f * scale, y - 7f * scale), 18f, C.Secondary);

        if (player.Definition.Stamina is { } stamina && player.Stamina < stamina.Max)
        {
            float fraction = Math.Clamp(player.Stamina / stamina.Max, 0f, 1f);
            Color color = player.Exhausted ? C.Danger * 0.7f : C.Secondary * 0.8f;
            Primitives.FillRect(batch, pixel, new Vector2(margin, y + 12f * scale), new Vector2(width * fraction, 3f * scale), color);
        }

        float itemY = y - 30f * scale;
        foreach ((string id, string? nameKey) in world.Inventory.Items)
        {
            string name = nameKey is not null ? T.Get(nameKey) : id;
            Primitives.Rect(batch, pixel, new Vector2(margin, itemY + 5f * scale), new Vector2(10f, 10f) * scale, C.Accent, 2f);
            _ui.Text(batch, name, new Vector2(margin + 18f * scale, itemY), 18f, C.Accent);
            itemY -= 24f * scale;
        }
    }

    /// <summary>The observer's message, typed out letter by letter under a blinking radio label.</summary>
    private void DrawTransmission(SpriteBatch batch, Rectangle screen, float time)
    {
        if (_transmission is not { } current) return;
        float scale = _ui.Scale;
        float total = current.Duration + TypingTime(current.Text);
        float fade = Math.Clamp(MathF.Min(current.Elapsed * 6f, (total - current.Elapsed) * 3f), 0f, 1f);
        int letters = Math.Min(current.Text.Length, (int)(current.Elapsed * LettersPerSecond));
        string shown = current.Text[..letters];

        float width = MathF.Min(screen.Width * 0.62f, 760f * scale);
        var origin = new Vector2((screen.Width - width) * 0.5f, screen.Height * 0.12f);
        Primitives.FillRect(batch, _ui.Assets.Pixel, origin, new Vector2(width, 66f * scale), C.Panel * fade);
        Primitives.Rect(batch, _ui.Assets.Pixel, origin, new Vector2(width, 66f * scale), C.Secondary * (0.6f * fade), 1f);
        bool blink = (int)(time * 3f) % 2 == 0;
        string header = (blink ? "((o)) " : "( o ) ") + T.Get("hud.transmission");
        _ui.Text(batch, header, origin + new Vector2(12f, 6f) * scale, 16f, C.Secondary * fade, shadow: 0f);
        _ui.Text(batch, shown, origin + new Vector2(12f, 28f) * scale, 22f, C.Text * fade, shadow: 0f);
    }

    private const float LettersPerSecond = 60f;

    private static float TypingTime(string text) => text.Length / LettersPerSecond;

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
            int left = Math.Max(1, player.ExecutionStrikesNeeded) - player.ExecutionStrikesDone;
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
        else if (_survival && world.Inventory.Medkits > 0 && player.Health < player.Definition.Health * 0.5f)
            prompt = $"[{KeyLabel(GameAction.Heal)}] {T.Get("prompt.heal")}";

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

    /// <summary>Grid position of the tracked subject and the strength of the camera's uplink (under the clock).</summary>
    private void DrawTelemetry(SpriteBatch batch, GameWorld world, Rectangle screen, float time, float margin)
    {
        System.Numerics.Vector2 p = world.Player.Position;
        string grid = T.Format("hud.grid", ("x", $"{p.X / 10f:0000.0}"), ("y", $"{p.Y / 10f:0000.0}"));
        float y = margin + 26f * _ui.Scale;
        _ui.Text(batch, grid, new Vector2(screen.Width - margin, y), 16f, C.Text * 0.75f, Align.Right, 0f);

        // Uplink: four bars wavering with a slow noise, weaker while a transmission is coming in.
        float wave = 0.5f + 0.5f * MathF.Sin(time * 0.7f) * MathF.Sin(time * 1.9f + 1f);
        int bars = Math.Clamp((int)MathF.Round(2.2f + wave * 2f - (_transmission is not null ? 1f : 0f)), 1, 4);
        float bar = 5f * _ui.Scale, gap = 3f * _ui.Scale, height = 16f * _ui.Scale;
        float x0 = screen.Width - margin - 4 * (bar + gap);
        float baseY = y + 26f * _ui.Scale + height;
        for (int i = 0; i < 4; i++)
        {
            float h = height * (i + 1) / 4f;
            Color color = i < bars ? C.Text * 0.85f : C.Text * 0.2f;
            Primitives.FillRect(batch, _ui.Assets.Pixel, new Vector2(x0 + i * (bar + gap), baseY - h), new Vector2(bar, h), color);
        }
        _ui.Text(batch, T.Get("hud.uplink"), new Vector2(x0 - 8f * _ui.Scale, baseY - 18f * _ui.Scale), 15f, C.Text * 0.7f, Align.Right, 0f);
    }

    /// <summary>
    /// Detection markers of the surveillance feed: brackets on the creatures hunting the subject ("?" on the
    /// merely suspicious ones), and a notch on the edge of the image for hunters out of frame.
    /// </summary>
    public void DrawMarkers(SpriteBatch batch, GameWorld world, Camera2D camera, Rectangle screen, Func<Actor, bool> isVisible, float time)
    {
        if (!Surveillance || world.Outcome != WorldOutcome.Playing) return;
        Texture2D pixel = _ui.Assets.Pixel;
        Actor player = world.Player;
        foreach (Actor actor in world.Actors)
        {
            if (!actor.IsAlive || actor.Brain is not { } brain || !actor.IsHostileTo(player)) continue;
            bool hunting = brain.State is AiState.Chase or AiState.Awakening;
            bool wary = brain.State is AiState.Suspicious or AiState.Investigate or AiState.Search;
            if (!hunting && !wary) continue;

            Vector2 at = camera.WorldToScreen(WorldRenderer.ToXna(actor.Position));
            bool onScreen = screen.Contains((int)at.X, (int)at.Y);
            Color color = (hunting ? C.Text : C.Text * 0.55f) * (hunting && (int)(time * 4f) % 2 == 0 ? 1f : 0.75f);
            if (onScreen && isVisible(actor))
            {
                float r = (actor.Radius * camera.Zoom) + 8f * _ui.Scale, tick = 7f * _ui.Scale;
                foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                {
                    var corner = at + new Vector2(sx * r, sy * r);
                    Primitives.Line(batch, pixel, corner, corner - new Vector2(sx * tick, 0f), color, 2f);
                    Primitives.Line(batch, pixel, corner, corner - new Vector2(0f, sy * tick), color, 2f);
                }
                if (wary) _ui.Text(batch, "?", at + new Vector2(r + 4f, -r - 6f), 16f, color, shadow: 0f);
            }
            else if (hunting && !onScreen)
            {
                // A notch on the frame edge, pointing towards the hunter.
                Vector2 center = new(screen.Width * 0.5f, screen.Height * 0.5f);
                Vector2 dir = at - center;
                if (dir.LengthSquared() < 1f) continue;
                dir.Normalize();
                float scale = MathF.Min((screen.Width * 0.5f - 40f) / MathF.Max(0.001f, MathF.Abs(dir.X)), (screen.Height * 0.5f - 40f) / MathF.Max(0.001f, MathF.Abs(dir.Y)));
                Vector2 tip = center + dir * scale;
                Vector2 side = new(-dir.Y, dir.X);
                float s = 9f * _ui.Scale;
                Primitives.Line(batch, pixel, tip, tip - dir * s + side * s * 0.6f, color, 2f);
                Primitives.Line(batch, pixel, tip, tip - dir * s - side * s * 0.6f, color, 2f);
            }
        }
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
