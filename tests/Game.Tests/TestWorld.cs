using Game.Core.Battle;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Tests;

/// <summary>
/// A small hand-made ruleset and helpers to build battles in code, so rule tests do not depend
/// on content files. Values follow OXCE defaults (4 TU floors, 22/14 voxel soldiers…).
/// </summary>
internal static class TestWorld
{
    public static Ruleset Rules(BattleRules? battle = null) => new()
    {
        Stats = new[] { "tu", "stamina", "health", "bravery", "reactions", "firing", "throwing", "strength", "melee" }
            .ToDictionary(id => id, id => new StatDefinition { Id = id, NameKey = $"stat.{id}" }),
        DamageTypes = new Dictionary<string, DamageTypeDefinition>
        {
            ["ap"] = new() { Id = "ap" },
            ["flat"] = new() { Id = "flat", Random = DamageRandom.Flat, ToStun = 0, ToWound = 0, ToArmor = 0 },
        },
        Shapes = new Dictionary<string, ShapeDefinition>
        {
            ["floor"] = new() { Id = "floor", Boxes = [[0, 0, 0, 16, 16, 1]] },
            ["westWall"] = new() { Id = "westWall", Boxes = [[0, 0, 0, 2, 16, 24]] },
            ["northWall"] = new() { Id = "northWall", Boxes = [[0, 0, 0, 16, 2, 24]] },
            ["crate"] = new() { Id = "crate", Boxes = [[2, 2, 0, 14, 14, 12]] },
            ["step8"] = new() { Id = "step8", Boxes = [[0, 0, 0, 16, 16, 8]] },
            ["step16"] = new() { Id = "step16", Boxes = [[0, 0, 0, 16, 16, 16]] },
        },
        TileParts = new Dictionary<string, TilePartDefinition>
        {
            ["ground"] = new() { Id = "ground", Shape = "floor", TuWalk = 4 },
            ["mud"] = new() { Id = "mud", Shape = "floor", TuWalk = 6 },
            ["wallW"] = new() { Id = "wallW", Shape = "westWall", BlocksSight = true, Armor = 50, DestroyedInto = "ruinW" },
            ["wallN"] = new() { Id = "wallN", Shape = "northWall", BlocksSight = true, Armor = 50 },
            ["ruinW"] = new() { Id = "ruinW", Walkable = true },
            ["windowW"] = new() { Id = "windowW", Shape = "westWall", BlocksSight = false, Armor = 10 },
            ["doorW"] = new() { Id = "doorW", Shape = "westWall", BlocksSight = true, Door = DoorKind.Swing, OpenInto = "doorOpenW", Armor = 30 },
            ["doorOpenW"] = new() { Id = "doorOpenW", Walkable = true, ClosedInto = "doorW" },
            ["crate"] = new() { Id = "crate", Shape = "crate", TuWalk = TilePartDefinition.Impassable, Armor = 20 },
            ["pillar"] = new() { Id = "pillar", Shape = "westWall", TuWalk = TilePartDefinition.Impassable, BlocksSight = true },
            ["step8"] = new() { Id = "step8", Shape = "step8", StandHeight = 8 },
            ["step16"] = new() { Id = "step16", Shape = "step16", StandHeight = 16 },
            ["lamp"] = new() { Id = "lamp", Light = 15 },
        },
        Items = new Dictionary<string, ItemDefinition>
        {
            ["rifle"] = new()
            {
                Id = "rifle", Kind = ItemKind.Firearm, TwoHanded = true, Ammo = ["clip"],
                Snap = new AttackModeDefinition { Accuracy = 60, Tu = 25 },
                Aimed = new AttackModeDefinition { Accuracy = 110, Tu = 80 },
                Auto = new AttackModeDefinition { Accuracy = 35, Tu = 35, Shots = 3 },
            },
            ["clip"] = new() { Id = "clip", Kind = ItemKind.Ammo, Rounds = 20, Damage = new DamageDefinition { Power = 30, Type = "ap" } },
            ["laser"] = new()
            {
                Id = "laser", Kind = ItemKind.Firearm, FlatTu = true,
                Snap = new AttackModeDefinition { Accuracy = 100, Tu = 10 },
                Damage = new DamageDefinition { Power = 1000, Type = "flat" },
            },
            ["knife"] = new()
            {
                Id = "knife", Kind = ItemKind.Melee, Melee = new AttackModeDefinition { Accuracy = 100, Tu = 15 },
                Damage = new DamageDefinition { Power = 1000, Type = "flat" },
            },
            ["corpse"] = new() { Id = "corpse", Kind = ItemKind.Corpse },
        },
        Armors = new Dictionary<string, ArmorDefinition>
        {
            ["suit"] = new() { Id = "suit", Front = 5, Side = 4, Rear = 3, Under = 2, Corpse = "corpse", Sprite = "unit" },
            ["plate"] = new() { Id = "plate", Front = 100, Side = 100, Rear = 100, Under = 100, Sprite = "unit", CanKneel = false },
        },
        Units = new Dictionary<string, UnitDefinition>
        {
            ["soldier"] = new()
            {
                Id = "soldier", Armor = "suit",
                Stats = new Dictionary<string, int>
                {
                    ["tu"] = 60, ["stamina"] = 60, ["health"] = 40, ["bravery"] = 50, ["reactions"] = 50,
                    ["firing"] = 60, ["throwing"] = 60, ["strength"] = 30, ["melee"] = 50,
                },
            },
        },
        InventorySlots = new Dictionary<string, InventorySlotDefinition>
        {
            [Inventory.RightHand] = new() { Id = Inventory.RightHand, Kind = InventorySlotKind.Hand },
            [Inventory.LeftHand] = new() { Id = Inventory.LeftHand, Kind = InventorySlotKind.Hand, FillOrder = 1 },
            ["belt"] = new() { Id = "belt", Kind = InventorySlotKind.Grid, Width = 4, Height = 1, FillOrder = 2 },
            [Inventory.Ground] = new() { Id = Inventory.Ground, Kind = InventorySlotKind.Ground },
        },
        Battle = battle ?? new BattleRules(),
    };

