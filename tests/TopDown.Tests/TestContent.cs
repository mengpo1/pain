using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Tests;

/// <summary>Small hand-made definitions so logic tests do not depend on content files.</summary>
internal static class TestContent
{
    public const float Tile = 32f;
    public const float Dt = 1f / 120f;

    public static DefinitionSet Definitions(Action<GameRules>? configureRules = null)
    {
        var weapons = new Dictionary<string, WeaponDefinition>
        {
            ["unarmed"] = new()
            {
                Id = "unarmed", Kind = WeaponKind.Melee, CanDrop = false, Damage = 0, DamageType = "unarmed",
                Range = 16, ArcDegrees = 90, AttackDuration = 0.2f, HitDelay = 0.05f, FireRate = 3f,
                Knockdown = true, KnockdownDuration = 2f, Knockback = 160f,
                Execution = new ExecutionDefinition { Strikes = 3, WindUp = 0.1f, StrikeInterval = 0.12f, StrikeOnInput = true, AutoStrikeDelay = 0.4f, Recovery = 0.1f },
            },
            ["bat"] = new()
            {
                Id = "bat", Kind = WeaponKind.Melee, SpriteId = "weapon.bat", Damage = 100, DamageType = "blunt",
                Range = 24, ArcDegrees = 120, AttackDuration = 0.25f, HitDelay = 0.07f, FireRate = 2.5f,
                Execution = new ExecutionDefinition { Strikes = 1, WindUp = 0.15f, Recovery = 0.1f },
                Throw = new ThrowDefinition { Speed = 700, Damage = 0, Knockdown = true, KnockdownDuration = 2f },
            },
            ["knife"] = new()
            {
                Id = "knife", Kind = WeaponKind.Melee, SpriteId = "weapon.knife", Damage = 100, DamageType = "sharp",
                Range = 16, ArcDegrees = 80, AttackDuration = 0.15f, HitDelay = 0.04f, FireRate = 4f,
                Throw = new ThrowDefinition { Speed = 900, Damage = 100, DamageType = "sharp", Knockdown = false },
            },
            ["pistol"] = new()
            {
                Id = "pistol", Kind = WeaponKind.Firearm, SpriteId = "weapon.pistol", Damage = 100, DamageType = "bullet",
                Range = 900, FireRate = 4, MagazineSize = 3, SpreadDegrees = 0, ProjectileSpeed = 2300,
                NoiseRadius = 600, MuzzleDistance = 20,
                Execution = new ExecutionDefinition { Mode = ExecutionMode.Shot, Strikes = 2, WindUp = 0.1f, StrikeInterval = 0.15f, Recovery = 0.1f },
            },
            ["revolver"] = new()
            {
                Id = "revolver", Kind = WeaponKind.Firearm, SpriteId = "weapon.revolver", Damage = 100, DamageType = "bullet",
                Range = 900, FireRate = 2, MagazineSize = 6, ProjectileSpeed = 0, NoiseRadius = 600, MuzzleDistance = 20,
            },
            ["railgun"] = new()
            {
                Id = "railgun", Kind = WeaponKind.Firearm, SpriteId = "weapon.rifle", Damage = 100, DamageType = "bullet",
                Range = 2000, FireRate = 10, MagazineSize = 50, ProjectileSpeed = 100_000, MuzzleDistance = 20,
            },
        };

        var actors = new Dictionary<string, ActorDefinition>
        {
            ["player"] = new() { Id = "player", Faction = "player", Health = 100, Radius = 10, Speed = 190, Acceleration = 3400, Friction = 3000, HitStunDuration = 0 },
            ["guard"] = new()
            {
                Id = "guard", Faction = "enemy", Health = 100, Radius = 10, Speed = 85, RunSpeed = 175, WeaponId = "pistol",
                Ai = new AiDefinition { VisionRange = 430, VisionAngleDegrees = 120, ReactionTime = 0.3f, AimErrorDegrees = 0, TurnSpeedDegrees = 720 },
            },
            ["thug"] = new()
            {
                Id = "thug", Faction = "enemy", Health = 100, Radius = 10, Speed = 90, RunSpeed = 185, WeaponId = "bat",
                Ai = new AiDefinition { VisionRange = 400, ReactionTime = 0.2f, TurnSpeedDegrees = 720 },
            },
            ["brawler"] = new()
            {
                Id = "brawler", Faction = "enemy", Health = 100, Radius = 10, Speed = 90, RunSpeed = 190, Acceleration = 5000, Friction = 5000,
                Ai = new AiDefinition { VisionRange = 400, ReactionTime = 0.15f, TurnSpeedDegrees = 720 },
            },
            ["brute"] = new()
            {
                Id = "brute", Faction = "enemy", Health = 300, Radius = 14, KnockdownImmune = true,
                DamageMultipliers = new() { ["blunt"] = 0f, ["unarmed"] = 0f },
                Ai = new AiDefinition { Behavior = AiBehavior.Dormant },
            },
            ["dummy"] = new()
            {
                Id = "dummy", Faction = "enemy", Health = 100, Radius = 10,
                Ai = new AiDefinition { Behavior = AiBehavior.Dormant },
            },
        };

        var tiles = new Dictionary<string, TileDefinition>
        {
            ["floor"] = new() { Id = "floor", SpriteId = "tile.floor" },
            ["wall"] = new() { Id = "wall", SpriteId = "tile.wall", Layer = TileLayer.Wall, BlocksMovement = true, BlocksProjectiles = true, BlocksVision = true },
            ["glass"] = new()
            {
                Id = "glass", SpriteId = "tile.glass", Layer = TileLayer.Wall, BlocksMovement = true, BlocksProjectiles = true,
                Destructible = true, BreaksInto = "broken_glass",
            },
            ["broken_glass"] = new() { Id = "broken_glass", Layer = TileLayer.Wall, BlocksMovement = true },
        };

        var doors = new Dictionary<string, DoorDefinition>
        {
            ["door"] = new() { Id = "door", SpriteId = "door.wood" },
        };

        var modifiers = new Dictionary<string, ModifierDefinition>
        {
            ["swift"] = new() { Id = "swift", MoveSpeed = 1.5f },
            ["armored"] = new() { Id = "armored", DamageTaken = 0.5f },
        };

        var rules = new GameRules();
        configureRules?.Invoke(rules);
        return new DefinitionSet(weapons, actors, tiles, doors, modifiers, rules);
    }

