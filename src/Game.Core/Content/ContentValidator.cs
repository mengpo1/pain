using Game.Core.Battle;
using Game.Core.Definitions;

namespace Game.Core.Content;

/// <summary>
/// Checks references between definitions after loading, with messages naming the definition and
/// the faulty value. Errors in a mission make it unplayable (it is listed in
/// <see cref="ContentDatabase.InvalidMissions"/>); the rest of the game stays available.
/// </summary>
internal static class ContentValidator
{
    public static void Validate(ContentDatabase database)
    {
        Ruleset rules = database.Rules;
        DiagnosticList d = database.Diagnostics;
        AssetCatalog assets = database.Assets;

        foreach (string stat in StatIds.Required)
            if (!rules.Stats.ContainsKey(stat)) d.Error("stats", $"The statistic '{stat}' is required by the rules but not declared.");

        foreach (string slot in new[] { Inventory.RightHand, Inventory.LeftHand, Inventory.Ground })
            if (!rules.InventorySlots.ContainsKey(slot)) d.Error("inventory", $"The inventory slot '{slot}' is required by the rules but not declared.");

        foreach (InventorySlotDefinition slot in rules.InventorySlots.Values)
            foreach (string target in slot.Costs.Keys)
                if (!rules.InventorySlots.ContainsKey(target)) d.Error($"inventory '{slot.Id}'", $"Cost towards unknown slot '{target}'.");

        foreach (TilePartDefinition part in rules.TileParts.Values) ValidateTilePart(part, rules, assets, d);
        foreach (ItemDefinition item in rules.Items.Values) ValidateItem(item, rules, assets, d);
        foreach (ArmorDefinition armor in rules.Armors.Values) ValidateArmor(armor, rules, assets, d);
        foreach (UnitDefinition unit in rules.Units.Values) ValidateUnit(unit, rules, d);
        foreach (MapBlockDefinition block in rules.MapBlocks.Values) ValidateBlock(block, rules, d);
        foreach (TerrainDefinition terrain in rules.Terrains.Values) ValidateTerrain(terrain, rules, d);
        foreach (MissionDefinition mission in rules.Missions.Values)
        {
            int errors = d.Errors.Count();
            ValidateMission(mission, rules, d);
            if (d.Errors.Count() > errors) database.InvalidMissions.Add(mission.Id);
        }

        foreach (string id in database.Game.Missions)
            if (!rules.Missions.ContainsKey(id)) d.Error("game.json", $"Menu mission '{id}' does not exist.");

        ValidateTexts(database, d);
    }

    private static void ValidateTilePart(TilePartDefinition part, Ruleset rules, AssetCatalog assets, DiagnosticList d)
    {
        string source = $"tile part '{part.Id}'";
        if (part.Shape.Length > 0 && !rules.Shapes.ContainsKey(part.Shape)) d.Error(source, $"Unknown shape '{part.Shape}'.");
        foreach ((string label, string? reference) in new[] { ("destroyedInto", part.DestroyedInto), ("openInto", part.OpenInto), ("closedInto", part.ClosedInto) })
            if (reference is not null && !rules.TileParts.ContainsKey(reference)) d.Error(source, $"{label}: unknown tile part '{reference}'.");
        if (part.Door != DoorKind.None && part.OpenInto is null) d.Error(source, "A door needs 'openInto' (the open variant).");
        if (part.Sprite.Length > 0 && !assets.HasSprite(part.Sprite)) d.Warning(source, $"Sprite '{part.Sprite}' is not in assets.json.");
        if (part.StandHeight is < 0 or > 24) d.Error(source, "standHeight must be between 0 and 24 voxels.");
    }

