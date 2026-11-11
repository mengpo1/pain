using Game.Core.Battle.Combat;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Actions;

/// <summary>
/// One attack: a single shot, a burst, or a blow. TU are paid once at the start (OXCE pays a
/// burst once); each projectile spends one round and gets its own deviation roll; each impact
/// is resolved before the next shot. After the last shot, the enemy may fire back.
/// </summary>
internal sealed class AttackAction : BattleAction
{
    private readonly BattleUnit _attacker;
    private readonly BattleItem _weapon;
    private readonly AttackMode _mode;
    private readonly Position _target;
    private readonly bool _reaction;
    private bool _started;
    private int _shotsLeft;

    private AttackAction(BattleUnit attacker, BattleItem weapon, AttackMode mode, Position target, bool reaction)
    {
        _attacker = attacker;
        _weapon = weapon;
        _mode = mode;
        _target = target;
        _reaction = reaction;
    }

    public static (CommandResult, BattleAction?) Create(Battle battle, BattleUnit unit, AttackCommand command)
    {
        BattleItem? weapon = battle.GetItem(command.WeaponId);
        CommandResult check = Validate(battle, unit, weapon, command.Mode, command.Target, out int turnCost);
        if (!check.Accepted) return (check, null);

        if (unit.Faction == Faction.Player && command.Mode != AttackMode.Melee)
        {
            // OXCE refuses, at no cost, a first shot whose straight line hits something else first.
            Voxel origin = Ballistics.Origin(battle, unit, command.Target);
            Voxel? aim = Ballistics.AimPoint(battle, unit, origin, command.Target);
            if (aim is null || !Ballistics.HasLineOfFire(battle, unit, origin, aim.Value, command.Target))
                return (CommandResult.Refused("error.no_line_of_fire"), null);
        }

        _ = turnCost;
        return (CommandResult.Ok, new AttackAction(unit, weapon!, command.Mode, command.Target, reaction: false));
    }

    /// <summary>A reaction snap shot (or blow), queued by <see cref="ReactionFire"/>; TU are checked again when it fires.</summary>
    public static AttackAction Reaction(BattleUnit reactor, BattleItem weapon, AttackMode mode, Position target) =>
        new(reactor, weapon, mode, target, reaction: true);

    /// <summary>Checks an attack can be made now: weapon in hand, mode, ammo, range, adjacency, TU (turning included).</summary>
    public static CommandResult Validate(Battle battle, BattleUnit unit, BattleItem? weapon, AttackMode mode, Position target, out int turnCost)
    {
        turnCost = 0;
        if (weapon is null || weapon.Owner != unit || !Inventory.Hands.Contains(weapon.Slot)) return CommandResult.Refused("error.not_in_hand");
        if (target == unit.Position) return CommandResult.Refused("error.invalid_target");
        int cost = CombatRules.AttackTu(unit, weapon.Definition, mode);
        if (cost <= 0) return CommandResult.Refused("error.mode_unavailable");

        if (mode == AttackMode.Melee)
        {
            if (Math.Abs(target.X - unit.Position.X) > 1 || Math.Abs(target.Y - unit.Position.Y) > 1 || Math.Abs(target.Z - unit.Position.Z) > 1)
                return CommandResult.Refused("error.out_of_reach");
        }
        else
        {
            if (!weapon.HasAmmo) return CommandResult.Refused("error.no_ammo");
            if (!CombatRules.InRange(weapon.Definition, unit.Position, target)) return CommandResult.Refused("error.out_of_range");
        }

        turnCost = Directions.TurnSteps(unit.Direction, Directions.Towards(unit.Position, target)) * battle.Rules.Battle.TurnTu;
        if (unit.Tu < cost + turnCost) return CommandResult.Refused("error.not_enough_tu", cost + turnCost);
        return CommandResult.Ok;
    }

