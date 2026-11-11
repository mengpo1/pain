using System.Numerics;
using TopDown.Core.Definitions;

namespace TopDown.Content;

/// <summary>
/// Checks references between definitions after loading, so content mistakes are reported
/// clearly at startup instead of failing in the middle of a game.
/// </summary>
internal static class ContentValidator
{
    public static void Validate(ContentDatabase content)
    {
        DiagnosticList d = content.Diagnostics;
        DefinitionSet defs = content.Definitions;
        AssetCatalog assets = content.Assets;

        ValidateGame(content, d);
        ValidateAnimations(content.Assets, d);

        foreach (WeaponDefinition weapon in defs.Weapons.Values)
        {
            string source = $"weapon:{weapon.Id}";
            // Natural weapons (fists, claws) are never seen on their own.
            if (weapon.CanDrop || !string.IsNullOrEmpty(weapon.SpriteId)) Sprite(assets, d, source, weapon.SpriteId);
            if (!string.IsNullOrEmpty(weapon.HeldSprite)) Sprite(assets, d, source, weapon.HeldSprite);
            if (weapon.IsFirearm)
            {
                if (weapon.MagazineSize <= 0) d.Error(source, "A firearm needs 'magazineSize' > 0.");
                if (!string.IsNullOrEmpty(weapon.ProjectileSpriteId)) Sprite(assets, d, source, weapon.ProjectileSpriteId);
            }

            if (weapon.FireRate <= 0f) d.Error(source, "'fireRate' must be > 0.");
            if (weapon.AmmoType is not null && (!weapon.IsFirearm || weapon.ReloadTime <= 0f))
                d.Error(source, "'ammoType' is for firearms and needs a 'reloadTime' > 0.");
            if (weapon.Execution.Strikes < 1) d.Error(source, "'execution.strikes' must be at least 1.");
            if (weapon.Dismember is { } dismember)
            {
                if (dismember.Chance is < 0f or > 1f) d.Error(source, "'dismember.chance' must be between 0 and 1.");
                if (dismember.Count < 1) d.Error(source, "'dismember.count' must be at least 1.");
                foreach (string part in dismember.Parts)
                    if (!defs.Actors.Values.Any(a => a.Sprites.Parts.Any(p => p.Id == part)))
                        d.Warning(source, $"'dismember.parts': no character has a part '{part}'.");
            }

            if (weapon.WoundChance is < 0f or > 1f) d.Error(source, "'woundChance' must be between 0 and 1.");
            foreach (var (anatomy, variant) in weapon.Execution.Against)
            {
                if (variant.Strikes is < 1) d.Error(source, $"'execution.against.{anatomy}.strikes' must be at least 1.");
                if (!defs.Actors.Values.Any(a => a.Anatomy == anatomy))
                    d.Warning(source, $"'execution.against.{anatomy}': no character has this anatomy.");
            }
            if (weapon.Range <= 0f) d.Error(source, "'range' must be > 0.");
            Sound(assets, d, source, weapon.Sounds.Attack, weapon.Sounds.Hit, weapon.Sounds.Empty, weapon.Sounds.Pickup);
        }

        foreach (ActorDefinition actor in defs.Actors.Values)
        {
            string source = $"actor:{actor.Id}";
            if (actor.Health <= 0f) d.Error(source, "'health' must be > 0.");
            if (actor.Radius <= 1f) d.Error(source, "'radius' is too small.");
            Reference(defs.Weapons, d, source, "weapon", actor.WeaponId);
            Reference(defs.Weapons, d, source, "weapon", actor.NaturalWeaponId);
            Sprite(assets, d, source, actor.Sprites.Body);
            foreach (string? sprite in new[] { actor.Sprites.Legs, actor.Sprites.Down, actor.Sprites.Dead, actor.Sprites.Mount, actor.Sprites.Kick, actor.Sprites.Torso })
                if (sprite is not null) Sprite(assets, d, source, sprite);
            foreach (BodyPart part in actor.Sprites.Parts) Sprite(assets, d, source, part.Sprite);
            foreach ((string key, string sprite) in actor.Sprites.Animations)
                if (!assets.HasSprite(sprite)) d.Warning(source, $"Animation '{key}' uses unknown sprite '{sprite}'.");
            if (actor.Sprites.AnimationSet is { Length: > 0 } set && !assets.Animations.Keys.Any(id => id.StartsWith(set + ".", StringComparison.Ordinal)))
                d.Warning(source, $"Animation set '{set}' has no animation (ids starting with '{set}.').");
            if (actor.Sprites.Parts.Count > 0 && actor.Sprites.Torso is null)
                d.Warning(source, "'sprites.parts' needs 'sprites.torso' (the body without its parts).");
            Sound(assets, d, source, actor.Sounds.Death, actor.Sounds.Hurt, actor.Sounds.Alert, actor.Sounds.Idle, actor.Sounds.Leap);
            if (actor.Ai.Leap is { } leap && (leap.Speed <= 0f || leap.Distance <= 0f || leap.Range < leap.MinRange))
                d.Error(source, "'ai.leap' needs speed and distance > 0 and range >= minRange.");
            if (actor.Stamina is { Max: <= 0f }) d.Error(source, "'stamina.max' must be > 0.");
        }

        foreach (TileDefinition tile in defs.Tiles.Values)
        {
            string source = $"tile:{tile.Id}";
            if (!string.IsNullOrEmpty(tile.SpriteId)) Sprite(assets, d, source, tile.SpriteId);
            if (tile.Destructible && tile.BreaksInto is null) d.Warning(source, "Destructible tile without 'breaksInto' (becomes a bare floor).");
            Reference(defs.Tiles, d, source, "tile", tile.BreaksInto);
            Sound(assets, d, source, tile.BreakSound);
        }

        foreach (DoorDefinition door in defs.Doors.Values)
        {
            Sprite(assets, d, $"door:{door.Id}", door.SpriteId);
            Sound(assets, d, $"door:{door.Id}", door.HitSound);
        }

        foreach (ModifierDefinition modifier in defs.Modifiers.Values)
            Reference(defs.Weapons, d, $"modifier:{modifier.Id}", "weapon", modifier.StartingWeaponId);

        foreach (ThemeDefinition theme in content.Themes.Values)
        {
            if (!assets.Fonts.ContainsKey(theme.Ui.Font)) d.Warning($"theme:{theme.Id}", $"Unknown font '{theme.Ui.Font}'.");
        }

        foreach (LevelDefinition level in content.Levels.Values)
        {
            int errorsBefore = d.Errors.Count();
            ValidateLevel(content, level, d);
            if (d.Errors.Count() > errorsBefore) content.InvalidLevels.Add(level.Id);
        }

        ValidateLocales(content, d);
    }

