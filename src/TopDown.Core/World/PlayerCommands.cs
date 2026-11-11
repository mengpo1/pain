using System.Numerics;

namespace TopDown.Core.World;

/// <summary>
/// What the player wants to do during one simulation step, independent of any input device.
/// "Pressed" flags are true only for the step that consumes them.
/// </summary>
public struct PlayerCommands
{
    /// <summary>Movement direction; longer than 1 is clamped (diagonals are normalised).</summary>
    public Vector2 Move;

    /// <summary>World position the character looks at.</summary>
    public Vector2 AimTarget;

    public bool AttackPressed;
    public bool AttackHeld;
    public bool PickupOrThrowPressed;
    public bool InteractPressed;

    /// <summary>Held: run (when the character has stamina).</summary>
    public bool Run;
    public bool ReloadPressed;
    public bool HealPressed;
}
