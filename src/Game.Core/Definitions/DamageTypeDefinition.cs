using Game.Core.Content;

namespace Game.Core.Definitions;

/// <summary>How the power of an attack is turned into damage (OXCE RuleDamageType::getRandomDamage).</summary>
public enum DamageRandom
{
    /// <summary>Power ± <see cref="BattleRules.DamageRangePercent"/> % (0–200 % in UFO).</summary>
    Standard,

    /// <summary>Power ± <see cref="BattleRules.ExplosiveDamageRangePercent"/> % (50–150 %).</summary>
    Explosion,

    /// <summary>0 to 2 × power.</summary>
    Ufo,

    /// <summary>50 % to 150 % of power.</summary>
    Tftd,

    /// <summary>Exactly the power.</summary>
    Flat,

    /// <summary>Fire: 5–10 whatever the power.</summary>
    Fire,

    /// <summary>No damage (smoke).</summary>
    None,
}

/// <summary>
/// A kind of damage and what it does (OXCE RuleDamageType). Conversions are factors applied to
/// the damage left after armour; defaults are those of OXCE's armour-piercing type.
/// </summary>
public sealed class DamageTypeDefinition : Definition
{
    public string NameKey { get; init; } = "";

    public DamageRandom Random { get; init; } = DamageRandom.Standard;

    /// <summary>Multiplies the armour value subtracted from the damage (0 = ignores armour).</summary>
    public double ArmorEffectiveness { get; init; } = 1.0;

    public double ToHealth { get; init; } = 1.0;

    /// <summary>Armour of the side hit loses this fraction of the damage that went through (rounded).</summary>
    public double ToArmor { get; init; } = 0.1;

    /// <summary>Stun added: random 0..damage times this factor.</summary>
    public double ToStun { get; init; } = 0.25;

    /// <summary>When false, stun is exactly damage × <see cref="ToStun"/> (stun weapons).</summary>
    public bool RandomStun { get; init; } = true;

    /// <summary>Fatal wounds: chance damage/11 (always from 11 damage) of 1–3 wounds when 1.0.</summary>
    public double ToWound { get; init; } = 1.0;

    /// <summary>Fraction of the damage applied to the terrain part hit.</summary>
    public double ToTile { get; init; } = 0.5;

    /// <summary>Items on the ground are destroyed when the explosion damage × this exceeds their armour.</summary>
    public double ToItem { get; init; }

    /// <summary>Always hits the front armour and the torso (fire, smoke).</summary>
    public bool IgnoreDirection { get; init; }

    /// <summary>
    /// Explosions: radius = power × this when the item gives no radius (OXCE 0.05 for HE, i.e.
    /// power / 20); 0 = this type never explodes.
    /// </summary>
    public double RadiusEffectiveness { get; init; }

    /// <summary>Power lost per tile travelled by an explosion.</summary>
    public int RadiusReduction { get; init; } = 10;

    /// <summary>
    /// Direct hits on terrain: 1 = the tile takes 50–150 % of power (× ToTile), 2 = the same
    /// randomised damage as units.
    /// </summary>
    public int TileDamageMethod { get; init; } = 1;

    /// <summary>Sprite id of the explosion animation (explosive types).</summary>
    public string? ExplosionSprite { get; init; }

    public string? ExplosionSound { get; init; }
}
