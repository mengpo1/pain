using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.Gameplay;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

public enum WorldOutcome
{
    Playing,
    Victory,
    Defeat,
}

/// <summary>
/// The simulation of one level. It knows nothing about rendering, audio or input devices:
/// feed it <see cref="PlayerCommands"/> with a fixed time step and read <see cref="Events"/>.
/// </summary>
public sealed class GameWorld
{
    private readonly List<Actor> _actors = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<WorldWeapon> _weapons = new();
    private readonly List<WorldWeapon> _pendingWeapons = new();
    private readonly List<Projectile> _pendingProjectiles = new();
    private readonly List<Door> _doors = new();
    private readonly List<Prop> _props = new();
    private readonly List<ItemPickup> _items = new();
    private readonly List<Interactable> _interactables = new();
    private readonly Dictionary<string, RectF> _zones = new();
    private readonly List<GameEvent> _events = new();
    private readonly List<Noise> _noises = new();
    private bool[] _propBlocked = [];
    private int _nextActorId = 1;

    internal GameWorld(LevelDefinition level, DefinitionSet definitions, TileMap map, ModifierDefinition modifier, int seed)
    {
        Level = level;
        Definitions = definitions;
        Map = map;
        Modifier = modifier;
        Random = new Random(seed);
        Physics = new Physics(this);
        Combat = new CombatSystem(this);
        DoorLogic = new DoorSystem(this);
        Objectives = new ObjectiveTracker(level.Objectives);
        Score = new ScoreTracker(definitions.Rules.Score);
        Triggers = new TriggerSystem(level.Triggers);
        Player = null!;
    }

    /// <summary>Builds a fresh world from a level. Calling it again is how a level restarts.</summary>
    public static GameWorld Create(LevelDefinition level, DefinitionSet definitions, int seed = 1, ModifierDefinition? modifier = null) =>
        LevelBuilder.Build(level, definitions, modifier ?? ModifierDefinition.None, seed);

    public LevelDefinition Level { get; }
    public DefinitionSet Definitions { get; }
    public GameRules Rules => Definitions.Rules;
    public ModifierDefinition Modifier { get; }
    public TileMap Map { get; }
    public Physics Physics { get; }
    public CombatSystem Combat { get; }
    internal DoorSystem DoorLogic { get; }
    public ObjectiveTracker Objectives { get; }
    public ScoreTracker Score { get; }
    public TriggerSystem Triggers { get; }
    public Pathfinder Pathfinder { get; private set; } = null!;
    public Random Random { get; }

    public float Time { get; private set; }
    public WorldOutcome Outcome { get; private set; } = WorldOutcome.Playing;
    public Actor Player { get; private set; }

    public IReadOnlyList<Actor> Actors => _actors;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public IReadOnlyList<WorldWeapon> Weapons => _weapons;
    public IReadOnlyList<Door> Doors => _doors;
    public IReadOnlyList<Prop> Props => _props;
    public IReadOnlyList<ItemPickup> Items => _items;
    public IReadOnlyList<Interactable> Interactables => _interactables;
    public IReadOnlyDictionary<string, RectF> Zones => _zones;

    /// <summary>Events produced since the last <see cref="ClearEvents"/>.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>Floor used when a destructible tile has no replacement.</summary>
    internal TileDefinition FallbackFloor { get; } = new() { Id = "rubble" };

    public void ClearEvents() => _events.Clear();

    public void Step(float dt, in PlayerCommands commands)
    {
        if (dt <= 0f) return;
        Time += dt;

        foreach (Actor actor in _actors)
        {
            actor.PreviousPosition = actor.Position;
            actor.PreviousFacing = actor.Facing;
            actor.TickState(dt);
        }

        ApplyPlayerCommands(commands);
        DeliverNoises();
        foreach (Actor actor in _actors) actor.Brain?.Update(dt);

        Combat.UpdateMeleeAttacks();
        Combat.UpdateExecutions();

        foreach (Actor actor in _actors) Physics.MoveActor(actor, dt);
        Physics.SeparateActors();
        DoorLogic.Update(dt);

        FlushPending();
        Combat.UpdateProjectiles(dt);
        Combat.UpdateWorldWeapons(dt);
        FlushPending();
        _projectiles.RemoveAll(p => !p.Alive);

        CollectItems();
        Triggers.Update(this);
        Objectives.Update(this, dt);
        Score.Update(dt);

        if (Outcome == WorldOutcome.Playing && Objectives.AllComplete && Objectives.Count > 0)
        {
            Outcome = WorldOutcome.Victory;
            AddEvent(new LevelCompleted());
        }
    }