    /// <summary>'.' floor, '#' wall, '=' glass, 'D' door. Positions are in tiles.</summary>
    public static LevelDefinition Level(string[] map, Vector2 player, float playerFacing = 0f, string? playerWeapon = null)
    {
        return new LevelDefinition
        {
            Id = "test",
            TileSize = Tile,
            Legend = new() { ["."] = "floor", ["#"] = "wall", ["="] = "glass" },
            DoorSymbols = new() { ["D"] = new LevelDoorSymbol { Tile = "floor", Door = "door" } },
            Map = map.ToList(),
            Player = new LevelPlayer { Definition = "player", Position = player, FacingDegrees = playerFacing, WeaponId = playerWeapon ?? "none" },
        };
    }

    /// <summary>An empty room of the given size (walls around).</summary>
    public static string[] Room(int width, int height)
    {
        var rows = new string[height];
        for (int y = 0; y < height; y++)
        {
            rows[y] = y == 0 || y == height - 1
                ? new string('#', width)
                : "#" + new string('.', width - 2) + "#";
        }

        return rows;
    }

    public static LevelActor Enemy(string definition, float x, float y, float facing = 180f, string? id = null, string? weapon = null, AiBehavior? behavior = null) =>
        new() { Id = id, Definition = definition, Position = new Vector2(x, y), FacingDegrees = facing, WeaponId = weapon, Behavior = behavior };

    public static GameWorld World(LevelDefinition level, DefinitionSet? definitions = null, ModifierDefinition? modifier = null) =>
        GameWorld.Create(level, definitions ?? Definitions(), seed: 1, modifier);

    public static Vector2 TileCenter(float x, float y) => new((x + 0.5f) * Tile, (y + 0.5f) * Tile);

    /// <summary>Steps the world with the same commands; "pressed" flags only apply to the first step.</summary>
    public static void Run(GameWorld world, float seconds, PlayerCommands commands = default)
    {
        int steps = (int)MathF.Round(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            world.Step(Dt, commands);
            commands.AttackPressed = false;
            commands.PickupOrThrowPressed = false;
            commands.InteractPressed = false;
        }
    }

    public static PlayerCommands Aim(GameWorld world, Vector2 target, bool attack = false) => new()
    {
        AimTarget = target,
        AttackPressed = attack,
    };

    public static T? Event<T>(GameWorld world) where T : GameEvent => world.Events.OfType<T>().FirstOrDefault();
}

/// <summary>Fluent helper to describe a test level.</summary>
internal sealed class LevelDefinitionBuilder
{
    private readonly LevelDefinition _level;

    public LevelDefinitionBuilder(string[] map, Vector2 player, float playerFacing = 0f, string? playerWeapon = null) =>
        _level = TestContent.Level(map, player, playerFacing, playerWeapon);

    public LevelDefinitionBuilder With(params LevelActor[] actors)
    {
        _level.Actors.AddRange(actors);
        return this;
    }

    public LevelDefinitionBuilder WithWeapon(string definition, float x, float y, int? ammo = null)
    {
        _level.Weapons.Add(new LevelWeapon { Definition = definition, Position = new Vector2(x, y), Ammo = ammo });
        return this;
    }

    public LevelDefinitionBuilder WithZone(string name, int x, int y, int width, int height)
    {
        _level.Zones[name] = new LevelZone { X = x, Y = y, Width = width, Height = height };
        return this;
    }

    public LevelDefinitionBuilder WithObjective(ObjectiveType type, string? zone = null, string? target = null, float duration = 0f, string? tag = null)
    {
        _level.Objectives.Add(new ObjectiveDefinition { Type = type, Zone = zone, Target = target, Duration = duration, Tag = tag, TextKey = type.ToString() });
        return this;
    }

    public LevelDefinitionBuilder WithTrigger(TriggerCondition on, string subject, params TriggerAction[] actions)
    {
        _level.Triggers.Add(new TriggerDefinition { On = on, Subject = subject, Actions = actions.ToList() });
        return this;
    }

    public LevelDefinitionBuilder WithItem(string id, float x, float y)
    {
        _level.Items.Add(new LevelItem { Id = id, Sprite = "item", Position = new Vector2(x, y) });
        return this;
    }

    public LevelDefinitionBuilder WithInteractable(string id, float x, float y)
    {
        _level.Interactables.Add(new LevelInteractable { Id = id, Sprite = "switch", Position = new Vector2(x, y) });
        return this;
    }

    public LevelDefinitionBuilder WithProp(float x, float y, float width, float height, bool blocksProjectiles = false)
    {
        _level.Props.Add(new LevelProp { Sprite = "prop", Position = new Vector2(x, y), Size = new Vector2(width, height), BlocksProjectiles = blocksProjectiles });
        return this;
    }

    public LevelDefinition Build() => _level;
}
