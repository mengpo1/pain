using Game.Core.Battle.Combat;
using Game.Core.Battle.Vision;
using Game.Core.Definitions;

namespace Game.Core.Battle.Actions;

/// <summary>
/// The end of a projectile's flight or of a blow: damage is applied to the unit or the terrain
/// part hit (one step after the shot, so an interface can show the flight first).
/// </summary>
internal sealed class ImpactAction(BattleUnit attacker, BattleItem weapon, DamageDefinition damage, TraceHit hit) : BattleAction
{
    public override bool Advance(Battle battle)
    {
        battle.Emit(new ProjectileHit(weapon.Id, hit.Voxel, hit.Unit?.Id));
        if (!battle.Rules.DamageTypes.TryGetValue(damage.Type, out DamageTypeDefinition? type)) return true;

        int power = damage.Power;
        int unitDamage = DamageRules.RandomDamage(battle.Rules.Battle, type, power, battle.Random);
        switch (hit.Kind)
        {
            case HitKind.Unit when hit.Unit is { } unit:
                DamageRules.HitUnitAt(battle, attacker, unit, hit.Voxel, unitDamage, type);
                // Being hit by a direct shot reveals the shooter to the victim's side (OXCE).
                attacker.TurnsSinceSeenBy[(int)unit.Faction] = 0;
                attacker.LastSeenPositionBy[(int)unit.Faction] = attacker.Position;
                break;
            case HitKind.Floor:
                DamageRules.HitTilePart(battle, hit.Tile, TilePartSlot.Floor, power, unitDamage, type);
                break;
            case HitKind.WestWall:
                DamageRules.HitTilePart(battle, hit.Tile, TilePartSlot.WestWall, power, unitDamage, type);
                break;
            case HitKind.NorthWall:
                DamageRules.HitTilePart(battle, hit.Tile, TilePartSlot.NorthWall, power, unitDamage, type);
                break;
            case HitKind.Object:
                DamageRules.HitTilePart(battle, hit.Tile, TilePartSlot.Object, power, unitDamage, type);
                break;
        }

        battle.RefreshVision();
        return true;
    }
}
