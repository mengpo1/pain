using Microsoft.Xna.Framework.Input;

namespace Game.Desktop.Input;

/// <summary>Everything the player can ask with the keyboard or mouse. Bindings are configurable.</summary>
public enum GameAction
{
    ScrollUp,
    ScrollDown,
    ScrollLeft,
    ScrollRight,
    ZoomIn,
    ZoomOut,
    LevelUp,
    LevelDown,
    NextUnit,
    PreviousUnit,
    CenterOnUnit,
    Kneel,
    Reload,
    Inventory,
    EndTurn,
    Cancel,
    Confirm,
    QuickSave,
    QuickLoad,
    ToggleDebug,
    MenuUp,
    MenuDown,
    Back,
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2,
}

/// <summary>A key or a mouse button. Written in settings as "W", "Space" or "Mouse:Left".</summary>
public readonly record struct InputBinding(Keys? Key, MouseButton? Mouse)
{
    public static bool TryParse(string text, out InputBinding binding)
    {
        binding = default;
        string value = text.Trim();
        if (value.StartsWith("Mouse:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse(value["Mouse:".Length..], true, out MouseButton button)) return false;
            binding = new InputBinding(null, button);
            return true;
        }

        if (value.StartsWith("Key:", StringComparison.OrdinalIgnoreCase)) value = value["Key:".Length..];
        if (!Enum.TryParse(value, true, out Keys key)) return false;
        binding = new InputBinding(key, null);
        return true;
    }

    public override string ToString() => Key is { } key ? key.ToString() : $"Mouse:{Mouse}";
}

/// <summary>
/// Action → bindings. Scrolling defaults accept both WASD and ZQSD (the same physical keys on
/// QWERTY and AZERTY keyboards) plus the arrow keys; no other default uses those letters.
/// </summary>
public sealed class InputBindings
{
    private readonly Dictionary<GameAction, List<InputBinding>> _bindings = new();

    public static IReadOnlyDictionary<GameAction, string[]> Defaults { get; } = new Dictionary<GameAction, string[]>
    {
        [GameAction.ScrollUp] = ["Up", "W", "Z"],
        [GameAction.ScrollDown] = ["Down", "S"],
        [GameAction.ScrollLeft] = ["Left", "A", "Q"],
        [GameAction.ScrollRight] = ["Right", "D"],
        [GameAction.ZoomIn] = ["OemPlus", "Add"],
        [GameAction.ZoomOut] = ["OemMinus", "Subtract"],
        [GameAction.LevelUp] = ["PageUp"],
        [GameAction.LevelDown] = ["PageDown"],
        [GameAction.NextUnit] = ["Tab"],
        [GameAction.PreviousUnit] = ["LeftShift+Tab"],
        [GameAction.CenterOnUnit] = ["C"],
        [GameAction.Kneel] = ["K"],
        [GameAction.Reload] = ["R"],
        [GameAction.Inventory] = ["I"],
        [GameAction.EndTurn] = ["E"],
        [GameAction.Cancel] = ["Escape"],
        [GameAction.Confirm] = ["Enter", "Space"],
        [GameAction.QuickSave] = ["F5"],
        [GameAction.QuickLoad] = ["F9"],
        [GameAction.ToggleDebug] = ["F1"],
        [GameAction.MenuUp] = ["Up", "W", "Z"],
        [GameAction.MenuDown] = ["Down", "S"],
        [GameAction.Back] = ["Escape"],
    };

    /// <summary>Builds bindings from defaults, replacing the actions present in <paramref name="overrides"/>.</summary>
    public static InputBindings Create(IReadOnlyDictionary<string, List<string>>? overrides, Action<string> warn)
    {
        var bindings = new InputBindings();
        foreach ((GameAction action, string[] defaults) in Defaults) bindings.Set(action, defaults, warn);
        if (overrides is null) return bindings;
        foreach ((string name, List<string> values) in overrides)
        {
            if (Enum.TryParse(name, true, out GameAction action)) bindings.Set(action, values, warn);
            else warn($"Unknown action '{name}' in key bindings.");
        }

        return bindings;
    }

    public IReadOnlyList<InputBinding> For(GameAction action) =>
        _bindings.TryGetValue(action, out var list) ? list : [];

    /// <summary>Text shown next to buttons ("E", "Tab").</summary>
    public string Label(GameAction action) => For(action) is { Count: > 0 } list ? list[0].ToString() : "";

    private void Set(GameAction action, IEnumerable<string> values, Action<string> warn)
    {
        var list = new List<InputBinding>();
        foreach (string value in values)
        {
            // "LeftShift+Tab": a modifier is accepted in the file but only the main key is bound
            // here; the screen checks Shift itself for "previous" actions.
            string main = value.Contains('+') ? value[(value.LastIndexOf('+') + 1)..] : value;
            if (InputBinding.TryParse(main, out InputBinding binding)) list.Add(binding);
            else warn($"Unknown key '{value}' for action {action}.");
        }

        _bindings[action] = list;
    }
}
