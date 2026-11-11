using Game.Core.Battle.Actions;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Reaction fire (OXCE TileEngine::checkReactionFire): when a unit of the side playing walks a
/// step, kneels or stands, or finishes firing, enemies who see it may interrupt with snap shots
/// (or a blow if it stands right in front of them). A reactor fires while its reaction score —
/// reactions × TU left / max TU — is at least the actor's; each shot lowers that score by
/// cost × reactions / max TU, up to 10 times per trigger. Ties go to the reactor.
/// </summary>
public static class ReactionFire
{
    /// <summary>Queues the reactions triggered by <paramref name="actor"/>. Returns true if any.</summary>
    public static bool Check(Battle battle, BattleUnit actor)
    {
        if (!actor.IsActive || actor.Faction != battle.CurrentFaction || battle.CurrentFaction == Faction.Neutral) return false;
        double actorScore = CombatRules.ReactionScore(actor);

        var candidates = new List<Candidate>();
        foreach (BattleUnit reactor in battle.Units)
        {
            if (!CanReact(battle, reactor, actor)) continue;
            (BattleItem weapon, AttackMode mode)? reaction = ReactionWeapon(battle, reactor, actor);
            if (reaction is null) continue;
            candidates.Add(new Candidate(reactor, reaction.Value.weapon, reaction.Value.mode, CombatRules.ReactionScore(reactor)));
        }

        var queued = new List<AttackAction>();
        var tuReserved = new Dictionary<BattleUnit, int>();
        while (candidates.Count > 0)
        {
            Candidate best = candidates.MaxBy(c => c.Score)!;
            if (best.Score < actorScore) break;

            int cost = CombatRules.AttackTu(best.Reactor, best.Weapon.Definition, best.Mode);
            int reserved = tuReserved.GetValueOrDefault(best.Reactor);
            if (best.Reactor.Tu - reserved < cost || best.Count >= battle.Rules.Battle.MaxReactionsPerTrigger)
            {
                candidates.Remove(best);
                continue;
            }

            tuReserved[best.Reactor] = reserved + cost;
            queued.Add(AttackAction.Reaction(best.Reactor, best.Weapon, best.Mode, actor.Position));
            battle.Emit(new ReactionTriggered(best.Reactor.Id, actor.Id));
            best.Score -= cost * (double)best.Reactor.Stats[StatIds.Reactions] / best.Reactor.MaxTu;
            best.Count++;
        }

        // The stack runs the last pushed first: push in reverse so the best reactor fires first.
        for (int i = queued.Count - 1; i >= 0; i--) battle.Push(queued[i]);
        return queued.Count > 0;
    }

    private static bool CanReact(Battle battle, BattleUnit reactor, BattleUnit actor)
    {
        if (!reactor.IsActive || reactor.Faction == battle.CurrentFaction || !Factions.AreEnemies(reactor.Faction, actor.Faction)) return false;
        int range = battle.Rules.Battle.MaxViewDistance;
        if (reactor.Position.HorizontalDistanceSquared(actor.Position) > range * range) return false;
        bool facing = VisionSystem.InViewSector(reactor.Direction, reactor.Position, actor.Position);
        if (!facing && !reactor.HitByThisTurn.Contains(actor)) return false;
        return VisionSystem.CanSee(battle, reactor, actor);
    }

    /// <summary>
    /// A blow if the actor is adjacent in the direction the reactor faces, otherwise a snap shot
    /// with a loaded firearm in range. Never aimed or auto fire.
    /// </summary>
    private static (BattleItem, AttackMode)? ReactionWeapon(Battle battle, BattleUnit reactor, BattleUnit actor)
    {
        foreach (string hand in Inventory.Hands)
        {
            BattleItem? item = reactor.ItemInSlot(hand);
            if (item is null) continue;
            bool adjacentInFront = reactor.Position.Step(reactor.Direction) == actor.Position with { Z = reactor.Position.Z } && Math.Abs(actor.Position.Z - reactor.Position.Z) <= 1;
            if (item.Definition.Melee is not null && adjacentInFront && CombatRules.AttackTu(reactor, item.Definition, AttackMode.Melee) <= reactor.Tu)
                return (item, AttackMode.Melee);
        }

        foreach (string hand in Inventory.Hands)
        {
            BattleItem? item = reactor.ItemInSlot(hand);
            if (item is null || !item.IsFirearm || !item.HasAmmo) continue;
            int cost = CombatRules.AttackTu(reactor, item.Definition, AttackMode.Snap);
            if (cost <= 0 || cost > reactor.Tu || !CombatRules.InRange(item.Definition, reactor.Position, actor.Position)) continue;
            return (item, AttackMode.Snap);
        }

        return null;
    }

    private sealed class Candidate(BattleUnit reactor, BattleItem weapon, AttackMode mode, double score)
    {
        public BattleUnit Reactor { get; } = reactor;
        public BattleItem Weapon { get; } = weapon;
        public AttackMode Mode { get; } = mode;
        public double Score { get; set; } = score;
        public int Count { get; set; }
    }
}
