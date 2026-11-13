using System.Numerics;
using TopDown.Content;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Bugs found during code review, each reproduced then fixed.</summary>
public class RegressionTests
{
    private static Actor Other(GameWorld world, string definition) => world.Actors.First(a => a.Definition.Id == definition);

    [Fact]
    public void Enemy_investigating_a_broken_window_does_not_get_stuck()
    {
        // The player waits in a closed pocket on the right, out of sight.
        string[] map =
        [
            "####################",
            "#.........=......#.#",
            "#.........=......#.#",
            "#.........=......#.#",
            "#................###",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(18, 2), playerWeapon: "pistol")
            .With(TestContent.Enemy("thug", 4, 4, facing: 180, weapon: "none")).Build());
        Actor enemy = Other(world, "thug");

        // Break the window: its noise comes from an unwalkable tile.
        world.Combat.BreakTile(10, 2);
        Run(world, 1f);
        Assert.Equal(AiState.Investigate, enemy.Brain!.State);

        Run(world, 6f);
        Assert.NotEqual(AiState.Investigate, enemy.Brain.State);
    }

    [Fact]
    public void Silent_kills_do_not_alert_allies_behind_walls()
    {
        var definitions = Definitions();
        definitions.Actors["thug"].Ai.AlertAlliesRadius = 400;
        string[] map =
        [
            "##############",
            "#.....#......#",
            "#.....#......#",
            "##############",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(1, 1), playerWeapon: "knife")
            .With(TestContent.Enemy("thug", 2, 1, facing: 0), TestContent.Enemy("thug", 9, 1, facing: 0, id: "neighbour")).Build(), definitions);
        Actor victim = world.Actors.First(a => a.Definition.Id == "thug" && a.LevelId is null);

        world.Combat.ApplyHit(victim, new Hit(100f, "sharp", false, 0f, Vector2.UnitX, 0f, world.Player, HitMethod.Melee));
        Run(world, 0.1f);

        Assert.False(victim.IsAlive);
        Assert.Null(world.FindActor("neighbour")!.Brain!.Target);
    }

    [Fact]
    public void Thrown_weapons_do_not_pass_through_closed_doors()
    {
        string[] map =
        [
            "###########",
            "#....#....#",
            "#....D....#",
            "#....#....#",
            "###########",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(3, 2), playerWeapon: "knife").With(TestContent.Enemy("dummy", 5.42f, 2.5f)).Build());

        Run(world, 1f, new PlayerCommands { PickupOrThrowPressed = true, AimTarget = TileCenter(8, 2) });

        Assert.True(Other(world, "dummy").IsAlive);
    }

    [Fact]
    public void Thrown_weapons_fly_through_broken_windows()
    {
        string[] map =
        [
            "####################",
            "#.........=........#",
            "#.........=........#",
            "#.........=........#",
            "####################",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(7, 2), playerWeapon: "knife").With(TestContent.Enemy("dummy", 13, 2)).Build());

        Run(world, 1f, new PlayerCommands { PickupOrThrowPressed = true, AimTarget = TileCenter(13, 2) });

        Assert.False(Other(world, "dummy").IsAlive);
        Assert.Equal("broken_glass", world.Map.Get(10, 2).Id);
    }

    [Fact]
    public void Enemies_do_not_shoot_through_their_knocked_down_allies()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 5), new Vector2(2, 2))
            .With(TestContent.Enemy("guard", 12, 2, facing: 180), TestContent.Enemy("dummy", 7, 2)).Build());
        Actor dummy = Other(world, "dummy");
        Actor guard = Other(world, "guard");
        dummy.KnockDown(10f);

        for (int i = 0; i < 120; i++)
        {
            guard.Position = TileCenter(12, 2);
            world.Step(Dt, default);
        }

        Assert.True(dummy.IsAlive);
    }

    [Fact]
    public void Reaction_time_applies_again_when_the_player_reappears()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(20, 7), new Vector2(2, 2)).With(TestContent.Enemy("guard", 12, 2, facing: 180)).Build());
        Actor guard = Other(world, "guard");
        world.Player.Health = float.MaxValue;
        Run(world, 0.6f);
        Assert.Contains(world.Events, e => e is ShotFired);

        // A wall appears between them (with a way around, so the guard keeps chasing), then vanishes.
        for (int y = 1; y <= 3; y++) world.Map.Set(7, y, definitions: world.Definitions, "wall");
        Run(world, 0.7f);
        Assert.Equal(AiState.Chase, guard.Brain!.State);
        Assert.False(guard.Brain.CanSeeTarget);
        for (int y = 1; y <= 3; y++) world.Map.Set(7, y, definitions: world.Definitions, "floor");
        world.ClearEvents();

        Run(world, 0.1f);
        Assert.True(guard.Brain.CanSeeTarget);
        Assert.DoesNotContain(world.Events, e => e is ShotFired);
    }

    [Fact]
    public void Locked_doors_block_straight_walking()
    {
        string[] map =
        [
            "###########",
            "#....#....#",
            "#....D....#",
            "#....#....#",
            "###########",
        ];
        LevelDefinition level = Level(map, new Vector2(2, 2));
        level.DoorSymbols["D"].Locked = true;
        GameWorld world = World(level);

        Assert.False(world.Physics.CanWalkStraight(TileCenter(2, 2), TileCenter(8, 2), 10f));
    }

    [Fact]
    public void Door_knockdown_pushes_the_victim()
    {
        string[] map =
        [
            "###########",
            "#....#....#",
            "#....D....#",
            "#....#....#",
            "###########",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(3.5f, 2)).With(TestContent.Enemy("dummy", 5.9f, 2.3f)).Build());
        Actor victim = Other(world, "dummy");
        Vector2 start = victim.Position;

        Run(world, 1f, new PlayerCommands { Move = Vector2.UnitX });

        Assert.Equal(ActorState.KnockedDown, victim.State);
        Assert.True(Vector2.Distance(start, victim.Position) > 4f, $"moved {Vector2.Distance(start, victim.Position)}");
    }

    [Fact]
    public void Melee_hits_a_standing_enemy_before_a_body_on_the_floor()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat")
            .With(TestContent.Enemy("dummy", 2.7f, 2, id: "down"), TestContent.Enemy("dummy", 2.95f, 2.35f, id: "up")).Build());
        world.FindActor("down")!.KnockDown(10f);

        Run(world, 0.4f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.False(world.FindActor("up")!.IsAlive);
        Assert.True(world.FindActor("down")!.IsAlive);
    }

    [Fact]
    public void Weapons_cannot_be_picked_up_through_a_closed_door()
    {
        string[] map =
        [
            "###########",
            "#....#....#",
            "#....D....#",
            "#....#....#",
            "###########",
        ];
        GameWorld world = World(new LevelDefinitionBuilder(map, new Vector2(5.15f, 2.6f)).WithWeapon("bat", 5.85f, 2.6f).Build());

        Run(world, 0.05f, new PlayerCommands { PickupOrThrowPressed = true });

        Assert.Null(world.Player.Weapon);
    }

    [Fact]
    public void Enemies_immune_to_executions_cannot_be_executed()
    {
        var definitions = Definitions();
        definitions.Actors["dummy"].DamageMultipliers["execution"] = 0f;
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2)).With(TestContent.Enemy("dummy", 2.8f, 2)).Build(), definitions);
        Other(world, "dummy").KnockDown(5f);

        Assert.Null(world.Combat.FindExecutionTarget(world.Player));
    }

    [Fact]
    public void Early_clicks_are_remembered_briefly()
    {
        GameWorld world = World(new LevelDefinitionBuilder(Room(10, 5), new Vector2(2, 2), playerWeapon: "bat").Build());
        Run(world, 0.3f, Aim(world, TileCenter(6, 2), attack: true));
        Assert.Single(world.Events.OfType<MeleeSwing>());

        // Click 0.1 s before the end of the 0.4 s cooldown: the swing happens as soon as possible.
        Run(world, 0.2f, Aim(world, TileCenter(6, 2), attack: true));

        Assert.Equal(2, world.Events.OfType<MeleeSwing>().Count());
    }

    [Fact]
    public void Map_keys_that_differ_only_by_case_are_different_tiles()
    {
        using var content = new TempContent();
        content.Write("Content/Base/levels/one.json", """
            {
              "legend": { "#": "wall", ".": "floor", "W": "wall", "w": "floor" },
              "map": [ "#####", "#.wW#", "#####" ],
              "player": { "position": [1, 1] },
              "objectives": [ { "type": "survive", "duration": 1 } ]
            }
            """);

        ContentDatabase database = content.Load();

        Assert.False(database.Diagnostics.HasErrors, string.Join("\n", database.Diagnostics.Items));
        GameWorld world = GameWorld.Create(database.Levels["one"], database.Definitions);
        Assert.Equal("floor", world.Map.Get(2, 1).Id);
        Assert.Equal("wall", world.Map.Get(3, 1).Id);
    }

    [Fact]
    public void Text_keys_that_differ_only_by_case_are_different_texts()
    {
        using var content = new TempContent();
        content.Write("Content/Base/locales/en.json", """{ "game.title": "Test", "objective.kill": "Kill", "menu.OK": "Upper", "menu.ok": "Lower" }""");

        ContentDatabase database = content.Load();
        Localizer texts = database.CreateLocalizer("en");

        Assert.Equal("Upper", texts["menu.OK"]);
        Assert.Equal("Lower", texts["menu.ok"]);
    }

    [Fact]
    public void Duplicate_keys_are_reported_instead_of_crashing()
    {
        using var content = new TempContent();
        content.Write("Content/Base/locales/en.json", """{ "game.title": "Test", "game.title": "Again" }""");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Errors, d => d.Source.EndsWith("en.json"));
    }

    [Fact]
    public void Non_text_ids_are_reported_instead_of_crashing()
    {
        using var content = new TempContent();
        content.Write("Content/Base/weapons/bad.json", """{ "id": 5, "kind": "melee" }""");

        ContentDatabase database = content.Load();

        Assert.Contains(database.Diagnostics.Errors, d => d.Source.EndsWith("bad.json") && d.Message.Contains("no 'id'"));
    }

    [Fact]
    public void Mods_in_a_dependency_cycle_are_not_loaded()
    {
        using var content = new TempContent();
        content.Mod("A", "mod_a", dependencies: "\"mod_b\"");
        content.Mod("B", "mod_b", dependencies: "\"mod_a\"");

        ContentDatabase database = content.Load("mod_a", "mod_b");

        Assert.Equal(["base"], database.LoadedPacks.Select(p => p.Manifest.Id));
        Assert.Contains(database.Diagnostics.Errors, d => d.Message.Contains("Circular"));
    }

    [Fact]
    public void Plural_forms_never_mix_languages()
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["kills.one"] = "{count} kill", ["kills.other"] = "{count} kills", ["enemy.one"] = "{count} enemy", ["enemy.other"] = "{count} enemies" },
            ["fr"] = new Dictionary<string, string> { ["kills.other"] = "{count} éliminations" },
        };
        var texts = new Localizer(tables, "fr", "en");

        // French has only "other": use it rather than the English singular.
        Assert.Equal("1 éliminations", texts.Format("kills", ("count", 1)));
        // Missing in French: English text with English rules (0 is plural in English).
        Assert.Equal("0 enemies", texts.Format("enemy", ("count", 0)));
    }

    [Fact]
    public void Positions_on_half_tiles_are_checked_against_the_right_tile()
    {
        using var content = new TempContent();
        content.Write("Content/Base/levels/one.json", """
            {
              "legend": { "#": "wall", ".": "floor" },
              "map": [ "######", "#..#.#", "######" ],
              "player": { "position": [2.5, 1] },
              "objectives": [ { "type": "survive", "duration": 1 } ]
            }
            """);

        ContentDatabase database = content.Load();

        // 2.5 lies in tile 3 (a wall), as LevelDefinition.ToWorld computes it.
        Assert.Contains(database.Diagnostics.Warnings, d => d.Source == "level:one" && d.Message.Contains("inside a solid tile"));
    }

    [Fact]
    public void Double_doors_drawn_in_a_horizontal_wall_are_horizontal()
    {
        string[] map =
        [
            "########",
            "#......#",
            "###DD###",
            "#......#",
            "########",
        ];
        GameWorld world = World(Level(map, new Vector2(1, 1)));

        Assert.All(world.Doors, door => Assert.True(MathF.Abs(MathF.Sin(door.ClosedAngle)) < 0.01f, "door should be horizontal"));
    }
}

internal static class TileMapTestExtensions
{
    public static void Set(this TileMap map, int x, int y, DefinitionSet definitions, string tile) => map.Set(x, y, definitions.Tile(tile));
}
