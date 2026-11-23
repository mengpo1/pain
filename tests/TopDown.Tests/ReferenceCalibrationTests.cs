using System.Numerics;
using TopDown.Content;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;

namespace TopDown.Tests;

/// <summary>
/// The shipped action pack against the ranges measured on the reference gameplay video
/// (docs/analyse-video-hm.md). The ranges are estimates read from a 240p, 30 fps recording, not
/// official values: they keep the tuning in the observed window, nothing more.
/// </summary>
public class ReferenceCalibrationTests
{
    private const float Dt = 1f / 120f;
    private const float VideoFrame = 1001f / 30000f;
    /// <summary>Shoulder width of the pack's characters, in world units (docs/art/action.md).</summary>
    private const float BodyWidth = 22f;

    private static string RepositoryRoot
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null && !File.Exists(Path.Combine(directory, "TopDown.slnx")))
                directory = Path.GetDirectoryName(directory);
            return directory ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    private static readonly Lazy<ContentDatabase> ActionPack = new(() => Load());

    private static ContentDatabase Load(params string[] mods) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(RepositoryRoot, "Content"),
        ModsRoot = Path.Combine(RepositoryRoot, "Mods"),
        EnabledMods = mods,
    });

    /// <summary>A large empty room of the shipped pack; positions in tiles.</summary>
    private static GameWorld Arena(string? weapon = null, params LevelActor[] enemies) => Arena(ActionPack.Value.Definitions, weapon, enemies);

    private static GameWorld Arena(DefinitionSet definitions, string? weapon, params LevelActor[] enemies)
    {
        var rows = new List<string>();
        for (int y = 0; y < 12; y++) rows.Add(y is 0 or 11 ? new string('#', 40) : "#" + new string('.', 38) + "#");
        var level = new LevelDefinition
        {
            Id = "calibration",
            TileSize = 32,
            Legend = new() { ["."] = "floor_wood", ["#"] = "wall_white" },
            Map = rows,
            Player = new LevelPlayer { Definition = "player", Position = new Vector2(3, 5), WeaponId = weapon ?? LevelBuilder.NoWeapon },
            Actors = enemies.ToList(),
        };
        return GameWorld.Create(level, definitions, seed: 1);
    }

    private static LevelActor Dummy(float x, float y) =>
        new() { Id = "target", Definition = "thug", Position = new Vector2(x, y), FacingDegrees = 180f, WeaponId = LevelBuilder.NoWeapon, Behavior = AiBehavior.Dormant };

    private static Vector2 Tile(float x, float y) => new((x + 0.5f) * 32f, (y + 0.5f) * 32f);

    /// <summary>Lines of world shown on a 720p or 1080p screen once pixel art rounds the zoom.</summary>
    private static float VisibleLines(int screenHeight) =>
        screenHeight / MathF.Max(1f, MathF.Round(screenHeight / ActionPack.Value.Game.Camera.ViewHeight));

    // ------------------------------------------------------------------ movement (video 05:55–06:05)

    [Fact]
    public void The_player_starts_and_stops_within_two_video_frames()
    {
        GameWorld world = Arena();
        float speed = world.Player.Definition.Speed;
        var run = new PlayerCommands { Move = Vector2.UnitX, AimTarget = Tile(30, 5) };

        float start = 0f;
        while (world.Player.Velocity.Length() < speed * 0.95f && start < 1f) { world.Step(Dt, run); start += Dt; }
        Run(world, 0.3f, run);
        float stop = 0f;
        while (world.Player.Velocity.Length() > speed * 0.05f && stop < 1f) { world.Step(Dt, new PlayerCommands { AimTarget = Tile(30, 5) }); stop += Dt; }

        Assert.InRange(start, 0f, 2f * VideoFrame);
        Assert.InRange(stop, 0f, 2f * VideoFrame);
    }

    [Theory]
    [InlineData(720)]
    [InlineData(1080)]
    public void Running_crosses_the_screen_at_the_reference_pace(int screenHeight)
    {
        float speed = ActionPack.Value.Definitions.Actors["player"].Speed;

        // Reference: 155–185 video px/s for a 240 px high view, characters 10–15 px wide.
        Assert.InRange(speed / VisibleLines(screenHeight), 0.64f, 0.78f);
        Assert.InRange(speed / BodyWidth, 10f, 18f);
    }

    [Fact]
    public void The_camera_trails_a_running_player_like_the_reference()
    {
        CameraSettings camera = ActionPack.Value.Game.Camera;
        float speed = ActionPack.Value.Definitions.Actors["player"].Speed;

        // Steady lag of an exponential follow = speed / sharpness; reference: 15–25 px of a 240 px view.
        Assert.InRange(speed / camera.FollowSharpness / VisibleLines(1080), 0.06f, 0.105f);
        // Time to take up 95 % of a new speed; reference: 0.25–0.37 s.
        Assert.InRange(3f / camera.FollowSharpness, 0.25f, 0.40f);
    }

    // ------------------------------------------------------------------ firearms (01:49–01:51, 02:41.8)

    [Fact]
    public void The_automatic_rifle_fires_about_fifteen_rounds_a_second()
    {
        GameWorld world = Arena("rifle");
        int shots = 0;
        var hold = new PlayerCommands { AttackHeld = true, AttackPressed = true, AimTarget = Tile(30, 5) };
        for (int i = 0; i < 120; i++)
        {
            world.ClearEvents();
            world.Step(Dt, hold);
            hold.AttackPressed = false;
            shots += world.Events.OfType<ShotFired>().Count();
        }

        // One round every second video frame (24 → 19 in 8 frames): 14–16 per second.
        Assert.InRange(shots, 14, 16);
    }

    [Fact]
    public void The_shotgun_pumps_in_about_half_a_second()
    {
        float interval = 1f / ActionPack.Value.Definitions.Weapon("shotgun").FireRate;

        // Reference: 15 and 16 video frames between shots.
        Assert.InRange(interval, 14.5f * VideoFrame, 16.5f * VideoFrame);
    }

    // ------------------------------------------------------------------ bodies (04:17, 06:23, 10:08, 10:11)

    [Theory]
    [InlineData("bat", 0.8f, 1.7f)]
    [InlineData("pipe", 0.8f, 1.7f)]
    [InlineData("pistol", 1.2f, 3.5f)]
    [InlineData("shotgun", 1.2f, 3.5f)]
    [InlineData("rifle", 1.2f, 3.5f)]
    public void A_killed_body_is_thrown_like_in_the_reference(string weapon, float minBodies, float maxBodies)
    {
        // A fresh copy where the target cannot be left wounded: the reference only shows kills.
        DefinitionSet definitions = Load().Definitions;
        definitions.Actors["thug"].Wound = null;
        bool firearm = definitions.Weapon(weapon).IsFirearm;
        GameWorld world = Arena(definitions, weapon, Dummy(firearm ? 9f : 3.9f, 5));
        Actor target = world.FindActor("target")!;

        var attack = new PlayerCommands { AttackPressed = true, AttackHeld = true, AimTarget = target.Position };
        Vector2? deathPosition = null;
        for (int i = 0; i < 120 && deathPosition is null; i++)
        {
            world.Step(Dt, attack);
            attack.AttackPressed = false;
            attack.AttackHeld = false;
            if (!target.IsAlive) deathPosition = target.Position;
        }

        Assert.NotNull(deathPosition);
        Run(world, 1f, new PlayerCommands { AimTarget = target.Position });
        float thrown = Vector2.Distance(deathPosition!.Value, target.Position) / BodyWidth;
        Assert.InRange(thrown, minBodies, maxBodies);
    }

    // ------------------------------------------------------------------ walls (04:17.0, 10:10.0)

    [Theory]
    [InlineData("wall_white")]
    [InlineData("wall_brick")]
    [InlineData("wall_plaster")]
    [InlineData("glass")]
    public void Walls_are_as_thin_as_in_the_reference(string tile)
    {
        float? thickness = ActionPack.Value.Definitions.Tiles[tile].Thickness;

        // Reference: 2–5 px for characters 10–15 px wide, 0.2–0.45 of a body width.
        Assert.NotNull(thickness);
        Assert.InRange(thickness!.Value / BodyWidth, 0.2f, 0.45f);
    }

    [Fact]
    public void Every_door_of_the_action_levels_meets_its_walls()
    {
        foreach (LevelDefinition level in ActionPack.Value.Levels.Values)
        {
            GameWorld world = GameWorld.Create(level, ActionPack.Value.Definitions, seed: 1);
            TileMap map = world.Map;
            foreach (Door door in world.Doors)
            {
                // Just beyond each end of the closed door, the wall is solid: no gap to slip or shoot through.
                Vector2 along = Geo.FromAngle(door.ClosedAngle);
                Assert.True(map.IsSolidAt(door.Hinge - along * 2f), $"{level.Id}: wall missing at the hinge of the door at {door.Hinge}");
                Assert.True(map.IsSolidAt(door.Hinge + along * (door.Length + 2f)), $"{level.Id}: wall missing at the end of the door at {door.Hinge}");
            }
        }
    }

    // ------------------------------------------------------------------ doors (08:49.37–.47, 09:08.017–.050)

    [Fact]
    public void A_kicked_wooden_door_swings_open_within_three_video_frames()
    {
        var rows = new List<string>();
        for (int y = 0; y < 11; y++)
            rows.Add(y is 0 or 10 ? new string('#', 20) : "#........." + (y == 5 ? "D" : "#") + "........#");
        var level = new LevelDefinition
        {
            Id = "door_calibration",
            TileSize = 32,
            Legend = new() { ["."] = "floor_wood", ["#"] = "wall_white" },
            DoorSymbols = new() { ["D"] = new LevelDoorSymbol { Tile = "floor_wood", Door = "door_wood" } },
            Map = rows,
            Player = new LevelPlayer { Definition = "player", Position = new Vector2(7, 5), WeaponId = LevelBuilder.NoWeapon },
        };
        GameWorld world = GameWorld.Create(level, ActionPack.Value.Definitions, seed: 1);
        Door door = world.Doors.Single();
        var run = new PlayerCommands { Move = Vector2.UnitX, AimTarget = Tile(18, 5) };

        float? kicked = null, open = null;
        for (int i = 0; i < 240 && open is null; i++)
        {
            world.Step(Dt, run);
            float swung = MathF.Abs(door.Angle); // relative to the closed position
            if (kicked is null && swung > 0.01f) kicked = world.Time;
            if (swung >= Geo.DegToRad(85f)) open = world.Time;
        }

        Assert.NotNull(open);
        Assert.InRange(open!.Value - kicked!.Value, 0f, 3f * VideoFrame);
    }

    // ------------------------------------------------------------------ falls (01:12.406, 08:49.500)

    [Fact]
    public void A_floored_human_lies_on_the_floor_within_two_video_frames()
    {
        // Reference: standing on one frame, lying on the next. The rest of the clip (bounce, blood pool) may last.
        string[] falls = ["knockdown", "death.shot", "death.cut", "death.blunt"];
        var checkedClips = 0;
        foreach (string human in new[] { "player", "guard", "thug", "brute" })
        {
            foreach (string fall in falls)
            {
                AnimationDefinition clip = ActionPack.Value.Assets.Animations[$"actor.{human}.{fall}"];
                float? landing = clip.EventTime("thud", clip.Durations.Count);
                Assert.True(landing is not null, $"{human} {fall} has no thud");
                Assert.InRange(landing!.Value, 0f, 2f * VideoFrame);
                checkedClips++;
            }
        }

        Assert.Equal(16, checkedClips);
    }

    [Fact]
    public void A_floored_enemy_stands_up_within_three_video_frames()
    {
        GameWorld world = Arena(null, Dummy(10f, 5));
        Actor thug = world.FindActor("target")!;
        thug.KnockDown(0.5f);

        float rising = 0f;
        for (int i = 0; i < 240 && thug.State != ActorState.Active; i++)
        {
            world.Step(Dt, default);
            if (thug.State == ActorState.GettingUp) rising += Dt;
        }

        // Reference: lying, sitting, standing on three consecutive frames (10:13.667–.734, 11:35.867–.967).
        Assert.Equal(ActorState.Active, thug.State);
        Assert.InRange(rising, Dt, 3f * VideoFrame + Dt); // + one simulation step of rounding
    }

    [Theory]
    [InlineData("player")]
    [InlineData("guard")]
    [InlineData("thug")]
    [InlineData("brute")]
    public void The_get_up_animation_stands_before_the_character_can_act(string human)
    {
        AnimationDefinition clip = ActionPack.Value.Assets.Animations[$"actor.{human}.getup"];
        float standing = clip.Durations.Take(clip.Durations.Count - 1).Sum();

        Assert.InRange(standing, 0f, ActionPack.Value.Definitions.Actors[human].GetUpDuration + 0.001f);
    }

    // ------------------------------------------------------------------ executions (01:13–02:29)

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bare_handed_execution_kills_in_the_reference_window(bool mashing)
    {
        GameWorld world = Arena(null, Dummy(3.8f, 5));
        Actor victim = world.FindActor("target")!;
        victim.KnockDown(3f);

        world.Step(Dt, new PlayerCommands { InteractPressed = true, AimTarget = victim.Position });
        Assert.Equal(ActorState.Executing, world.Player.State);
        float elapsed = Dt;
        for (int i = 0; i < 360 && victim.IsAlive; i++)
        {
            world.Step(Dt, new PlayerCommands { AttackPressed = mashing, AimTarget = victim.Position });
            elapsed += Dt;
        }

        // Reference, mount to kill: 15, 20, 27 and 28 video frames (0.50–0.93 s), blows ~10 frames apart.
        Assert.False(victim.IsAlive);
        Assert.InRange(elapsed, 0.5f, 0.95f);
    }

    // ------------------------------------------------------------------ the thermal packs keep their own feel

    [Theory]
    [InlineData("noct_prototype", false)]
    [InlineData("noct_inspired", true)]
    public void The_thermal_packs_keep_their_previous_tuning(string mod, bool usesGuards)
    {
        DefinitionSet definitions = Load(mod).Definitions;
        ExecutionDefinition bareHanded = definitions.Weapon("unarmed").Execution;

        Assert.Equal(140f, definitions.Weapon("pistol").Knockback);
        Assert.Equal(1.25f, definitions.Weapon("shotgun").FireRate);
        Assert.Equal(300f, definitions.Weapon("shotgun").Knockback);
        Assert.Equal(220f, definitions.Weapon("pipe").Knockback);
        Assert.Equal(150f, definitions.Weapon("machete").Knockback);
        Assert.Equal(0.1f, bareHanded.WindUp);
        Assert.Equal(0.12f, bareHanded.StrikeInterval);
        Assert.Equal(0.6f, bareHanded.AutoStrikeDelay);
        Assert.Equal(0.09f, Load(mod).Game.Feel.FallDuration);
        if (mod == "noct_prototype") Assert.Equal(620f, Load(mod).Definitions.Doors["door_wood"].PushSpeedDegrees);
        Assert.False(definitions.Tiles["glass"].Thickness > 0f);
        if (usesGuards) Assert.False(definitions.Tiles["wall_plaster"].Thickness > 0f);
        if (usesGuards) Assert.Equal(0.4f, definitions.Actors["guard"].GetUpDuration);
    }

    // ------------------------------------------------------------------ reproducible scenes (tools/CalibrationScenes)

    [Fact]
    public void The_calibration_scenes_load_and_play()
    {
        ContentDatabase scenes = ContentLoader.Load(new ContentLoadOptions
        {
            ContentRoot = Path.Combine(RepositoryRoot, "Content"),
            ModsRoot = Path.Combine(RepositoryRoot, "tools", "CalibrationScenes"),
            EnabledMods = ["calibration", "calibration_measure"],
        });

        Assert.Empty(scenes.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Select(d => d.ToString()));
        Assert.Equal(0f, scenes.Theme.Post.Vignette);
        foreach (string id in new[] { "cal_bat", "cal_bare_hands", "cal_rifle", "cal_run", "cal_door" })
        {
            GameWorld world = GameWorld.Create(scenes.Levels[id], scenes.Definitions, seed: 1);
            for (int i = 0; i < 120; i++) world.Step(Dt, default);
            Assert.True(world.Player.IsAlive, id);
        }
    }

    private static void Run(GameWorld world, float seconds, PlayerCommands commands)
    {
        for (int i = 0; i < (int)MathF.Round(seconds / Dt); i++) world.Step(Dt, commands);
    }
}