    private static void ValidateItem(ItemDefinition item, Ruleset rules, AssetCatalog assets, DiagnosticList d)
    {
        string source = $"item '{item.Id}'";
        if (item.Damage is { } damage && !rules.DamageTypes.ContainsKey(damage.Type)) d.Error(source, $"Unknown damage type '{damage.Type}'.");
        foreach (string ammo in item.Ammo)
        {
            if (!rules.Items.TryGetValue(ammo, out ItemDefinition? clip)) d.Error(source, $"Unknown ammo item '{ammo}'.");
            else if (clip.Kind != ItemKind.Ammo) d.Error(source, $"'{ammo}' is listed as ammo but is a {clip.Kind}.");
        }

        switch (item.Kind)
        {
            case ItemKind.Firearm when item.Ammo.Count == 0 && item.Damage is null:
                d.Error(source, "A firearm needs 'ammo' items or its own 'damage'.");
                break;
            case ItemKind.Firearm when item.Snap is null && item.Aimed is null && item.Auto is null:
                d.Error(source, "A firearm needs at least one of 'snap', 'aimed', 'auto'.");
                break;
            case ItemKind.Ammo when item.Damage is null:
                d.Error(source, "Ammo needs 'damage'.");
                break;
            case ItemKind.Ammo when item.Rounds <= 0:
                d.Error(source, "Ammo needs 'rounds' > 0.");
                break;
            case ItemKind.Melee when item.Melee is null || item.Damage is null:
                d.Error(source, "A melee weapon needs 'melee' and 'damage'.");
                break;
        }

        if (item.Auto is { Shots: < 1 }) d.Error(source, "auto.shots must be at least 1.");
        foreach (string? sprite in new[] { item.FloorSprite, item.ProjectileSprite })
            if (!string.IsNullOrEmpty(sprite) && !assets.HasSprite(sprite)) d.Warning(source, $"Sprite '{sprite}' is not in assets.json.");
        foreach (string? sound in new[] { item.FireSound, item.HitSound })
            if (sound is not null && !assets.Sounds.ContainsKey(sound)) d.Warning(source, $"Sound '{sound}' is not in assets.json.");
    }

    private static void ValidateArmor(ArmorDefinition armor, Ruleset rules, AssetCatalog assets, DiagnosticList d)
    {
        string source = $"armor '{armor.Id}'";
        if (armor.Corpse is { } corpse && !rules.Items.ContainsKey(corpse)) d.Error(source, $"Unknown corpse item '{corpse}'.");
        foreach (string type in armor.DamageModifiers.Keys)
            if (!rules.DamageTypes.ContainsKey(type)) d.Error(source, $"Damage modifier for unknown damage type '{type}'.");
        foreach (string stat in armor.Stats.Keys)
            if (!rules.Stats.ContainsKey(stat)) d.Error(source, $"Unknown statistic '{stat}'.");
        if (!assets.HasSprite(armor.Sprite)) d.Warning(source, $"Sprite '{armor.Sprite}' is not in assets.json.");
        if (armor.StandHeight is <= 0 or > 24 || armor.KneelHeight <= 0 || armor.KneelHeight > armor.StandHeight)
            d.Error(source, "Heights must satisfy 0 < kneelHeight ≤ standHeight ≤ 24.");
    }

    private static void ValidateUnit(UnitDefinition unit, Ruleset rules, DiagnosticList d)
    {
        string source = $"unit '{unit.Id}'";
        if (!rules.Armors.ContainsKey(unit.Armor)) d.Error(source, $"Unknown armor '{unit.Armor}'.");
        foreach (string stat in unit.Stats.Keys)
            if (!rules.Stats.ContainsKey(stat)) d.Error(source, $"Unknown statistic '{stat}' (declare it in stats/).");
        if (unit.Stats.GetValueOrDefault(StatIds.Health) <= 0) d.Error(source, "Health must be positive.");
        foreach (LoadoutEntry entry in unit.Loadout)
        {
            if (!rules.Items.TryGetValue(entry.Item, out ItemDefinition? item))
            {
                d.Error(source, $"Loadout: unknown item '{entry.Item}'.");
                continue;
            }

            if (entry.Slot is { } slot && !rules.InventorySlots.ContainsKey(slot)) d.Error(source, $"Loadout: unknown slot '{slot}'.");
            if (entry.Ammo is { } ammo && !item.Ammo.Contains(ammo)) d.Error(source, $"Loadout: '{ammo}' cannot be loaded into '{entry.Item}'.");
        }
    }