    private static void ValidateGame(ContentDatabase content, DiagnosticList d)
    {
        GameSettings game = content.Game;
        if (!content.Definitions.Weapons.ContainsKey(game.Rules.UnarmedWeaponId))
            d.Error("game.json", $"rules.unarmedWeaponId '{game.Rules.UnarmedWeaponId}' is not a known weapon.");
        if (!content.Themes.ContainsKey(game.Theme)) d.Error("game.json", $"Unknown theme '{game.Theme}'.");
        if (game.Campaign.Count == 0) d.Warning("game.json", "The campaign is empty.");
        foreach (string id in game.Campaign)
            if (!content.Levels.ContainsKey(id)) d.Error("game.json", $"The campaign references unknown level '{id}'.");
        foreach (string id in game.ExtraLevels)
            if (!content.Levels.ContainsKey(id)) d.Error("game.json", $"'extraLevels' references unknown level '{id}'.");
        if (!content.Locales.ContainsKey(game.FallbackLanguage)) d.Error("game.json", $"No texts for the fallback language '{game.FallbackLanguage}'.");
        Sound(content.Assets, d, "game.json", game.MenuMusic);
        GameSounds s = game.Sounds;
        Sound(content.Assets, d, "game.json", s.UiMove, s.UiSelect, s.Pickup, s.Throw, s.Bounce, s.Impact, s.Footstep,
            s.ObjectiveComplete, s.LevelComplete, s.Crush, s.Tear, s.Transmission, s.Reload, s.Heal);
    }

