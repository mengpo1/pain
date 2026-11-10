using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TopDown.Desktop.Input;

/// <summary>Keyboard and mouse state of this frame and the previous one, read through bindings.</summary>
public sealed class InputState
{
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _mouse;
    private MouseState _previousMouse;

    public InputState(InputBindings bindings) => Bindings = bindings;

    public InputBindings Bindings { get; set; }
    public Vector2 MousePosition => new(_mouse.X, _mouse.Y);
    public bool MouseMoved => _mouse.X != _previousMouse.X || _mouse.Y != _previousMouse.Y;
    public int ScrollDelta => _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    /// <summary>When false (window inactive), every query returns "not pressed".</summary>
    public bool Enabled { get; set; } = true;

    public void Update()
    {
        _previousKeyboard = _keyboard;
        _previousMouse = _mouse;
        _keyboard = Keyboard.GetState();
        _mouse = Mouse.GetState();
    }

    public bool Held(GameAction action)
    {
        if (!Enabled) return false;
        IReadOnlyList<InputBinding> bindings = Bindings.For(action);
        for (int i = 0; i < bindings.Count; i++)
            if (IsDown(bindings[i], _keyboard, _mouse)) return true;
        return false;
    }

    public bool Pressed(GameAction action)
    {
        if (!Enabled) return false;
        IReadOnlyList<InputBinding> bindings = Bindings.For(action);
        for (int i = 0; i < bindings.Count; i++)
            if (IsDown(bindings[i], _keyboard, _mouse) && !IsDown(bindings[i], _previousKeyboard, _previousMouse)) return true;
        return false;
    }

    public bool KeyPressed(Keys key) => Enabled && _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public bool LeftClicked => Enabled && _mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;

    /// <summary>Movement from the four move actions; diagonals are normalised by the simulation.</summary>
    public Vector2 MoveVector()
    {
        var move = Vector2.Zero;
        if (Held(GameAction.MoveUp)) move.Y -= 1f;
        if (Held(GameAction.MoveDown)) move.Y += 1f;
        if (Held(GameAction.MoveLeft)) move.X -= 1f;
        if (Held(GameAction.MoveRight)) move.X += 1f;
        return move;
    }

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