    private static void ValidateBlock(MapBlockDefinition block, Ruleset rules, DiagnosticList d)
    {
        string source = $"map block '{block.Id}'";
        if (block.Size.Length != 3 || block.Width <= 0 || block.Length <= 0 || block.Height <= 0)
        {
            d.Error(source, "size must be [width, length, levels] with positive values.");
            return;
        }

        foreach ((string symbol, MapCellDefinition cell) in block.Legend)
        {
            if (symbol.Length != 1) d.Error(source, $"Legend key '{symbol}' must be a single character.");
            CheckPart(cell.Floor, "floor");
            CheckPart(cell.West, "west");
            CheckPart(cell.North, "north");
            CheckPart(cell.Object, "object");
            if (cell.Spawn is { } spawn && !Enum.TryParse(spawn, ignoreCase: true, out Faction _))
                d.Error(source, $"Legend '{symbol}': unknown spawn side '{spawn}' (player, hostile, neutral).");

            void CheckPart(string? id, string slot)
            {
                if (id is not null && !rules.TileParts.ContainsKey(id)) d.Error(source, $"Legend '{symbol}': unknown {slot} part '{id}'.");
            }
        }

        if (block.Levels.Count > block.Height) d.Error(source, $"{block.Levels.Count} levels drawn but size says {block.Height}.");
        for (int z = 0; z < block.Levels.Count; z++)
        {
            IReadOnlyList<string> rows = block.Levels[z];
            if (rows.Count != block.Length) d.Error(source, $"Level {z} has {rows.Count} rows, expected {block.Length}.");
            for (int y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != block.Width) d.Error(source, $"Level {z}, row {y} has {rows[y].Length} characters, expected {block.Width}.");
                foreach (char c in rows[y])
                    if (c != ' ' && !block.Legend.ContainsKey(c.ToString())) d.Error(source, $"Level {z}, row {y}: symbol '{c}' is not in the legend.");
            }
        }
    }

    private static void ValidateTerrain(TerrainDefinition terrain, Ruleset rules, DiagnosticList d)
    {
        string source = $"terrain '{terrain.Id}'";
        if (terrain.Blocks.Count == 0) d.Error(source, "A terrain needs blocks.");
        foreach (TerrainBlockEntry entry in terrain.Blocks)
        {
            if (!rules.MapBlocks.TryGetValue(entry.Block, out MapBlockDefinition? block)) d.Error(source, $"Unknown map block '{entry.Block}'.");
            else if (block.Width % terrain.BlockSize != 0 || block.Length % terrain.BlockSize != 0)
                d.Error(source, $"Block '{entry.Block}' ({block.Width} × {block.Length}) is not a multiple of the block size {terrain.BlockSize}.");
        }
    }

    private static void ValidateMission(MissionDefinition mission, Ruleset rules, DiagnosticList d)
    {
        string source = $"mission '{mission.Id}'";
        MissionMapDefinition map = mission.Map;
        if ((map.Block is null) == (map.Terrain is null)) d.Error(source, "map needs exactly one of 'block' or 'terrain'.");
        if (map.Block is { } block && !rules.MapBlocks.ContainsKey(block)) d.Error(source, $"Unknown map block '{block}'.");
        if (map.Terrain is { } terrain && !rules.Terrains.ContainsKey(terrain)) d.Error(source, $"Unknown terrain '{terrain}'.");
        if (mission.Player.Count == 0) d.Error(source, "The player has no units.");
        foreach (DeploymentEntry entry in mission.Player.Concat(mission.Hostile).Concat(mission.Neutral))
            if (!rules.Units.ContainsKey(entry.Unit)) d.Error(source, $"Unknown unit '{entry.Unit}'.");
        if (mission.Objective.Kind == ObjectiveKind.Survive && mission.Objective.Turns <= 0) d.Error(source, "A survive objective needs 'turns'.");
        if (mission.Objective.Kind == ObjectiveKind.ReachZone)
        {
            if (mission.Objective.Zone is null) d.Error(source, "A reachZone objective needs 'zone'.");
            else if (map.Block is { } b && rules.MapBlocks.TryGetValue(b, out MapBlockDefinition? def) && !def.Legend.Values.Any(c => c.Zone == mission.Objective.Zone))
                d.Error(source, $"Zone '{mission.Objective.Zone}' does not appear in map block '{b}'.");
        }
    }

    /// <summary>Names used by definitions should exist in the fallback language (warnings only).</summary>
    private static void ValidateTexts(ContentDatabase database, DiagnosticList d)
    {
        if (!database.Locales.TryGetValue(database.Game.FallbackLanguage, out IReadOnlyDictionary<string, string>? texts))
        {
            d.Error("localization", $"No texts for the fallback language '{database.Game.FallbackLanguage}'.");
            return;
        }

        Ruleset rules = database.Rules;
        IEnumerable<(string Source, string Key)> keys = rules.Items.Values.Select(i => ($"item '{i.Id}'", i.NameKey))
            .Concat(rules.Units.Values.Select(u => ($"unit '{u.Id}'", u.NameKey)))
            .Concat(rules.Stats.Values.Select(s => ($"stat '{s.Id}'", s.NameKey)))
            .Concat(rules.Missions.Values.Select(m => ($"mission '{m.Id}'", m.NameKey)))
            .Concat([("game.json", database.Game.TitleKey)]);
        foreach ((string source, string key) in keys)
            if (key.Length > 0 && !texts.ContainsKey(key)) d.Warning(source, $"Text '{key}' is missing in '{database.Game.FallbackLanguage}'.");
    }
}
