using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Damage to units and terrain, following OXCE BattleUnit::damage, TileEngine::hit and
/// checkForCasualties (docs/mechanics.md). Randomisation happens once per hit, then the armour
/// of the side hit is subtracted and what is left is converted into health, stun, wounds and
/// armour wear by the damage type's factors.
/// </summary>
public static class DamageRules
{
    /// <summary>Highest stun a unit can accumulate, in multiples of its maximum health.</summary>
    private const int StunCapFactor = 4;

    public static int RandomDamage(BattleRules rules, DamageTypeDefinition type, int power, GameRandom random)
    {
        return type.Random switch
        {
            DamageRandom.Ufo => random.Next(0, power * 2),
            DamageRandom.Tftd => random.Next(power * 50 / 100, power * 150 / 100),
            DamageRandom.Flat => power,
            DamageRandom.Fire => random.Next(rules.FireDamageMin, rules.FireDamageMax),
            DamageRandom.None => 0,
            DamageRandom.Explosion => Spread(power, rules.ExplosiveDamageRangePercent, random),
            _ => Spread(power, rules.DamageRangePercent, random),
        };
    }

    private static int Spread(int power, int percent, GameRandom random) =>
        random.Next(power * (100 - percent) / 100, power * (100 + percent) / 100);

    /// <summary>
    /// Side of the armour hit, from where the impact is relative to the unit (OXCE: the octant of
    /// the impact, compared with the unit's facing; oblique hits fall on the front or rear two times
    /// out of three). An impact straight below the unit's centre hits the underside.
    /// </summary>
    public static ArmorSide SideHit(int rx, int ry, int rz, int facing, GameRandom random)
    {
        if (rx == 0 && ry == 0 && rz <= 0) return ArmorSide.Under;
        int octant;
        if (Math.Abs(ry) > 2 * Math.Abs(rx)) octant = ry > 0 ? 4 : 0;
        else if (Math.Abs(rx) > 2 * Math.Abs(ry)) octant = rx < 0 ? 6 : 2;
        else if (rx < 0) octant = ry > 0 ? 5 : 7;
        else octant = ry > 0 ? 3 : 1;

        int difference = (octant + 8 - facing) % 8;
        bool mostly = random.Next(0, 2) < 2;
        return difference switch
        {
            0 => ArmorSide.Front,
            1 => mostly ? ArmorSide.Front : ArmorSide.Right,
            2 => ArmorSide.Right,
            3 => mostly ? ArmorSide.Rear : ArmorSide.Right,
            4 => ArmorSide.Rear,
            5 => mostly ? ArmorSide.Rear : ArmorSide.Left,
            6 => ArmorSide.Left,
            _ => mostly ? ArmorSide.Front : ArmorSide.Left,
        };
    }

    /// <summary>Body part hit: the head above the unit's height, arms (or torso) above 4 voxels, legs below.</summary>
    public static BodyPart PartHit(ArmorSide side, int rz, int height, GameRandom random)
    {
        if (side == ArmorSide.Under) return BodyPart.Torso;
        if (rz >= height) return BodyPart.Head;
        if (rz > 4)
            return side switch { ArmorSide.Left => BodyPart.LeftArm, ArmorSide.Right => BodyPart.RightArm, _ => BodyPart.Torso };
        return side switch
        {
            ArmorSide.Left => BodyPart.LeftLeg,
            ArmorSide.Right => BodyPart.RightLeg,
            _ => random.Next(0, 1) == 0 ? BodyPart.LeftLeg : BodyPart.RightLeg,
        };
    }

    /// <summary>
    /// A projectile or blow reaching a unit at <paramref name="impact"/> (voxel). Returns the
    /// health lost.
    /// </summary>
    public static int HitUnitAt(Battle battle, BattleUnit? attacker, BattleUnit target, Voxel impact, int damage, DamageTypeDefinition type)
    {
        Position p = target.Position;
        int rx = impact.X - (p.X * Voxel.TileWidth + Voxel.TileWidth / 2);
        int ry = impact.Y - (p.Y * Voxel.TileWidth + Voxel.TileWidth / 2);
        int rz = impact.Z - (p.Z * Voxel.TileHeight + battle.Map.StandElevation(p));
        return Damage(battle, attacker, target, rx, ry, rz, damage, type);
    }

    /// <summary>OXCE BattleUnit::damage with an already randomised damage value.</summary>
    public static int Damage(Battle battle, BattleUnit? attacker, BattleUnit target, int rx, int ry, int rz, int damage, DamageTypeDefinition type)
    {
        if (target.Health <= 0 || target.Status == UnitStatus.Dead) return 0;
        GameRandom random = battle.Random;
        if (attacker is not null && attacker != target) target.HitByThisTurn.Add(attacker);

        int modifier = target.Armor.DamageModifiers.TryGetValue(type.Id, out int m) ? m : 100;
        damage = damage * modifier / 100;

        ArmorSide side;
        BodyPart part;
        if (type.IgnoreDirection)
        {
            side = ArmorSide.Front;
            part = BodyPart.Torso;
        }
        else
        {
            side = SideHit(rx, ry, rz, target.Direction, random);
            part = PartHit(side, rz, target.Height, random);
        }

        int healthLoss = 0, stun = 0, wounds = 0;
        if (damage > 0)
        {
            int through = (int)(damage - target.ArmorValue(side) * type.ArmorEffectiveness);
            if (through > 0)
            {
                stun = Convert(type.RandomStun, type.ToStun, through, random);
                healthLoss = Convert(false, type.ToHealth, through, random);
                wounds = Wounds(type, through, random);
                int armorLoss = Convert(false, type.ToArmor, through, random);

                target.Stun = Math.Clamp(target.Stun + stun, 0, StunCapFactor * target.MaxHealth);
                target.Health = Math.Clamp(target.Health - healthLoss, Math.Min(0, target.Health), target.MaxHealth);
                if (wounds > 0 && IsWoundable(target))
                {
                    target.AddWound(part, wounds);
                    Morale.Change(target, -wounds);
                }
                else
                {
                    wounds = 0;
                }

                target.SetArmorValue(side, target.ArmorValue(side) - armorLoss);
            }
        }

        // Pain: morale loss from damage taken, reduced by bravery.
        if (healthLoss > 0)
        {
            int bravery = (110 - target.Stats[StatIds.Bravery]) * 10 / 100;
            Morale.Change(target, -(100 * (healthLoss * bravery / 10) / 100));
        }

        battle.Emit(new UnitDamaged(target.Id, healthLoss, stun, side, part, wounds));
        CheckCasualty(battle, target, attacker);
        return healthLoss;
    }