    public override bool Advance(Battle battle)
    {
        if (!_started) return Start(battle);
        if (_shotsLeft <= 0 || !_attacker.IsActive || (_mode != AttackMode.Melee && !_weapon.HasAmmo))
        {
            // OXCE checks reaction fire after the last projectile of a sequence; reactions to a
            // blow have no chance by default.
            if (!_reaction && _mode != AttackMode.Melee) ReactionFire.Check(battle, _attacker);
            return true;
        }

        _shotsLeft--;
        if (_mode == AttackMode.Melee) Strike(battle);
        else Fire(battle);
        return false;
    }

    private bool Start(Battle battle)
    {
        _started = true;
        if (!_attacker.IsActive || _weapon.Owner != _attacker) return true;
        if (_reaction)
        {
            // The target may have died or moved away from the tile before this reaction fires.
            if (battle.UnitAt(_target) is not { IsActive: true }) return true;
            int reactionCost = CombatRules.AttackTu(_attacker, _weapon.Definition, _mode);
            if (_attacker.Tu < reactionCost || (_mode != AttackMode.Melee && !_weapon.HasAmmo)) return true;
            TurnRules.Face(battle, _attacker, Directions.Towards(_attacker.Position, _target));
            _attacker.Tu -= reactionCost;
        }
        else
        {
            CommandResult check = Validate(battle, _attacker, _weapon, _mode, _target, out int turnCost);
            if (!check.Accepted) return true;
            _attacker.Tu -= turnCost + CombatRules.AttackTu(_attacker, _weapon.Definition, _mode);
            TurnRules.Face(battle, _attacker, Directions.Towards(_attacker.Position, _target));
        }

        battle.RefreshVision();
        _shotsLeft = _weapon.Definition.Mode(_mode)?.Shots ?? 1;
        return false;
    }

    private void Fire(Battle battle)
    {
        BattleItem source = _weapon.AmmoSource!;
        if (source.Definition.Rounds > 0 || !_weapon.SelfPowered) source.Rounds--;

        Voxel origin = Ballistics.Origin(battle, _attacker, _target);
        Voxel aim = Ballistics.AimPoint(battle, _attacker, origin, _target) ?? Voxel.TileCenter(_target, Voxel.TileHeight / 2);
        int accuracy = CombatRules.Accuracy(battle, _attacker, _weapon, _mode);
        double fraction = CombatRules.AccuracyAtDistance(battle.Rules.Battle, _weapon.Definition, _mode, accuracy, origin.DistanceTo(aim));
        Voxel deviated = Ballistics.Deviate(origin, aim, fraction, battle.Random);
        TraceHit hit = Ballistics.Fly(battle, _attacker, origin, deviated);

        battle.Emit(new ShotFired(_attacker.Id, _weapon.Id, _mode, origin, hit.Voxel, _reaction));
        battle.Push(new ImpactAction(_attacker, _weapon, source.Definition.Damage!, hit));
    }

    /// <summary>
    /// Melee is a real percentage roll (OXCE): accuracy − RNG(0..99) &gt; 0 (dodge not modelled yet).
    /// </summary>
    private void Strike(Battle battle)
    {
        BattleUnit? victim = battle.UnitAt(_target);
        DamageDefinition damage = _weapon.Definition.Damage!;
        int accuracy = CombatRules.Accuracy(battle, _attacker, _weapon, AttackMode.Melee);
        bool hits = victim is not null && accuracy - battle.Random.Next(0, 99) > 0;
        Voxel impact = victim is not null
            ? new Voxel(_target.X * Voxel.TileWidth + Voxel.TileWidth / 2, _target.Y * Voxel.TileWidth + Voxel.TileWidth / 2, LineTracer.UnitBottom(battle, victim) + victim.Height / 2)
            : Voxel.TileCenter(_target, Voxel.TileHeight / 2);
        battle.Emit(new ShotFired(_attacker.Id, _weapon.Id, AttackMode.Melee, Ballistics.Origin(battle, _attacker, _target), impact, _reaction));
        var hit = new TraceHit(hits ? HitKind.Unit : HitKind.Nothing, impact, _target, hits ? victim : null);
        battle.Push(new ImpactAction(_attacker, _weapon, damage, hit));
    }
}
