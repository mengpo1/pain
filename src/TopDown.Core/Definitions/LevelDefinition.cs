using System.Numerics;

namespace TopDown.Core.Definitions;

/// <summary>
/// A level as stored in content files. Positions are in tile units: (3, 4) is the centre
/// of the tile in column 3, row 4; fractional values are allowed.
/// </summary>
public sealed class LevelDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string? IntroKey { get; set; }
    public float TileSize { get; set; } = 32f;

    /// <summary>Character → tile id used to read <see cref="Map"/>.</summary>
    public Dictionary<string, string> Legend { get; set; } = new();

    /// <summary>One string per row, one character per tile.</summary>
    public List<string> Map { get; set; } = new();

    /// <summary>
    /// Map characters that place a door on a floor tile. The door's axis is deduced from the
    /// walls around it (walls left and right → horizontal door).
    /// </summary>
    public Dictionary<string, LevelDoorSymbol> DoorSymbols { get; set; } = new();

    public LevelPlayer Player { get; set; } = new();
    public List<LevelActor> Actors { get; set; } = new();
    public List<LevelWeapon> Weapons { get; set; } = new();
    public List<LevelDoor> Doors { get; set; } = new();
    public List<LevelProp> Props { get; set; } = new();
    public List<LevelDecor> Decor { get; set; } = new();
    public List<LevelRoof> Roofs { get; set; } = new();
    public List<LevelLight> Lights { get; set; } = new();
    public List<LevelItem> Items { get; set; } = new();
    public List<LevelInteractable> Interactables { get; set; } = new();
    public Dictionary<string, LevelZone> Zones { get; set; } = new();
    public List<ObjectiveDefinition> Objectives { get; set; } = new();
    public List<TriggerDefinition> Triggers { get; set; } = new();

    public string? Music { get; set; }
    public string? Ambience { get; set; }

    public int Width => Map.Count == 0 ? 0 : Map.Max(row => row.Length);
    public int Height => Map.Count;

    public Vector2 ToWorld(Vector2 tilePosition) => (tilePosition + new Vector2(0.5f)) * TileSize;
}

public sealed class LevelPlayer
{
    public string Definition { get; set; } = "player";
    public Vector2 Position { get; set; }
    public float FacingDegrees { get; set; }
    public string? WeaponId { get; set; }

    /// <summary>What the player carries at the start (spare ammunition, medkits).</summary>
    public List<LootEntry> Inventory { get; set; } = new();
}

/// <summary>
/// Something given to the player: spare ammunition of a type, medkits, a weapon (dropped at the
/// spot) or a mission item. One field per entry is enough.
/// </summary>
public sealed class LootEntry
{
    /// <summary>Ammunition type (see the weapons' <c>ammoType</c>) and its amount.</summary>
    public string? Ammo { get; set; }
    public int Amount { get; set; }
    public int Medkits { get; set; }
    /// <summary>Weapon definition id: placed on the floor where it was found.</summary>
    public string? Weapon { get; set; }
    /// <summary>Mission item id (for "collect" objectives and "requires").</summary>
    public string? Item { get; set; }
    public string? NameKey { get; set; }
}

public sealed class LevelActor
{
    /// <summary>Optional unique name, used by triggers.</summary>
    public string? Id { get; set; }
    public string Definition { get; set; } = "";
    public Vector2 Position { get; set; }
    public float FacingDegrees { get; set; }

    /// <summary>Overrides the definition's AI behaviour.</summary>
    public AiBehavior? Behavior { get; set; }

    /// <summary>Patrol waypoints (tile units).</summary>
    public List<Vector2> Patrol { get; set; } = new();

    /// <summary>Overrides the definition's weapon. "none" = unarmed.</summary>
    public string? WeaponId { get; set; }

    /// <summary>Group name used by triggers ("ambush", "wave2"…).</summary>
    public string? Tag { get; set; }
}

public sealed class LevelWeapon
{
    public string Definition { get; set; } = "";
    public Vector2 Position { get; set; }
    public float RotationDegrees { get; set; }
    /// <summary>Remaining ammunition; null = full magazine.</summary>
    public int? Ammo { get; set; }
}

public enum DoorAxis
{
    Vertical,
    Horizontal,
}

public enum DoorHinge
{
    /// <summary>Top end for a vertical door, left end for a horizontal one.</summary>
    Start,
    End,
}

public sealed class LevelDoor
{
    public string? Id { get; set; }
    public string Definition { get; set; } = "";
    /// <summary>Tile (integer column, row) where the door stands, usually a gap in a wall.</summary>
    public Vector2 Tile { get; set; }
    public DoorAxis Axis { get; set; } = DoorAxis.Vertical;
    public DoorHinge Hinge { get; set; } = DoorHinge.Start;
    public bool Locked { get; set; }
}

public sealed class LevelDoorSymbol
{
    /// <summary>Floor tile under the door.</summary>
    public string Tile { get; set; } = "";
    public string Door { get; set; } = "";
    public DoorHinge Hinge { get; set; } = DoorHinge.Start;
    public bool Locked { get; set; }
}

