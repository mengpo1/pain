using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

public enum UnitStatus
{
    Active,

    /// <summary>Stun damage reached health: the unit lies on the floor and can recover.</summary>
    Unconscious,

    Dead,
}

/// <summary>
/// A unit in battle: what it is (definition and armour), where it stands, and everything that
/// changes during the fight (TU, health, stun, morale, armour wear, wounds, inventory).
/// </summary>
public sealed class BattleUnit
{
    private readonly int[] _armor = new int[5];
    private readonly int[] _wounds = new int[6];

    internal BattleUnit(int id, UnitDefinition definition, ArmorDefinition armor, Faction faction, string name)
    {
        Id = id;
        Definition = definition;
        Armor = armor;
        Faction = faction;
        Name = name;
        Stats = new StatBlock(definition.Stats);
        Stats.Add(armor.Stats);
        foreach (ArmorSide side in Enum.GetValues<ArmorSide>()) _armor[(int)side] = armor.ArmorFor(side);
        Tu = MaxTu;
        Energy = Stats[StatIds.Stamina];
        Health = Stats[StatIds.Health];
        Morale = 100;
    }

    public int Id { get; }
    public UnitDefinition Definition { get; }
    public ArmorDefinition Armor { get; }
    public Faction Faction { get; }

    /// <summary>Displayed name (generated for soldiers, localization key text for others).</summary>
    public string Name { get; }

    /// <summary>Base statistics plus armour bonuses.</summary>
    public StatBlock Stats { get; }

    public Position Position { get; internal set; }

    /// <summary>Facing, 0–7 (see <see cref="Directions"/>).</summary>
    public int Direction { get; internal set; }

    public bool Kneeling { get; internal set; }

    public UnitStatus Status { get; internal set; }

    public int Tu { get; internal set; }
    public int Energy { get; internal set; }
    public int Health { get; internal set; }
    public int Stun { get; internal set; }
    public int Morale { get; internal set; }

    public int MaxTu => Stats[StatIds.TimeUnits];
    public int MaxEnergy => Stats[StatIds.Stamina];
    public int MaxHealth => Stats[StatIds.Health];

    public bool IsActive => Status == UnitStatus.Active;

    /// <summary>Items carried, in every slot.</summary>
    public List<BattleItem> Inventory { get; } = new();

    /// <summary>Hostile units this unit currently sees (recomputed after every move).</summary>
    public HashSet<BattleUnit> VisibleUnits { get; } = new();

    /// <summary>
    /// Turns since an enemy side last saw this unit, by faction (int.MaxValue = never). The AI of a
    /// side only knows units it saw recently (see <see cref="AiDefinition.Intelligence"/>).
    /// </summary>
    internal int[] TurnsSinceSeenBy { get; } = [int.MaxValue, int.MaxValue, int.MaxValue];

    /// <summary>Last tile where each faction saw this unit.</summary>
    internal Position[] LastSeenPositionBy { get; } = new Position[Factions.Count];

    /// <summary>Units that hit this one during the current turn: they can be reacted to even from behind.</summary>
    internal HashSet<BattleUnit> HitByThisTurn { get; } = new();

    /// <summary>Player bookkeeping: the unit has finished acting this turn (skipped by "next unit").</summary>
    public bool Done { get; internal set; }

    public int ArmorValue(ArmorSide side) => _armor[(int)side];

    internal void SetArmorValue(ArmorSide side, int value) => _armor[(int)side] = Math.Max(0, value);

    public int Wounds(BodyPart part) => _wounds[(int)part];

    public int TotalWounds => _wounds.Sum();

    internal void AddWound(BodyPart part, int count) => _wounds[(int)part] += count;

    public BattleItem? ItemInSlot(string slot) => Inventory.FirstOrDefault(i => i.Slot == slot);

    /// <summary>Height in voxels of the top of the unit above its feet.</summary>
    public int Height => Kneeling ? Armor.KneelHeight : Armor.StandHeight;

    public int EyeHeight => Math.Max(1, Height - Armor.EyeOffset);

    public bool KnowsOf(BattleUnit other, int memoryTurns) => other.TurnsSinceSeenBy[(int)Faction] <= memoryTurns;

    public Position LastKnownPosition(BattleUnit other) => other.LastSeenPositionBy[(int)Faction];

    public override string ToString() => $"{Name} ({Definition.Id}#{Id}, {Faction}, {Position})";
}
