namespace Game.Core.Battle.Actions;

/// <summary>
/// Something that takes several steps to resolve (a walk, a burst of fire, a projectile in
/// flight). The battle keeps them on a stack: reaction fire is pushed on top and resolves before
/// the interrupted action continues, as in OpenXcom's state stack.
/// </summary>
internal abstract class BattleAction
{
    /// <summary>Performs one step. Returns true when the action is finished.</summary>
    public abstract bool Advance(Battle battle);
}
