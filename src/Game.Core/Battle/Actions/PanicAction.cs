using Game.Core.Battle.Combat;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Actions;

/// <summary>
/// What a unit that lost control does at the start of its turn (OXCE handlePanickingUnit):
/// panic — half the time it drops what it holds and flees up to 5 tiles away, otherwise it
/// freezes; berserk — up to 10 bursts (or snap shots) at the nearest enemy in sight, or at a
/// random nearby tile. Afterwards its TU drop to 0 and it regains 15 morale.
/// </summary>
internal sealed class PanicAction(BattleUnit unit) : BattleAction
{
    private const int FleeDistance = 5;
    private const int FleeTries = 20;
    private const int MaxBerserkShots = 10;
    private const int WildFireDistance = 6;

    private bool _started;
    private int _shots;

    public override bool Advance(Battle battle)
    {
        if (!unit.IsActive) return true;
        if (unit.MoraleState == MoraleState.Berserk && Rampage(battle)) return false;
        if (!_started && unit.MoraleState == MoraleState.Panicking)
        {
            _started = true;
            if (battle.Random.Percent(50) && Flee(battle)) return false;
        }

        unit.Tu = 0;
        Morale.Change(unit, battle.Rules.Battle.MoraleAfterPanic);
        unit.MoraleState = MoraleState.None;
        return true;
    }

    private bool Flee(Battle battle)
    {
        foreach (string hand in Inventory.Hands)
            if (unit.ItemInSlot(hand) is { } item) battle.DropItem(item, unit.Position);
        for (int i = 0; i < FleeTries; i++)
        {
            Position target = unit.Position.Offset(battle.Random.Next(-FleeDistance, FleeDistance), battle.Random.Next(-FleeDistance, FleeDistance));
            if (!battle.Map.InBounds(target) || target == unit.Position || battle.UnitAt(target) is not null) continue;
            if (MoveAction.Forced(battle, unit, target) is { } walk)
            {
                battle.Push(walk);
                return true;
            }
        }

        return false;
    }

    /// <summary>Queues the next wild burst; false when the unit cannot shoot any more.</summary>
    private bool Rampage(Battle battle)
    {
        if (_shots >= MaxBerserkShots) return false;
        BattleItem? weapon = Inventory.MainWeapon(unit);
        if (weapon is null || !weapon.IsFirearm || !weapon.HasAmmo) return false;
        AttackMode mode = CombatRules.AttackTu(unit, weapon.Definition, AttackMode.Auto) > 0 ? AttackMode.Auto : AttackMode.Snap;
        int cost = CombatRules.AttackTu(unit, weapon.Definition, mode);
        if (cost <= 0 || unit.Tu < cost) return false;

        BattleUnit? target = battle.Units
            .Where(u => u.IsActive && Factions.AreEnemies(unit.Faction, u.Faction) && VisionSystem.CanSee(battle, unit, u))
            .MinBy(u => unit.Position.HorizontalDistanceSquared(u.Position));
        Position aim = target?.Position ?? unit.Position.Offset(battle.Random.Next(-WildFireDistance, WildFireDistance), battle.Random.Next(-WildFireDistance, WildFireDistance));
        if (!battle.Map.InBounds(aim) || aim == unit.Position) aim = unit.Position.Step(unit.Direction);
        if (!battle.Map.InBounds(aim)) return false;

        _shots++;
        battle.Push(AttackAction.Forced(unit, weapon, mode, aim));
        return true;
    }
}
