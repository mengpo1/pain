using Microsoft.Xna.Framework.Input;

namespace TopDown.Desktop.Input;

public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Attack,
    PickupThrow,
    Interact,
    LockOn,
    LookFar,
    Restart,
    Run,
    Reload,
    Heal,
    Pause,
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    Confirm,
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
/// Action → bindings. Defaults bind both WASD and ZQSD (the same physical keys on QWERTY and
/// AZERTY keyboards; no other action uses Z or Q), plus the arrow keys.
/// </summary>
public sealed class InputBindings
{
    private readonly Dictionary<GameAction, List<InputBinding>> _bindings = new();

    public static IReadOnlyDictionary<GameAction, string[]> Defaults { get; } = new Dictionary<GameAction, string[]>
    {
        [GameAction.MoveUp] = ["W", "Z", "Up"],
        [GameAction.MoveDown] = ["S", "Down"],
        [GameAction.MoveLeft] = ["A", "Q", "Left"],
        [GameAction.MoveRight] = ["D", "Right"],
        [GameAction.Attack] = ["Mouse:Left"],
        [GameAction.PickupThrow] = ["Mouse:Right"],
        [GameAction.Interact] = ["Space"],
        [GameAction.LockOn] = ["Mouse:Middle", "E"],
        [GameAction.LookFar] = ["LeftShift", "RightShift"],
        [GameAction.Restart] = ["R"],
        [GameAction.Run] = ["LeftShift", "RightShift"],
        [GameAction.Reload] = ["R"],
        [GameAction.Heal] = ["H"],
        [GameAction.Pause] = ["Escape", "P"],
        [GameAction.MenuUp] = ["Up", "W", "Z"],
        [GameAction.MenuDown] = ["Down", "S"],
        [GameAction.MenuLeft] = ["Left", "A", "Q"],
        [GameAction.MenuRight] = ["Right", "D"],
        [GameAction.Confirm] = ["Enter", "Space"],
        [GameAction.Back] = ["Escape", "Back"],
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

    private void Set(GameAction action, IEnumerable<string> values, Action<string> warn)
    {
        var list = new List<InputBinding>();
        foreach (string value in values)
        {
            if (InputBinding.TryParse(value, out InputBinding binding)) list.Add(binding);
            else warn($"Unknown key '{value}' for action {action}.");
        }

        _bindings[action] = list;
    }
}
