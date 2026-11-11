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
    private float _attackBuffer;
    private float _pickupBuffer;
    private float _interactBuffer;
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

    /// <summary>What the player carries (spare ammunition, medkits, mission items).</summary>
    public Inventory Inventory { get; } = new();
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

        Combat.UpdateReloads(dt);
        ApplyPlayerCommands(commands, dt);
        DeliverNoises();
        foreach (Actor actor in _actors) actor.Brain?.Update(dt);

        Combat.UpdateMeleeAttacks();
        Combat.UpdateExecutions(dt);
        Combat.UpdateWounds();
        Combat.UpdateLeaps(dt);

        foreach (Actor actor in _actors) Physics.MoveActor(actor, dt);
        Combat.ResolveLeapContacts();
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

    private void ApplyPlayerCommands(in PlayerCommands commands, float dt)
    {
        Actor player = Player;
        // Presses slightly too early (end of a swing, target not yet down) are kept briefly.
        float buffer = Rules.Combat.InputBuffer;
        _attackBuffer = commands.AttackPressed ? buffer : MathF.Max(0f, _attackBuffer - dt);
        _pickupBuffer = commands.PickupOrThrowPressed ? buffer : MathF.Max(0f, _pickupBuffer - dt);
        _interactBuffer = commands.InteractPressed ? buffer : MathF.Max(0f, _interactBuffer - dt);
        if (!player.IsAlive || Outcome != WorldOutcome.Playing)
        {
            player.MoveIntent = Vector2.Zero;
            return;
        }

        Vector2 move = Geo.ClampLength(commands.Move, 1f);
        if (player.State == ActorState.Executing)
        {
            // During an execution the attack button delivers the blows.
            if (_attackBuffer > 0f && !player.ExecutionFinished)
            {
                player.StrikeRequested = true;
                _attackBuffer = 0f;
            }

            if (!(Rules.Execution.CancelByMoving && move != Vector2.Zero && !player.ExecutionFinished)) return;
            player.EndExecution();
        }

        player.MoveIntent = move;
        player.MoveSpeed = UpdateRunning(player, commands.Run && move != Vector2.Zero, dt);

        Vector2 aim = commands.AimTarget - player.Position;
        if (player.CanAct && aim.LengthSquared() > 1f) player.Facing = Geo.ToAngle(aim);

        if (_interactBuffer > 0f && Combat.TryInteract(player))
        {
            _interactBuffer = 0f;
            _attackBuffer = 0f;
            return;
        }

        if (commands.ReloadPressed) Combat.TryReload(player);
        if (commands.HealPressed) TryHeal(player);

        bool automatic = Combat.CurrentWeapon(player).Automatic;
        if ((_attackBuffer > 0f || (commands.AttackHeld && automatic)) && Combat.TryAttack(player)) _attackBuffer = 0f;
        if (_pickupBuffer > 0f && player.IsFreeToAct)
        {
            Combat.PickupOrThrow(player);
            _pickupBuffer = 0f;
        }
    }

    public Actor? FindActor(string levelId) => _actors.FirstOrDefault(a => a.LevelId == levelId);

    internal void AddEvent(GameEvent gameEvent) => _events.Add(gameEvent);

    /// <summary>
    /// Walking or running speed. Running spends stamina; once exhausted, the character walks until
    /// it has recovered enough. Running footsteps can be heard by enemies.
    /// </summary>
    private float UpdateRunning(Actor actor, bool wantsToRun, float dt)
    {
        if (actor.Definition.Stamina is not { } stamina)
        {
            actor.IsRunning = false;
            return actor.Definition.Speed;
        }

        if (actor.Exhausted && actor.Stamina >= stamina.RecoverThreshold) actor.Exhausted = false;
        actor.IsRunning = wantsToRun && !actor.Exhausted && actor.Stamina > 0f;
        if (actor.IsRunning)
        {
            actor.Stamina = MathF.Max(0f, actor.Stamina - stamina.RunDrain * dt);
            actor.StaminaRestTimer = stamina.RegenDelay;
            if (actor.Stamina <= 0f) actor.Exhausted = true;
            actor.RunNoiseTimer -= dt;
            if (actor.RunNoiseTimer <= 0f)
            {
                actor.RunNoiseTimer = RunNoiseInterval;
                EmitNoise(actor.Position, stamina.RunNoise, actor);
            }

            return actor.Definition.RunSpeed;
        }

        actor.StaminaRestTimer -= dt;
        if (actor.StaminaRestTimer <= 0f) actor.Stamina = MathF.Min(stamina.Max, actor.Stamina + stamina.Regen * dt);
        return actor.Definition.Speed;
    }

    private const float RunNoiseInterval = 0.35f;

    /// <summary>Uses a medkit if the player is hurt and has one.</summary>
    public bool TryHeal(Actor actor)
    {
        if (!actor.IsPlayer || !actor.CanAct || Inventory.Medkits <= 0 || actor.Health >= actor.Definition.Health) return false;
        float before = actor.Health;
        actor.Health = MathF.Min(actor.Definition.Health, actor.Health + Rules.MedkitHeal);
        Inventory.Medkits--;
        AddEvent(new Healed(actor, actor.Health - before));
        return true;
    }

    /// <summary>
    /// Gives loot to the player: ammunition and medkits go to the inventory, a weapon is placed on
    /// the floor at <paramref name="where"/>, a mission item is kept (and announced to triggers).
    /// </summary>
    internal void GiveLoot(IReadOnlyList<LootEntry> loot, Vector2 where, bool announce = true)
    {
        if (loot.Count == 0) return;
        foreach (LootEntry entry in loot)
        {
            if (entry.Ammo is { } type) Inventory.AddAmmo(type, entry.Amount);
            if (entry.Medkits > 0) Inventory.Medkits += entry.Medkits;
            if (entry.Weapon is { } weaponId && Definitions.Weapons.TryGetValue(weaponId, out WeaponDefinition? weapon))
                AddWeapon(new WorldWeapon(new WeaponInstance(weapon), where + new Vector2(0f, Map.TileSize * 0.5f), 0.4f));
            if (entry.Item is { } item && Inventory.AddItem(item, entry.NameKey)) Triggers.OnCollected(item);
        }

        if (announce) AddEvent(new LootReceived(loot, where));
    }

    /// <summary>Makes a new character appear (level trigger); it hunts the player if asked.</summary>
    internal Actor? SpawnFromTrigger(TriggerAction action)
    {
        if (action.Definition is null || !Definitions.Actors.ContainsKey(action.Definition)) return null;
        Actor actor = LevelBuilder.SpawnEnemy(this, Level, Definitions, new LevelActor
        {
            Definition = action.Definition,
            Position = action.Position,
            FacingDegrees = action.FacingDegrees,
            Tag = action.Tag,
        });
        if (action.Alert) actor.Brain?.AlertTo(Player);
        AddEvent(new ActorSpawned(actor));
        return actor;
    }

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
                // Without loot, the item itself is what the mission needs.
                if (item.Definition.Loot.Count > 0) GiveLoot(item.Definition.Loot, item.Position);
                else if (Inventory.AddItem(item.Definition.Id, item.Definition.NameKey)) Triggers.OnCollected(item.Definition.Id);
            }
        }
    }

    internal void Activate(Interactable interactable)
    {
        if (!interactable.Available) return;
        interactable.Used = true;
        AddEvent(new Activated(interactable));
        GiveLoot(interactable.Definition.Loot, interactable.Position);
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
