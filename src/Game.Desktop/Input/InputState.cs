using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Desktop.Input;

/// <summary>Keyboard and mouse state of this frame and the previous one, read through bindings.</summary>
public sealed class InputState
{
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _mouse;
    private MouseState _previousMouse;
    private bool _initialized;

    public InputState(InputBindings bindings) => Bindings = bindings;

    public InputBindings Bindings { get; set; }
    public Point MousePosition => new(_mouse.X, _mouse.Y);
    public bool MouseMoved => _mouse.X != _previousMouse.X || _mouse.Y != _previousMouse.Y;
    public int ScrollDelta => Enabled ? _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue : 0;

    /// <summary>When false (window inactive), every query returns "not pressed".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Set by widgets that used this frame's click, so the map below does not also react.</summary>
    public bool ClickConsumed { get; set; }

    public bool Shift => _keyboard.IsKeyDown(Keys.LeftShift) || _keyboard.IsKeyDown(Keys.RightShift);

    public void Update()
    {
        _previousKeyboard = _keyboard;
        _previousMouse = _mouse;
        _keyboard = Keyboard.GetState();
        _mouse = Mouse.GetState();
        ClickConsumed = false;
        if (_initialized) return;
        // First frame: nothing was "just pressed" and the mouse did not "move" from (0, 0).
        _previousKeyboard = _keyboard;
        _previousMouse = _mouse;
        _initialized = true;
    }

    public bool Held(GameAction action)
    {
        if (!Enabled) return false;
        foreach (InputBinding binding in Bindings.For(action))
            if (IsDown(binding, _keyboard, _mouse)) return true;
        return false;
    }

    public bool Pressed(GameAction action)
    {
        if (!Enabled) return false;
        foreach (InputBinding binding in Bindings.For(action))
            if (IsDown(binding, _keyboard, _mouse) && !IsDown(binding, _previousKeyboard, _previousMouse)) return true;
        return false;
    }

    public bool KeyPressed(Keys key) => Enabled && _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public bool MousePressed(MouseButton button) =>
        Enabled && IsDown(new InputBinding(null, button), _keyboard, _mouse) && !IsDown(new InputBinding(null, button), _previousKeyboard, _previousMouse);

    public bool MouseHeld(MouseButton button) => Enabled && IsDown(new InputBinding(null, button), _keyboard, _mouse);

    public Point MouseDelta => new(_mouse.X - _previousMouse.X, _mouse.Y - _previousMouse.Y);

    /// <summary>A left click not already used by a widget.</summary>
    public bool LeftClicked => MousePressed(MouseButton.Left) && !ClickConsumed;

    public bool RightClicked => MousePressed(MouseButton.Right) && !ClickConsumed;

    private static bool IsDown(InputBinding binding, KeyboardState keyboard, MouseState mouse)
    {
        if (binding.Key is { } key) return keyboard.IsKeyDown(key);
        return binding.Mouse switch
        {
            MouseButton.Left => mouse.LeftButton == ButtonState.Pressed,
            MouseButton.Right => mouse.RightButton == ButtonState.Pressed,
            MouseButton.Middle => mouse.MiddleButton == ButtonState.Pressed,
            MouseButton.X1 => mouse.XButton1 == ButtonState.Pressed,
            MouseButton.X2 => mouse.XButton2 == ButtonState.Pressed,
            _ => false,
        };
    }
}