    /// <summary>OXCE conversion: round((random ? 0..d : d) × factor), 0 when d ≤ 0.</summary>
    private static int Convert(bool randomised, double factor, int damage, GameRandom random)
    {
        if (damage <= 0 || factor == 0) return 0;
        int value = randomised ? random.Next(0, damage) : damage;
        return (int)Math.Round(value * factor, MidpointRounding.AwayFromZero);
    }

    /// <summary>1–3 wounds with probability d/11 (always from 11 damage), for the default factor 1.</summary>
    private static int Wounds(DamageTypeDefinition type, int damage, GameRandom random)
    {
        if (type.ToWound <= 0) return 0;
        return random.Next(0, 10) < (int)(damage * type.ToWound) ? random.Next(1, 3) : 0;
    }

    /// <summary>OXCE: only soldiers bleed by default; armours can say otherwise.</summary>
    public static bool IsWoundable(BattleUnit unit) => unit.Armor.Woundable ?? unit.Faction == Faction.Player;

    /// <summary>Death when health reaches 0, unconsciousness when stun reaches health.</summary>
    public static void CheckCasualty(Battle battle, BattleUnit unit, BattleUnit? attacker)
    {
        if (unit.Status == UnitStatus.Dead) return;
        if (unit.Health <= 0) Kill(battle, unit, attacker);
        else if (unit.Status == UnitStatus.Active && unit.Stun >= unit.Health) KnockOut(battle, unit);
    }

    private static void Kill(Battle battle, BattleUnit unit, BattleUnit? killer)
    {
        bool wasStanding = unit.Status == UnitStatus.Active;
        unit.Status = UnitStatus.Dead;
        LeaveBody(battle, unit, wasStanding);
        battle.Emit(new UnitDied(unit.Id, killer?.Id));
        Morale.OnDeath(battle, unit, killer);
    }

    private static void KnockOut(Battle battle, BattleUnit unit)
    {
        unit.Status = UnitStatus.Unconscious;
        LeaveBody(battle, unit, wasStanding: true);
        battle.Emit(new UnitKnockedOut(unit.Id));
    }

    /// <summary>The unit's items fall to the floor and its body becomes an item on the tile (OpenXcom).</summary>
    private static void LeaveBody(Battle battle, BattleUnit unit, bool wasStanding)
    {
        Position p = unit.Position;
        if (battle.Map[p]?.Unit == unit) battle.Map[p]!.Unit = null;
        if (!wasStanding) return;
        foreach (BattleItem item in unit.Inventory.ToList()) battle.DropItem(item, p);
        unit.Kneeling = false;
        if (unit.Armor.Corpse is { } corpseId && battle.Rules.Items.TryGetValue(corpseId, out ItemDefinition? corpse))
        {
            BattleItem body = battle.CreateItem(corpse);
            body.Body = unit;
            battle.DropItem(body, p);
        }
    }

    /// <summary>
    /// A direct hit on a terrain part (OXCE TileEngine::hit): with method 1 the tile takes
    /// 50–150 % of the power, times ToTile (25–75 % for armour-piercing), and loses one layer if
    /// that reaches its armour.
    /// </summary>
    public static void HitTilePart(Battle battle, Position position, TilePartSlot slot, int power, int unitDamage, DamageTypeDefinition type)
    {
        Tile? tile = battle.Map[position];
        if (tile?[slot] is not { } part || type.ToTile <= 0) return;
        int baseDamage = type.TileDamageMethod == 2 ? unitDamage : battle.Random.Next(power / 2, power * 3 / 2);
        int damage = (int)Math.Round(baseDamage * type.ToTile, MidpointRounding.AwayFromZero);
        if (part.Armor > 0 && damage >= part.Armor) DestroyPart(battle, tile, slot);
    }

    /// <summary>Replaces a part by its remains (or nothing) and tells the interface.</summary>
    public static void DestroyPart(Battle battle, Tile tile, TilePartSlot slot)
    {
        TilePartDefinition part = tile[slot]!;
        TilePartDefinition? remains = part.DestroyedInto is { } id && battle.Rules.TileParts.TryGetValue(id, out TilePartDefinition? r) ? r : null;
        tile[slot] = remains;
        battle.Emit(new TilePartDestroyed(tile.Position, slot, part.Id, remains?.Id));
    }
}