/// <summary>A solid object with its own collision box, independent from its sprite.</summary>
public sealed class LevelProp
{
    public string Sprite { get; set; } = "";
    public Vector2 Position { get; set; }
    public float RotationDegrees { get; set; }
    /// <summary>Collision box size in tile units (axis-aligned, centred on the position).</summary>
    public Vector2 Size { get; set; } = Vector2.One;
    public bool BlocksMovement { get; set; } = true;
    public bool BlocksProjectiles { get; set; }
    public bool BlocksVision { get; set; }
}

public enum DecorLayer
{
    Floor,
    Top,
    /// <summary>On a roof: drawn with it, and fades away with it.</summary>
    Roof,
}

/// <summary>
/// Purely visual roof over a building: drawn above everything inside, it fades out while the player is under
/// it and back in when the player leaves (what is inside stays hidden from the outside).
/// </summary>
public sealed class LevelRoof
{
    /// <summary>Covered area in tiles: left, top, width, height.</summary>
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    /// <summary>Texture tiled over the area (one tile per map tile).</summary>
    public string Sprite { get; set; } = "";
    /// <summary>Opacity while the player is under it (0 = fully open).</summary>
    public float InsideOpacity { get; set; }
}

/// <summary>Purely visual element: never collides.</summary>
public sealed class LevelDecor
{
    public string Sprite { get; set; } = "";
    public Vector2 Position { get; set; }
    public float RotationDegrees { get; set; }
    public float Scale { get; set; } = 1f;
    public DecorLayer Layer { get; set; } = DecorLayer.Floor;
}

public sealed class LevelLight
{
    public Vector2 Position { get; set; }
    /// <summary>Radius in tiles.</summary>
    public float Radius { get; set; } = 4f;
    public string Color { get; set; } = "#ffffff";
    public float Intensity { get; set; } = 1f;
    /// <summary>0 = steady, 1 = strong random flicker.</summary>
    public float Flicker { get; set; }
}

/// <summary>Picked up by walking over it. Without <see cref="Loot"/> it is a mission item.</summary>
public sealed class LevelItem
{
    public string Id { get; set; } = "";
    public string Sprite { get; set; } = "";
    public string NameKey { get; set; } = "";
    public Vector2 Position { get; set; }
    /// <summary>What it gives (ammunition box, medkit…).</summary>
    public List<LootEntry> Loot { get; set; } = new();
}

/// <summary>Used with the interact key (switch, locker to search…).</summary>
public sealed class LevelInteractable
{
    public string Id { get; set; } = "";
    public string Sprite { get; set; } = "";
    /// <summary>Drawn once it has been used (an open, empty locker). Default: <see cref="Sprite"/>, dimmed.</summary>
    public string? UsedSprite { get; set; }
    public string PromptKey { get; set; } = "";
    public Vector2 Position { get; set; }
    public bool Once { get; set; } = true;
    /// <summary>Found inside when searched (given once).</summary>
    public List<LootEntry> Loot { get; set; } = new();
}

/// <summary>Rectangle of whole tiles: (X, Y) is the top-left tile, size in tiles.</summary>
public sealed class LevelZone
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
}

public enum ObjectiveType
{
    EliminateAll,
    ReachZone,
    Collect,
    Survive,
    Activate,
}

/// <summary>Objectives of a level are completed in order.</summary>
public sealed class ObjectiveDefinition
{
    public ObjectiveType Type { get; set; }
    public string TextKey { get; set; } = "";
    /// <summary>Zone for ReachZone.</summary>
    public string? Zone { get; set; }
    /// <summary>Item id for Collect, interactable id for Activate.</summary>
    public string? Target { get; set; }
    /// <summary>Only actors with this tag count for EliminateAll.</summary>
    public string? Tag { get; set; }
    /// <summary>Seconds for Survive.</summary>
    public float Duration { get; set; }
    /// <summary>Mission item the player must carry (an extraction that needs what was found).</summary>
    public string? Requires { get; set; }
}

public enum TriggerCondition
{
    EnterZone,
    ObjectiveCompleted,
    Activated,
    /// <summary>The level has just started.</summary>
    Start,
    /// <summary>The player got a mission item (subject = item id).</summary>
    Collected,
}

public enum TriggerActionType
{
    /// <summary>Wakes up dormant actors with the tag.</summary>
    ActivateTag,
    /// <summary>Makes actors with the tag hunt the player.</summary>
    AlertTag,
    Message,
    UnlockDoor,
    PlaySound,
    /// <summary>A message from the observer (radio panel with a signal sound).</summary>
    Transmission,
    /// <summary>A new character appears (definition, position, tag), hunting the player if "alert".</summary>
    Spawn,
}

public sealed class TriggerDefinition
{
    public TriggerCondition On { get; set; }
    /// <summary>Zone name, objective index (as text) or interactable id, depending on <see cref="On"/>.</summary>
    public string Subject { get; set; } = "";
    public bool Once { get; set; } = true;
    public List<TriggerAction> Actions { get; set; } = new();
}

public sealed class TriggerAction
{
    public TriggerActionType Type { get; set; }
    public string? Tag { get; set; }
    public string? TextKey { get; set; }
    public string? Door { get; set; }
    public string? Sound { get; set; }
    public float Duration { get; set; } = 3f;
    /// <summary>Spawn: character definition, position in tiles, facing.</summary>
    public string? Definition { get; set; }
    public Vector2 Position { get; set; }
    public float FacingDegrees { get; set; }
    /// <summary>Spawn: hunts the player at once.</summary>
    public bool Alert { get; set; }
}