    public static MissionDefinition Mission(ObjectiveDefinition? objective = null, int turnLimit = 0) =>
        new() { Id = "test", Objective = objective ?? new ObjectiveDefinition(), TurnLimit = turnLimit };

    /// <summary>A flat map of ground tiles, <paramref name="levels"/> high (upper levels empty).</summary>
    public static Battle Battle(int width = 20, int length = 20, int levels = 1, Ruleset? rules = null, MissionDefinition? mission = null, ulong seed = 1)
    {
        rules ??= Rules();
        var map = new BattleMap(width, length, levels);
        foreach (Tile tile in map.Tiles)
            if (tile.Position.Z == 0) tile[TilePartSlot.Floor] = rules.TileParts["ground"];
        return new Battle(rules, mission ?? Mission(), map, new GameRandom(seed));
    }

    public static void Set(this Battle battle, int x, int y, int z, TilePartSlot slot, string? part) =>
        battle.Map[x, y, z]![slot] = part is null ? null : battle.Rules.TileParts[part];

    public static BattleUnit Soldier(this Battle battle, Faction faction, int x, int y, int z = 0, int direction = 2, string unit = "soldier", string? weapon = null)
    {
        BattleUnit soldier = battle.AddUnit(battle.Rules.Units[unit], faction, new Position(x, y, z), direction);
        if (weapon is not null) battle.Give(soldier, weapon, Inventory.RightHand);
        return soldier;
    }

    public static BattleItem Give(this Battle battle, BattleUnit unit, string itemId, string slot)
    {
        BattleItem item = battle.CreateItem(battle.Rules.Items[itemId]);
        if (item.Definition.Ammo.Count > 0) Inventory.Load(null, item, battle.CreateItem(battle.Rules.Items[item.Definition.Ammo[0]]));
        Assert.True(Inventory.TryPlace(battle, unit, item, slot), $"Cannot place {itemId} in {slot}.");
        return item;
    }

    /// <summary>Executes a command, asserts it is accepted and runs it to completion.</summary>
    public static void Do(this Battle battle, BattleCommand command)
    {
        CommandResult result = battle.Execute(command);
        Assert.True(result.Accepted, $"{command} refused: {result.ReasonKey}");
        battle.RunUntilIdle();
    }
}