    private static void ValidateLevel(ContentDatabase content, LevelDefinition level, DiagnosticList d)
    {
        string source = $"level:{level.Id}";
        DefinitionSet defs = content.Definitions;
        if (level.Map.Count == 0)
        {
            d.Error(source, "The map is empty.");
            return;
        }

        if (level.TileSize <= 0f)
        {
            d.Error(source, "'tileSize' must be > 0.");
            return;
        }

        foreach ((string symbol, string tileId) in level.Legend)
        {
            if (symbol.Length != 1) d.Error(source, $"Legend key '{symbol}' must be a single character.");
            if (tileId != TileDefinition.Void.Id && !defs.Tiles.ContainsKey(tileId)) d.Error(source, $"Legend '{symbol}' uses unknown tile '{tileId}'.");
        }

        foreach ((string symbol, LevelDoorSymbol door) in level.DoorSymbols)
        {
            if (symbol.Length != 1) d.Error(source, $"Door symbol '{symbol}' must be a single character.");
            if (level.Legend.ContainsKey(symbol)) d.Error(source, $"'{symbol}' is both a legend tile and a door symbol.");
            if (!defs.Tiles.ContainsKey(door.Tile)) d.Error(source, $"Door symbol '{symbol}' uses unknown tile '{door.Tile}'.");
            Reference(defs.Doors, d, source, "door", door.Door);
        }

        for (int y = 0; y < level.Map.Count; y++)
        {
            for (int x = 0; x < level.Map[y].Length; x++)
            {
                string symbol = level.Map[y][x].ToString();
                if (symbol != " " && !level.Legend.ContainsKey(symbol) && !level.DoorSymbols.ContainsKey(symbol))
                    d.Error(source, $"Map character '{symbol}' at ({x}, {y}) is not in the legend.");
            }
        }

        var position = new PositionChecker(level, defs, d, source);
        Reference(defs.Actors, d, source, "actor", level.Player.Definition);
        Reference(defs.Weapons, d, source, "weapon", NullIfNone(level.Player.WeaponId));
        position.Check(level.Player.Position, "player start");

        var tags = new HashSet<string>(StringComparer.Ordinal);
        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LevelActor actor in level.Actors)
        {
            Reference(defs.Actors, d, source, "actor", actor.Definition);
            Reference(defs.Weapons, d, source, "weapon", NullIfNone(actor.WeaponId));
            position.Check(actor.Position, $"actor '{actor.Id ?? actor.Definition}'");
            foreach (Vector2 point in actor.Patrol) position.Check(point, $"patrol point of '{actor.Id ?? actor.Definition}'");
            if (actor.Behavior == AiBehavior.Patrol && actor.Patrol.Count == 0)
                d.Warning(source, $"Actor '{actor.Id ?? actor.Definition}' patrols but has no 'patrol' points.");
            if (actor.Tag is not null) tags.Add(actor.Tag);
            if (actor.Id is not null && !actorIds.Add(actor.Id)) d.Error(source, $"Actor id '{actor.Id}' is used twice.");
        }

        foreach (LevelWeapon weapon in level.Weapons)
        {
            Reference(defs.Weapons, d, source, "weapon", weapon.Definition);
            position.Check(weapon.Position, $"weapon '{weapon.Definition}'");
        }