    private void ApplyPlayerCommands(in PlayerCommands commands)
    {
        Actor player = Player;
        if (!player.IsAlive || Outcome != WorldOutcome.Playing)
        {
            player.MoveIntent = Vector2.Zero;
            return;
        }

        Vector2 move = Geo.ClampLength(commands.Move, 1f);
        if (player.State == ActorState.Executing)
        {
            if (Rules.Execution.CancelByMoving && move != Vector2.Zero) player.EndExecution();
            else return;
        }

        player.MoveIntent = move;
        player.MoveSpeed = player.Definition.Speed;

        Vector2 aim = commands.AimTarget - player.Position;
        if (player.CanAct && aim.LengthSquared() > 1f) player.Facing = Geo.ToAngle(aim);

        bool automatic = Combat.CurrentWeapon(player).Automatic;
        if (commands.AttackPressed || (commands.AttackHeld && automatic)) Combat.TryAttack(player);
        if (commands.PickupOrThrowPressed) Combat.PickupOrThrow(player);
        if (commands.InteractPressed) Combat.TryInteract(player);
    }

    public Actor? FindActor(string levelId) => _actors.FirstOrDefault(a => a.LevelId == levelId);

    internal void AddEvent(GameEvent gameEvent) => _events.Add(gameEvent);

    internal void AddWeapon(WorldWeapon weapon) => _pendingWeapons.Add(weapon);

    internal void AddProjectile(Projectile projectile) => _pendingProjectiles.Add(projectile);

    internal void RemoveWeapon(WorldWeapon weapon)
    {
        if (!_weapons.Remove(weapon)) _pendingWeapons.Remove(weapon);
    }

    /// <summary>Called once the level content is in place.</summary>
    internal void FinishBuilding()
    {
        FlushPending();
        _propBlocked = new bool[Map.Width * Map.Height];
        foreach (Prop prop in _props)
        {
            if (!prop.Definition.BlocksMovement) continue;
            for (int y = 0; y < Map.Height; y++)
            {
                for (int x = 0; x < Map.Width; x++)
                {
                    if (prop.Bounds.Intersects(Map.TileRect(x, y))) _propBlocked[y * Map.Width + x] = true;
                }
            }
        }

        Pathfinder = new Pathfinder(Map, IsWalkableTile);
    }

    /// <summary>Walkable for the AI: no solid tile, no solid prop, no locked door.</summary>
    private bool IsWalkableTile(int x, int y)
    {
        if (Map.Blocks(x, y, Blocking.Movement) || _propBlocked[y * Map.Width + x]) return false;
        foreach (Door door in _doors)
        {
            if (!door.Locked) continue;
            Vector2 middle = door.Hinge + Geo.FromAngle(door.ClosedAngle) * (door.Length * 0.5f);
            if (Map.TileAt(middle) == (x, y)) return false;
        }

        return true;
    }

    private void FlushPending()
    {
        _weapons.AddRange(_pendingWeapons);
        _pendingWeapons.Clear();
        _projectiles.AddRange(_pendingProjectiles);
        _pendingProjectiles.Clear();
    }

    internal Actor SpawnActor(ActorDefinition definition, bool isPlayer, Vector2 position, float facing)
    {
        var actor = new Actor(_nextActorId++, definition, isPlayer, position, facing);
        _actors.Add(actor);
        if (isPlayer) Player = actor;
        return actor;
    }

    internal void AddDoor(Door door) => _doors.Add(door);
    internal void AddProp(Prop prop) => _props.Add(prop);
    internal void AddItem(ItemPickup item) => _items.Add(item);
    internal void AddInteractable(Interactable interactable) => _interactables.Add(interactable);
    internal void AddZone(string name, RectF zone) => _zones[name] = zone;

    /// <summary>A sound that enemies can hear. Radius 0 is silent.</summary>
    public void EmitNoise(Vector2 position, float radius, Actor? source)
    {
        if (radius <= 0f) return;
        _noises.Add(new Noise(position, radius, source));
        AddEvent(new NoiseMade(position, radius));
    }

    private void DeliverNoises()
    {
        if (_noises.Count == 0) return;
        foreach (Actor actor in _actors)
        {
            if (actor.Brain is null || !actor.IsAlive) continue;
            foreach (Noise noise in _noises)
            {
                if (noise.Source == actor) continue;
                float radius = noise.Radius * actor.Definition.Ai.HearingMultiplier;
                if (Vector2.DistanceSquared(actor.Position, noise.Position) <= radius * radius)
                    actor.Brain.OnNoise(noise.Position, noise.Source);
            }
        }

        _noises.Clear();
    }

    private void CollectItems()
    {
        if (!Player.IsAlive) return;
        foreach (ItemPickup item in _items)
        {
            if (item.Collected) continue;
            if (Vector2.Distance(item.Position, Player.Position) <= Player.Radius + 10f)
            {
                item.Collected = true;
                AddEvent(new ItemCollected(Player, item));
            }
        }
    }

    internal void Activate(Interactable interactable)
    {
        if (!interactable.Available) return;
        interactable.Used = true;
        AddEvent(new Activated(interactable));
        Triggers.OnActivated(interactable.Definition.Id);
    }

    internal void OnActorKilled(Actor victim, in Hit hit)
    {
        if (hit.Source is { IsPlayer: true }) Score.OnKill(hit.Method);
        if (victim.IsPlayer && Outcome == WorldOutcome.Playing)
        {
            Outcome = WorldOutcome.Defeat;
            AddEvent(new PlayerDied(victim));
        }
    }

    private readonly record struct Noise(Vector2 Position, float Radius, Actor? Source);
}
