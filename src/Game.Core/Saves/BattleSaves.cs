using System.Text.Json;
using Game.Core.Battle;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Core.Saves;

/// <summary>A save cannot be written or read; the message says why, in plain words.</summary>
public sealed class SaveException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Writes and reads battle saves (versioned JSON). Writing goes through a temporary file renamed
/// over the old one, so a crash never leaves a half-written save. Reading checks the format, the
/// game pack and every definition id, and reports what is missing.
/// </summary>
public static class BattleSaves
{
    public static void Write(Game.Core.Battle.Battle battle, ContentDatabase content, string path)
    {
        if (battle.IsBusy) throw new SaveException("The battle can only be saved between actions.");
        BattleSave save = Capture(battle, content);
        string json = JsonSerializer.Serialize(save, ContentJson.Options);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null) Directory.CreateDirectory(directory);
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SaveException($"Cannot write '{path}': {exception.Message}", exception);
        }
    }

    public static Game.Core.Battle.Battle Read(string path, ContentDatabase content)
    {
        BattleSave save;
        try
        {
            save = JsonSerializer.Deserialize<BattleSave>(File.ReadAllText(path), ContentJson.Options)
                ?? throw new SaveException("The save file is empty.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SaveException($"Cannot read '{path}': {exception.Message}", exception);
        }
        catch (JsonException exception)
        {
            throw new SaveException($"'{Path.GetFileName(path)}' is not a valid save: {exception.Message}", exception);
        }

        return Restore(save, content);
    }

    public static BattleSave Capture(Game.Core.Battle.Battle battle, ContentDatabase content)
    {
        var save = new BattleSave
        {
            Game = content.GamePack.Manifest.Id,
            GameVersion = content.GamePack.Manifest.Version,
            Mods = content.LoadedPacks.Skip(1).Select(p => p.Manifest.Id).ToList(),
            SavedAt = DateTime.UtcNow,
            Mission = battle.Mission.Id,
            Random = battle.Random.State,
            Turn = battle.Turn,
            Faction = Name(battle.CurrentFaction),
            Reserve = new SavedReserve { Mode = Name(battle.PlayerReserve.Mode), Kneel = battle.PlayerReserve.Kneel },
        };

        BattleMap map = battle.Map;
        save.Map = new SavedMap
        {
            Width = map.Width, Length = map.Length, Height = map.Height, Light = map.GlobalLight,
            PatrolPoints = map.PatrolPoints.Select(At).ToList(),
        };
        foreach (Tile tile in map.Tiles)
        {
            List<string> discovered = Factions.All.Where(tile.IsDiscovered).Select(Name).ToList();
            if (tile.IsEmpty && tile.Zone is null && discovered.Count == 0) continue;
            save.Map.Tiles.Add(new SavedTile
            {
                At = At(tile.Position), Floor = tile.Floor?.Id, West = tile.WestWall?.Id, North = tile.NorthWall?.Id, Object = tile.Object?.Id,
                Zone = tile.Zone, Smoke = tile.Smoke, Fire = tile.Fire, Discovered = discovered,
            });
        }

        foreach (BattleUnit unit in battle.Units)
        {
            save.Units.Add(new SavedUnit
            {
                Id = unit.Id, Definition = unit.Definition.Id, Faction = Name(unit.Faction), Name = unit.Name,
                At = At(unit.Position), Direction = unit.Direction, Kneeling = unit.Kneeling, Status = Name(unit.Status),
                MoraleState = Name(unit.MoraleState), Tu = unit.Tu, Energy = unit.Energy, Health = unit.Health, Stun = unit.Stun,
                Morale = unit.Morale, Done = unit.Done, Stats = unit.Stats.Values.ToDictionary(p => p.Key, p => p.Value),
                Armor = Enum.GetValues<ArmorSide>().Select(unit.ArmorValue).ToArray(),
                Wounds = Enum.GetValues<BodyPart>().Select(unit.Wounds).ToArray(),
                TurnsSinceSeen = unit.TurnsSinceSeenBy.Select(t => t == int.MaxValue ? -1 : t).ToArray(),
                LastSeen = unit.LastSeenPositionBy.Select(At).ToList(),
                HitBy = unit.HitByThisTurn.Select(u => u.Id).ToList(),
            });
        }

        foreach (BattleItem item in battle.Items)
        {
            BattleItem? weapon = battle.Items.FirstOrDefault(i => i.LoadedAmmo == item);
            save.Items.Add(new SavedItem
            {
                Id = item.Id, Definition = item.Definition.Id, Owner = item.Owner?.Id, Slot = item.Slot, SlotX = item.SlotX, SlotY = item.SlotY,
                Ground = item.GroundPosition is { } ground ? At(ground) : null, LoadedIn = weapon?.Id, Rounds = item.Rounds,
                Fuse = item.Fuse, PrimedBy = item.PrimedBy?.Id, Body = item.Body?.Id,
            });
        }

        foreach (Faction faction in Factions.All)
            save.Spotted[Name(faction)] = battle.SpottedThisTurn(faction).Select(u => u.Id).ToList();
        return save;
    }

    public static Game.Core.Battle.Battle Restore(BattleSave save, ContentDatabase content)
    {
        if (save.Format != BattleSave.CurrentFormat)
            throw new SaveException($"This save uses format {save.Format}; this version reads format {BattleSave.CurrentFormat}.");
        if (save.Game != content.GamePack.Manifest.Id)
            throw new SaveException($"This save belongs to the game '{save.Game}', not '{content.GamePack.Manifest.Id}'.");

        Ruleset rules = content.Rules;
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        T Need<T>(IReadOnlyDictionary<string, T> table, string id, string kind) where T : class
        {
            if (table.TryGetValue(id, out T? value)) return value;
            missing.Add($"{kind} '{id}'");
            return null!;
        }

        MissionDefinition mission = Need(rules.Missions, save.Mission, "mission");
        foreach (SavedTile tile in save.Map.Tiles)
            foreach (string? part in new[] { tile.Floor, tile.West, tile.North, tile.Object })
                if (part is not null) Need(rules.TileParts, part, "tile part");
        foreach (SavedUnit unit in save.Units)
            if (Need(rules.Units, unit.Definition, "unit") is { } definition) Need(rules.Armors, definition.Armor, "armor");
        foreach (SavedItem item in save.Items) Need(rules.Items, item.Definition, "item");
        if (missing.Count > 0)
        {
            string mods = save.Mods.Count == 0 ? "" : $" The save was made with the mods: {string.Join(", ", save.Mods)}.";
            throw new SaveException($"The loaded content lacks definitions used by this save: {string.Join(", ", missing)}.{mods}");
        }

        var map = new BattleMap(save.Map.Width, save.Map.Length, save.Map.Height) { GlobalLight = save.Map.Light };
        map.PatrolPoints.AddRange(save.Map.PatrolPoints.Select(Pos));
        foreach (SavedTile saved in save.Map.Tiles)
        {
            Tile tile = map[Pos(saved.At)] ?? throw new SaveException($"Tile {string.Join(",", saved.At)} is outside the saved map.");
            tile[TilePartSlot.Floor] = Part(rules, saved.Floor);
            tile[TilePartSlot.WestWall] = Part(rules, saved.West);
            tile[TilePartSlot.NorthWall] = Part(rules, saved.North);
            tile[TilePartSlot.Object] = Part(rules, saved.Object);
            tile.Zone = saved.Zone;
            tile.Smoke = saved.Smoke;
            tile.Fire = saved.Fire;
            foreach (string faction in saved.Discovered) tile.Discover(Parse<Faction>(faction));
        }

        var battle = new Game.Core.Battle.Battle(rules, mission, map, new GameRandom(save.Random));
        battle.RestoreTurn(save.Turn, Parse<Faction>(save.Faction));
        battle.PlayerReserve.Mode = Parse<ReserveMode>(save.Reserve.Mode);
        battle.PlayerReserve.Kneel = save.Reserve.Kneel;

        var units = new Dictionary<int, BattleUnit>();
        foreach (SavedUnit saved in save.Units)
        {
            UnitDefinition definition = rules.Units[saved.Definition];
            var unit = new BattleUnit(saved.Id, definition, rules.Armors[definition.Armor], Parse<Faction>(saved.Faction), saved.Name)
            {
                Position = Pos(saved.At), Direction = saved.Direction, Kneeling = saved.Kneeling, Status = Parse<UnitStatus>(saved.Status),
                MoraleState = Parse<MoraleState>(saved.MoraleState), Tu = saved.Tu, Energy = saved.Energy, Health = saved.Health,
                Stun = saved.Stun, Morale = saved.Morale, Done = saved.Done,
            };
            foreach ((string stat, int value) in saved.Stats) unit.Stats[stat] = value;
            foreach (ArmorSide side in Enum.GetValues<ArmorSide>()) unit.SetArmorValue(side, saved.Armor.ElementAtOrDefault((int)side));
            foreach (BodyPart part in Enum.GetValues<BodyPart>()) unit.AddWound(part, saved.Wounds.ElementAtOrDefault((int)part));
            for (int f = 0; f < Factions.Count; f++)
            {
                int turns = saved.TurnsSinceSeen.ElementAtOrDefault(f);
                unit.TurnsSinceSeenBy[f] = turns < 0 ? int.MaxValue : turns;
                if (f < saved.LastSeen.Count) unit.LastSeenPositionBy[f] = Pos(saved.LastSeen[f]);
            }

            battle.Adopt(unit);
            units[unit.Id] = unit;
        }

        foreach (SavedUnit saved in save.Units)
            foreach (int attacker in saved.HitBy)
                if (units.TryGetValue(attacker, out BattleUnit? hitter)) units[saved.Id].HitByThisTurn.Add(hitter);

        var items = new Dictionary<int, BattleItem>();
        foreach (SavedItem saved in save.Items)
        {
            var item = new BattleItem(saved.Id, rules.Items[saved.Definition])
            {
                Rounds = saved.Rounds, Fuse = saved.Fuse,
                PrimedBy = saved.PrimedBy is { } primer ? units.GetValueOrDefault(primer) : null,
                Body = saved.Body is { } body ? units.GetValueOrDefault(body) : null,
            };
            battle.Adopt(item);
            items[item.Id] = item;
        }

        foreach (SavedItem saved in save.Items)
        {
            BattleItem item = items[saved.Id];
            if (saved.LoadedIn is { } weapon && items.TryGetValue(weapon, out BattleItem? holder))
            {
                holder.LoadedAmmo = item;
            }
            else if (saved.Owner is { } owner && units.TryGetValue(owner, out BattleUnit? carrier) && saved.Slot is not null)
            {
                item.Owner = carrier;
                item.Slot = saved.Slot;
                item.SlotX = saved.SlotX;
                item.SlotY = saved.SlotY;
                carrier.Inventory.Add(item);
            }
            else if (saved.Ground is { } ground)
            {
                item.GroundPosition = Pos(ground);
                map[item.GroundPosition.Value]?.Items.Add(item);
            }
        }

        VisionSystem.Refresh(battle);
        foreach ((string faction, List<int> ids) in save.Spotted)
        {
            HashSet<BattleUnit> spotted = battle.SpottedThisTurn(Parse<Faction>(faction));
            foreach (int id in ids)
                if (units.TryGetValue(id, out BattleUnit? unit)) spotted.Add(unit);
        }

        battle.TakeEvents();
        return battle;
    }

    private static TilePartDefinition? Part(Ruleset rules, string? id) => id is null ? null : rules.TileParts[id];

    private static int[] At(Position p) => [p.X, p.Y, p.Z];

    private static Position Pos(int[] at) =>
        at.Length == 3 ? new Position(at[0], at[1], at[2]) : throw new SaveException("A position must have three coordinates.");

    private static string Name<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static T Parse<T>(string text) where T : struct, Enum =>
        Enum.TryParse(text, ignoreCase: true, out T value) ? value : throw new SaveException($"Unknown {typeof(T).Name} '{text}' in the save.");
}