        var doorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LevelDoor door in level.Doors)
        {
            Reference(defs.Doors, d, source, "door", door.Definition);
            if (door.Tile.X < 0 || door.Tile.Y < 0 || door.Tile.X >= level.Width || door.Tile.Y >= level.Height)
                d.Error(source, $"Door at {door.Tile} is outside the map.");
            if (door.Id is not null) doorIds.Add(door.Id);
        }

        foreach (LevelProp prop in level.Props) Sprite(content.Assets, d, source, prop.Sprite);
        foreach (LevelDecor decor in level.Decor) Sprite(content.Assets, d, source, decor.Sprite);
        foreach (LevelRoof roof in level.Roofs)
        {
            Sprite(content.Assets, d, source, roof.Sprite);
            if (roof.Width <= 0f || roof.Height <= 0f) d.Error(source, "A roof needs a width and a height.");
        }
        foreach (LevelItem item in level.Items)
        {
            Sprite(content.Assets, d, source, item.Sprite);
            position.Check(item.Position, $"item '{item.Id}'");
        }

        foreach (LevelInteractable interactable in level.Interactables)
        {
            Sprite(content.Assets, d, source, interactable.Sprite);
            if (interactable.UsedSprite is not null) Sprite(content.Assets, d, source, interactable.UsedSprite);
            position.Check(interactable.Position, $"interactable '{interactable.Id}'");
        }

        // Mission items: from items without loot, and from any loot entry.
        var missionItems = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<LootEntry> allLoot = level.Player.Inventory
            .Concat(level.Items.SelectMany(i => i.Loot))
            .Concat(level.Interactables.SelectMany(i => i.Loot));
        foreach (LevelItem item in level.Items.Where(i => i.Loot.Count == 0)) missionItems.Add(item.Id);
        foreach (LootEntry loot in allLoot)
        {
            if (loot.Item is not null) missionItems.Add(loot.Item);
            Reference(defs.Weapons, d, source, "weapon", loot.Weapon);
            if (loot.Ammo is not null && !defs.Weapons.Values.Any(w => w.AmmoType == loot.Ammo))
                d.Warning(source, $"Loot gives ammunition '{loot.Ammo}' that no weapon uses.");
            if (loot.Ammo is null && loot.Medkits <= 0 && loot.Weapon is null && loot.Item is null)
                d.Warning(source, "A loot entry gives nothing.");
        }

        foreach ((string name, LevelZone zone) in level.Zones)
        {
            if (zone.Width <= 0 || zone.Height <= 0) d.Error(source, $"Zone '{name}' has an empty size.");
        }

        if (level.Objectives.Count == 0) d.Warning(source, "No objectives: the level can never be completed.");
        foreach (ObjectiveDefinition objective in level.Objectives)
        {
            switch (objective.Type)
            {
                case ObjectiveType.ReachZone when objective.Zone is null || !level.Zones.ContainsKey(objective.Zone):
                    d.Error(source, $"Objective 'reachZone' uses unknown zone '{objective.Zone}'.");
                    break;
                case ObjectiveType.Collect when objective.Target is null || !missionItems.Contains(objective.Target):
                    d.Error(source, $"Objective 'collect' uses unknown item '{objective.Target}'.");
                    break;
                case ObjectiveType.Activate when !level.Interactables.Any(i => i.Id == objective.Target):
                    d.Error(source, $"Objective 'activate' uses unknown interactable '{objective.Target}'.");
                    break;
                case ObjectiveType.Survive when objective.Duration <= 0f:
                    d.Error(source, "Objective 'survive' needs a 'duration' > 0.");
                    break;
                case ObjectiveType.EliminateAll when objective.Tag is not null && !tags.Contains(objective.Tag):
                    d.Warning(source, $"Objective 'eliminateAll' uses tag '{objective.Tag}' that no actor has.");
                    break;
            }

            if (objective.Requires is not null && !missionItems.Contains(objective.Requires))
                d.Error(source, $"Objective requires item '{objective.Requires}' that nothing in the level gives.");
        }

        foreach (TriggerDefinition trigger in level.Triggers)
        {
            switch (trigger.On)
            {
                case TriggerCondition.EnterZone when !level.Zones.ContainsKey(trigger.Subject):
                    d.Error(source, $"Trigger uses unknown zone '{trigger.Subject}'.");
                    break;
                case TriggerCondition.Activated when !level.Interactables.Any(i => i.Id == trigger.Subject):
                    d.Error(source, $"Trigger uses unknown interactable '{trigger.Subject}'.");
                    break;
                case TriggerCondition.ObjectiveCompleted when !int.TryParse(trigger.Subject, out int index) || index < 0 || index >= level.Objectives.Count:
                    d.Error(source, $"Trigger uses unknown objective index '{trigger.Subject}'.");
                    break;
                case TriggerCondition.Collected when !missionItems.Contains(trigger.Subject):
                    d.Error(source, $"Trigger uses unknown item '{trigger.Subject}'.");
                    break;
            }

            foreach (TriggerAction action in trigger.Actions)
            {
                if (action.Type is TriggerActionType.ActivateTag or TriggerActionType.AlertTag && (action.Tag is null || !tags.Contains(action.Tag)))
                    d.Warning(source, $"Trigger action '{action.Type}' uses tag '{action.Tag}' that no actor has.");
                if (action.Type == TriggerActionType.UnlockDoor && (action.Door is null || !doorIds.Contains(action.Door)))
                    d.Error(source, $"Trigger action 'unlockDoor' uses unknown door '{action.Door}'.");
                if (action.Type is TriggerActionType.PlaySound or TriggerActionType.Transmission) Sound(content.Assets, d, source, action.Sound);
                if (action.Type == TriggerActionType.Spawn)
                {
                    if (action.Definition is null || !defs.Actors.ContainsKey(action.Definition))
                        d.Error(source, $"Trigger action 'spawn' uses unknown character '{action.Definition}'.");
                    position.Check(action.Position, $"spawn of '{action.Definition}'");
                    if (action.Tag is not null) tags.Add(action.Tag);
                }
            }
        }

        Sound(content.Assets, d, source, level.Music, level.Ambience);
    }

    private static void ValidateLocales(ContentDatabase content, DiagnosticList d)
    {
        if (!content.Locales.TryGetValue(content.Game.FallbackLanguage, out var reference)) return;
        foreach ((string language, var table) in content.Locales)
        {
            if (language == content.Game.FallbackLanguage) continue;
            List<string> missing = reference.Keys.Where(key => !table.ContainsKey(key)).Order(StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
                d.Warning($"locale:{language}", $"{missing.Count} text(s) missing, '{content.Game.FallbackLanguage}' is used instead: {string.Join(", ", missing.Take(8))}{(missing.Count > 8 ? "…" : "")}");
        }
    }

    private static string? NullIfNone(string? weaponId) => weaponId == "none" ? null : weaponId;

    private static void Reference<T>(IReadOnlyDictionary<string, T> table, DiagnosticList d, string source, string kind, string? id)
    {
        if (id is not null && !table.ContainsKey(id)) d.Error(source, $"Unknown {kind} '{id}'.");
    }

    /// <summary>Frames, durations and attach points of every animation must line up.</summary>
    private static void ValidateAnimations(AssetCatalog assets, DiagnosticList d)
    {
        foreach ((string id, AnimationDefinition animation) in assets.Animations)
        {
            string source = $"animation:{id}";
            if (!assets.Textures.ContainsKey(animation.Texture)) d.Error(source, $"Unknown texture '{animation.Texture}'.");
            if (animation.FrameWidth <= 0 || animation.FrameHeight <= 0) d.Error(source, "'frameWidth' and 'frameHeight' must be > 0.");
            int frames = animation.Frames.Count;
            if (animation.Durations.Count > 0 && frames > 0 && animation.Durations.Count != frames)
                d.Error(source, $"'durations' has {animation.Durations.Count} values for {frames} frames.");
            if (animation.Durations.Any(t => t <= 0f)) d.Error(source, "Every duration must be > 0.");
            foreach ((string name, List<float[]> points) in animation.Attach)
            {
                if (frames > 0 && points.Count != frames)
                    d.Error(source, $"Attach point '{name}' has {points.Count} entries for {frames} frames.");
                if (points.Any(p => p.Length < 3)) d.Error(source, $"Attach point '{name}': each entry is [x, y, angle] or [x, y, angle, over].");
            }
        }
    }

    private static void Sprite(AssetCatalog assets, DiagnosticList d, string source, string id)
    {
        if (string.IsNullOrEmpty(id)) d.Warning(source, "No sprite id.");
        else if (!assets.HasSprite(id)) d.Warning(source, $"Unknown sprite '{id}' (a placeholder will be drawn).");
    }

    private static void Sound(AssetCatalog assets, DiagnosticList d, string source, params string?[] ids)
    {
        foreach (string? id in ids)
            if (id is not null && !assets.Sounds.ContainsKey(id)) d.Warning(source, $"Unknown sound '{id}'.");
    }

    /// <summary>Warns about objects placed outside the map or inside solid tiles.</summary>
    private sealed class PositionChecker(LevelDefinition level, DefinitionSet defs, DiagnosticList d, string source)
    {
        public void Check(Vector2 position, string what)
        {
            // Same rounding as LevelDefinition.ToWorld: x.5 belongs to the next tile.
            int x = (int)MathF.Floor(position.X + 0.5f);
            int y = (int)MathF.Floor(position.Y + 0.5f);
            if (x < 0 || y < 0 || y >= level.Map.Count || x >= level.Map[y].Length)
            {
                d.Error(source, $"The {what} at {position} is outside the map.");
                return;
            }

            if (level.Legend.TryGetValue(level.Map[y][x].ToString(), out string? tileId)
                && defs.Tiles.TryGetValue(tileId, out TileDefinition? tile) && tile.BlocksMovement)
                d.Warning(source, $"The {what} at {position} is inside a solid tile ('{tileId}').");
        }
    }
}
