using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Core.Gameplay;

/// <summary>Completes a level's objectives one after the other.</summary>
public sealed class ObjectiveTracker
{
    private readonly IReadOnlyList<ObjectiveDefinition> _objectives;

    internal ObjectiveTracker(IReadOnlyList<ObjectiveDefinition> objectives) => _objectives = objectives;

    public int Count => _objectives.Count;
    public int CurrentIndex { get; private set; }
    public ObjectiveDefinition? Current => CurrentIndex < _objectives.Count ? _objectives[CurrentIndex] : null;
    public bool AllComplete => CurrentIndex >= _objectives.Count;

    /// <summary>Time spent on the current objective (used by Survive).</summary>
    public float Elapsed { get; private set; }

    internal void Update(GameWorld world, float dt)
    {
        if (world.Outcome != WorldOutcome.Playing) return;
        Elapsed += dt;
        while (Current is { } objective && IsComplete(world, objective))
        {
            world.AddEvent(new ObjectiveCompleted(CurrentIndex, objective));
            world.Triggers.OnObjectiveCompleted(CurrentIndex);
            CurrentIndex++;
            Elapsed = 0f;
        }
    }

    private bool IsComplete(GameWorld world, ObjectiveDefinition objective) => objective.Type switch
    {
        ObjectiveType.EliminateAll => !world.Actors.Any(actor => IsRemainingEnemy(world, actor, objective.Tag)),
        ObjectiveType.ReachZone => objective.Zone is { } zone && world.Zones.TryGetValue(zone, out var rect)
            && world.Player.IsAlive && rect.Contains(world.Player.Position)
            && (objective.Requires is null || world.Inventory.Has(objective.Requires)),
        ObjectiveType.Collect => objective.Target is { } target
            && (world.Inventory.Has(target) || world.Items.Any(item => item.Definition.Id == target && item.Collected)),
        ObjectiveType.Survive => world.Player.IsAlive && Elapsed >= objective.Duration,
        ObjectiveType.Activate => world.Interactables.Any(i => i.Definition.Id == objective.Target && i.Used),
        _ => false,
    };

    private static bool IsRemainingEnemy(GameWorld world, Actor actor, string? tag) =>
        actor.IsAlive
        && actor.Definition.CountsForObjectives
        && world.Player.IsHostileTo(actor)
        && (tag is null || actor.Tag == tag);
}
