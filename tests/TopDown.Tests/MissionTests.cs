using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using static TopDown.Tests.TestContent;

namespace TopDown.Tests;

/// <summary>Missions described in data: transmissions, items found in containers, spawns, extraction.</summary>
public class MissionTests
{
    /// <summary>Find the battery in a locker, then reach the extraction zone with it.</summary>
    private static LevelDefinition Mission()
    {
        LevelDefinition level = new LevelDefinitionBuilder(TestContent.Room(24, 9), new Vector2(3, 4))
            .WithZone("extraction", 1, 1, 2, 2)
            .WithObjective(ObjectiveType.Collect, target: "battery")
            .WithObjective(ObjectiveType.ReachZone, zone: "extraction")
            .Build();
        level.Objectives[1].Requires = "battery";
        level.Interactables.Add(new LevelInteractable
        {
            Id = "locker", Sprite = "prop.locker", Position = new Vector2(10, 4),
            Loot = [new LootEntry { Item = "battery", NameKey = "item.battery" }],
        });
        level.Triggers.Add(new TriggerDefinition
        {
            On = TriggerCondition.Start,
            Actions = [new TriggerAction { Type = TriggerActionType.Transmission, TextKey = "tx.start", Duration = 4f }],
        });
        level.Triggers.Add(new TriggerDefinition
        {
            On = TriggerCondition.Collected, Subject = "battery",
            Actions =
            [
                new TriggerAction { Type = TriggerActionType.Transmission, TextKey = "tx.battery" },
                new TriggerAction { Type = TriggerActionType.Spawn, Definition = "brawler", Position = new Vector2(20, 4), Tag = "return", Alert = true },
            ],
        });
        return level;
    }

    private static void Interact(GameWorld world) => Run(world, 0.02f, new PlayerCommands { InteractPressed = true });

    [Fact]
    public void A_transmission_opens_the_mission()
    {
        GameWorld world = World(Mission());
        world.Step(Dt, default);

        TransmissionRequested transmission = Assert.Single(world.Events.OfType<TransmissionRequested>());
        Assert.Equal("tx.start", transmission.TextKey);
        Assert.Equal(4f, transmission.Duration);
    }

    [Fact]
    public void Searching_the_locker_completes_the_collect_objective_and_triggers_the_return()
    {
        GameWorld world = World(Mission());
        world.Player.Position = world.Level.ToWorld(new Vector2(9.4f, 4));
        Run(world, 0.1f);
        world.ClearEvents();

        Interact(world);
        Run(world, 0.05f);

        Assert.True(world.Inventory.Has("battery"));
        Assert.Equal(1, world.Objectives.CurrentIndex);
        Assert.Contains(world.Events.OfType<TransmissionRequested>(), t => t.TextKey == "tx.battery");
        Actor spawned = Assert.Single(world.Events.OfType<ActorSpawned>()).Actor;
        Assert.Equal("return", spawned.Tag);
        Assert.Equal(AiState.Chase, spawned.Brain!.State);
    }

    [Fact]
    public void The_extraction_only_counts_with_the_item()
    {
        LevelDefinition level = Mission();
        // Extraction first in the list, so only "requires" can hold it back.
        level.Objectives.Reverse();
        GameWorld world = World(level);
        world.Player.Position = world.Level.ToWorld(new Vector2(1.5f, 1.5f));

        Run(world, 0.5f);
        Assert.Equal(0, world.Objectives.CurrentIndex);
        Assert.Equal(WorldOutcome.Playing, world.Outcome);
    }

    [Fact]
    public void Reaching_the_extraction_alive_with_the_battery_wins()
    {
        GameWorld world = World(Mission());
        world.Player.Position = world.Level.ToWorld(new Vector2(9.4f, 4));
        Run(world, 0.05f);
        Interact(world);
        world.Player.Position = world.Level.ToWorld(new Vector2(1.5f, 1.5f));

        Run(world, 0.2f);

        Assert.True(world.Objectives.AllComplete);
        Assert.Equal(WorldOutcome.Victory, world.Outcome);
    }

    [Fact]
    public void A_restart_brings_everything_back()
    {
        LevelDefinition level = Mission();
        GameWorld first = World(level);
        first.Player.Position = first.Level.ToWorld(new Vector2(9.4f, 4));
        Run(first, 0.05f);
        Interact(first);
        first.Combat.ApplyHit(first.Player, new Hit(1000f, "bullet", false, 0f, Vector2.UnitX, 0f, null, HitMethod.Firearm));
        Assert.Equal(WorldOutcome.Defeat, first.Outcome);

        // Restarting is building the world again from the same level.
        GameWorld again = World(level);

        Assert.Equal(WorldOutcome.Playing, again.Outcome);
        Assert.False(again.Inventory.Has("battery"));
        Assert.True(again.Interactables.Single().Available);
        Assert.Equal(0, again.Objectives.CurrentIndex);
        Assert.True(again.Player.IsAlive);
        Assert.Equal(again.Player.Definition.Health, again.Player.Health);
    }
}
