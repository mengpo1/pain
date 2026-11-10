using System.Globalization;
using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Core.Gameplay;

/// <summary>Simple level scripting: "when X happens, do Y" described in level files.</summary>
public sealed class TriggerSystem
{
    private readonly List<TriggerState> _triggers;
    private readonly Queue<(TriggerCondition Condition, string Subject)> _signals = new();

    internal TriggerSystem(IEnumerable<TriggerDefinition> triggers) =>
        _triggers = triggers.Select(definition => new TriggerState(definition)).ToList();

    internal void OnActivated(string interactableId) => _signals.Enqueue((TriggerCondition.Activated, interactableId));

    internal void OnObjectiveCompleted(int index) =>
        _signals.Enqueue((TriggerCondition.ObjectiveCompleted, index.ToString(CultureInfo.InvariantCulture)));

    internal void Update(GameWorld world)
    {
        foreach (TriggerState trigger in _triggers)
        {
            if (trigger.Definition.On != TriggerCondition.EnterZone) continue;
            bool inside = world.Player.IsAlive
                && world.Zones.TryGetValue(trigger.Definition.Subject, out var zone)
                && zone.Contains(world.Player.Position);
            if (inside && !trigger.PlayerInside) Fire(world, trigger);
            trigger.PlayerInside = inside;
        }

        while (_signals.TryDequeue(out var signal))
        {
            foreach (TriggerState trigger in _triggers)
            {
                if (trigger.Definition.On == signal.Condition && trigger.Definition.Subject == signal.Subject)
                    Fire(world, trigger);
            }
        }
    }

    private static void Fire(GameWorld world, TriggerState trigger)
    {
        if (trigger.Fired && trigger.Definition.Once) return;
        trigger.Fired = true;
        foreach (TriggerAction action in trigger.Definition.Actions) Execute(world, action);
    }

    private static void Execute(GameWorld world, TriggerAction action)
    {
        switch (action.Type)
        {
            case TriggerActionType.ActivateTag:
                foreach (Actor actor in Tagged(world, action.Tag)) actor.Brain?.Activate();
                break;
            case TriggerActionType.AlertTag:
                foreach (Actor actor in Tagged(world, action.Tag)) actor.Brain?.AlertTo(world.Player);
                break;
            case TriggerActionType.Message when action.TextKey is not null:
                world.AddEvent(new MessageRequested(action.TextKey, action.Duration));
                break;
            case TriggerActionType.UnlockDoor:
                foreach (Door door in world.Doors.Where(d => d.LevelId == action.Door)) door.Locked = false;
                break;
            case TriggerActionType.PlaySound when action.Sound is not null:
                world.AddEvent(new SoundRequested(action.Sound, null));
                break;
        }
    }

    private static IEnumerable<Actor> Tagged(GameWorld world, string? tag) =>
        world.Actors.Where(actor => actor.IsAlive && tag is not null && actor.Tag == tag);

    private sealed class TriggerState(TriggerDefinition definition)
    {
        public TriggerDefinition Definition { get; } = definition;
        public bool Fired { get; set; }
        public bool PlayerInside { get; set; }
    }
}
