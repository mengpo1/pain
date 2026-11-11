namespace Game.Core.Saves;

/// <summary>
/// What is written to a battle save file. Definitions are referenced by their stable ids, never
/// copied: loading needs the same game pack (and the mods that provide those ids).
/// </summary>
public sealed class BattleSave
{
    /// <summary>Save format understood by this version of the engine.</summary>
    public const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;
    public string Game { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public List<string> Mods { get; set; } = new();
    public DateTime SavedAt { get; set; }

    public string Mission { get; set; } = "";
    public ulong Random { get; set; }
    public int Turn { get; set; }
    public string Faction { get; set; } = "";
    public SavedReserve Reserve { get; set; } = new();

    public SavedMap Map { get; set; } = new();
    public List<SavedUnit> Units { get; set; } = new();
    public List<SavedItem> Items { get; set; } = new();

    /// <summary>Unit ids each side spotted since its turn began (by faction name).</summary>
    public Dictionary<string, List<int>> Spotted { get; set; } = new();
}

public sealed class SavedReserve
{
    public string Mode { get; set; } = "none";
    public bool Kneel { get; set; }
}

public sealed class SavedMap
{
    public int Width { get; set; }
    public int Length { get; set; }
    public int Height { get; set; }
    public int Light { get; set; }
    public List<int[]> PatrolPoints { get; set; } = new();

    /// <summary>Non-empty tiles only.</summary>
    public List<SavedTile> Tiles { get; set; } = new();
}

public sealed class SavedTile
{
    public int[] At { get; set; } = [];
    public string? Floor { get; set; }
    public string? West { get; set; }
    public string? North { get; set; }
    public string? Object { get; set; }
    public string? Zone { get; set; }
    public int Smoke { get; set; }
    public int Fire { get; set; }

    /// <summary>Factions that discovered the tile.</summary>
    public List<string> Discovered { get; set; } = new();
}

public sealed class SavedUnit
{
    public int Id { get; set; }
    public string Definition { get; set; } = "";
    public string Faction { get; set; } = "";
    public string Name { get; set; } = "";
    public int[] At { get; set; } = [];
    public int Direction { get; set; }
    public bool Kneeling { get; set; }
    public string Status { get; set; } = "";
    public string MoraleState { get; set; } = "none";
    public int Tu { get; set; }
    public int Energy { get; set; }
    public int Health { get; set; }
    public int Stun { get; set; }
    public int Morale { get; set; }
    public bool Done { get; set; }
    public Dictionary<string, int> Stats { get; set; } = new();
    public int[] Armor { get; set; } = [];
    public int[] Wounds { get; set; } = [];

    /// <summary>Per faction: rounds since seen (-1 = never) and last position seen.</summary>
    public int[] TurnsSinceSeen { get; set; } = [];
    public List<int[]> LastSeen { get; set; } = new();
    public List<int> HitBy { get; set; } = new();
}

public sealed class SavedItem
{
    public int Id { get; set; }
    public string Definition { get; set; } = "";
    public int? Owner { get; set; }
    public string? Slot { get; set; }
    public int SlotX { get; set; }
    public int SlotY { get; set; }
    public int[]? Ground { get; set; }

    /// <summary>Id of the item this one is loaded into (clips).</summary>
    public int? LoadedIn { get; set; }
    public int Rounds { get; set; }
    public int? Fuse { get; set; }
    public int? PrimedBy { get; set; }
    public int? Body { get; set; }
}
