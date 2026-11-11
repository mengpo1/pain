using Game.Core.Battle.Actions;
using Game.Core.Battle.Movement;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>How a battle ended.</summary>
public sealed record BattleOutcome(bool Victory, int Turn, string ReasonKey);

/// <summary>
/// A tactical battle: the map, the units and items on it, whose turn it is, and the rules that
/// resolve commands. It runs without any interface: <see cref="Execute"/> validates a command and
/// queues its actions, <see cref="Advance"/> performs one atomic step (one tile walked, one shot,
/// one impact) and records <see cref="Events"/>. An interface animates each step before asking
/// for the next one; tests and the AI call <see cref="RunUntilIdle"/>.
/// </summary>
public sealed class Battle
{
    private readonly List<BattleUnit> _units = new();
    private readonly List<BattleItem> _items = new();
    private readonly List<BattleAction> _actions = new();
    private readonly List<BattleEvent> _events = new();
    private readonly int[] _light;
    private readonly bool[][] _visibleNow;
    private readonly HashSet<BattleUnit>[] _spottedThisTurn = [new(), new(), new()];
    private int _nextUnitId = 1;
    private int _nextItemId = 1;

    public Battle(Ruleset rules, MissionDefinition mission, BattleMap map, GameRandom random)
    {
        Rules = rules;
        Mission = mission;
        Map = map;
        Random = random;
        _light = new int[map.Width * map.Length * map.Height];
        _visibleNow = Factions.All.Select(_ => new bool[_light.Length]).ToArray();
        Turn = 1;
        CurrentFaction = Faction.Player;
    }

    public Ruleset Rules { get; }
    public MissionDefinition Mission { get; }
    public BattleMap Map { get; }
    public GameRandom Random { get; }

    public IReadOnlyList<BattleUnit> Units => _units;
    public IReadOnlyList<BattleItem> Items => _items;

    /// <summary>Round number; it goes up when the player's turn starts again.</summary>
    public int Turn { get; private set; }

    public Faction CurrentFaction { get; private set; }

    public BattleOutcome? Outcome { get; private set; }

    /// <summary>TU the player keeps for reaction fire (set from the interface).</summary>
    public Reserve PlayerReserve { get; } = new();

    public bool IsOver => Outcome is not null;

    /// <summary>True while queued actions remain (walking, shots in flight…).</summary>
    public bool IsBusy => _actions.Count > 0;

    internal int QueuedActions => _actions.Count;

    /// <summary>Events since the last <see cref="TakeEvents"/>, in order.</summary>
    public IReadOnlyList<BattleEvent> Events => _events;

    public List<BattleEvent> TakeEvents()
    {
        var events = new List<BattleEvent>(_events);
        _events.Clear();
        return events;
    }

    // ------------------------------------------------------------------ setup

    public BattleUnit AddUnit(UnitDefinition definition, Faction faction, Position position, int direction, string name = "")
    {
        ArmorDefinition armor = Rules.Armors[definition.Armor];
        var unit = new BattleUnit(_nextUnitId++, definition, armor, faction, name) { Position = position, Direction = direction };
        Tile tile = Map[position] ?? throw new ArgumentOutOfRangeException(nameof(position), $"{position} is outside the map.");
        if (tile.Unit is not null) throw new InvalidOperationException($"{position} is already occupied by {tile.Unit}.");
        tile.Unit = unit;
        _units.Add(unit);
        return unit;
    }

    public BattleItem CreateItem(ItemDefinition definition)
    {
        var item = new BattleItem(_nextItemId++, definition);
        _items.Add(item);
        return item;
    }

    /// <summary>Called once units are placed: computes vision and starts the first player turn.</summary>
    public void Start()
    {
        VisionSystem.Refresh(this);
        Emit(new TurnStarted(Turn, CurrentFaction));
        CheckOutcome();
    }

    // ------------------------------------------------------------------ queries

    public BattleUnit? GetUnit(int id) => _units.FirstOrDefault(u => u.Id == id);

    public BattleItem? GetItem(int id) => _items.FirstOrDefault(i => i.Id == id);

    public BattleUnit? UnitAt(Position p) => Map[p]?.Unit;

    public IEnumerable<BattleUnit> UnitsOf(Faction faction) => _units.Where(u => u.Faction == faction);

    public IEnumerable<BattleUnit> ActiveUnitsOf(Faction faction) => _units.Where(u => u.Faction == faction && u.IsActive);

    public int LightAt(Position p) => Map.InBounds(p) ? _light[Index(p)] : 0;

    internal void SetLight(Position p, int value) => _light[Index(p)] = value;

    /// <summary>True if a unit of <paramref name="faction"/> sees the tile right now (player only; AI sides have no terrain memory).</summary>
    public bool IsTileVisible(Faction faction, Position p) => Map.InBounds(p) && _visibleNow[(int)faction][Index(p)];

    internal void MarkVisible(Faction faction, Position p)
    {
        if (!Map.InBounds(p)) return;
        _visibleNow[(int)faction][Index(p)] = true;
        Map[p]!.Discover(faction);
    }

    internal void ClearCurrentlyVisibleTiles()
    {
        foreach (bool[] layer in _visibleNow) Array.Clear(layer);
    }

    /// <summary>
    /// True if the side currently has this unit in sight (OXCE "visible flag": a hostile seen
    /// during the player's turn stays shown until the end of that turn).
    /// </summary>
    public bool IsSpottedBy(Faction faction, BattleUnit unit) =>
        unit.Faction == faction
        || (CurrentFaction == faction && _spottedThisTurn[(int)faction].Contains(unit))
        || UnitsOf(faction).Any(u => u.VisibleUnits.Contains(unit));

    /// <summary>Units of other sides a faction knows about: seen within the last <paramref name="memoryTurns"/> rounds.</summary>
    public IEnumerable<BattleUnit> KnownEnemies(Faction faction, int memoryTurns) =>
        _units.Where(u => u.IsActive && Factions.AreEnemies(faction, u.Faction) && u.TurnsSinceSeenBy[(int)faction] <= memoryTurns);

    internal void RecordSighting(BattleUnit observer, BattleUnit target)
    {
        target.TurnsSinceSeenBy[(int)observer.Faction] = 0;
        target.LastSeenPositionBy[(int)observer.Faction] = target.Position;
        if (_spottedThisTurn[(int)observer.Faction].Add(target)) Emit(new EnemySpotted(observer.Faction, observer.Id, target.Id));
    }

    /// <summary>Number of enemies a side has spotted since its turn began (a rise interrupts walking).</summary>
    internal int SpottedCount(Faction faction) => _spottedThisTurn[(int)faction].Count;

    // ------------------------------------------------------------------ commands

    /// <summary>
    /// Validates a command against the rules and queues the resulting actions. Nothing happens to
    /// the state before <see cref="Advance"/>, except instantaneous actions (turning, kneeling).
    /// </summary>
    public CommandResult Execute(BattleCommand command)
    {
        if (IsOver) return CommandResult.Refused("error.battle_over");
        if (IsBusy) return CommandResult.Refused("error.busy");
        if (command is EndTurnCommand)
        {
            _actions.Add(new EndTurnAction());
            return CommandResult.Ok;
        }

        BattleUnit? unit = GetUnit(command.UnitId);
        if (unit is null) return CommandResult.Refused("error.no_unit");
        if (unit.Faction != CurrentFaction) return CommandResult.Refused("error.not_your_turn");
        if (!unit.IsActive) return CommandResult.Refused("error.unit_down");
        if (unit.MoraleState != MoraleState.None) return CommandResult.Refused("error.panicking");

        return command switch
        {
            MoveCommand move => Queue(MoveAction.Create(this, unit, move.Destination)),
            TurnCommand turn => TurnRules.Turn(this, unit, turn.Direction),
            KneelCommand => TurnRules.ToggleKneel(this, unit),
            AttackCommand attack => Queue(AttackAction.Create(this, unit, attack)),
            ReloadCommand reload => Inventory.Reload(this, unit, reload.WeaponId),
            ThrowCommand thrown => Queue(ThrowAction.Create(this, unit, thrown)),
            PrimeCommand prime => Inventory.Prime(this, unit, prime.ItemId, prime.Fuse),
            MoveItemCommand move => Inventory.Move(this, unit, move.ItemId, move.Slot),
            _ => CommandResult.Refused("error.unknown_command"),
        };
    }

    private CommandResult Queue((CommandResult Result, BattleAction? Action) created)
    {
        if (created.Action is not null) _actions.Add(created.Action);
        return created.Result;
    }

    /// <summary>Puts an action on top of the stack: it runs before the ones already queued (reaction fire).</summary>
    internal void Push(BattleAction action) => _actions.Add(action);

    /// <summary>Performs the next atomic step. Returns false when there was nothing to do.</summary>
    public bool Advance()
    {
        if (_actions.Count == 0) return false;
        BattleAction action = _actions[^1];
        bool finished = action.Advance(this);
        if (finished) _actions.Remove(action);
        CheckOutcome();
        if (IsOver) _actions.Clear();
        return true;
    }

    /// <summary>Runs queued actions to completion (tests, AI turns, fast-forward).</summary>
    public void RunUntilIdle(int maxSteps = 100_000)
    {
        for (int i = 0; i < maxSteps && Advance(); i++)
        {
        }

        if (IsBusy) throw new InvalidOperationException("The battle did not settle: an action never finished.");
    }

    // ------------------------------------------------------------------ turns

    /// <summary>Hands the turn to the next side (called once the end-of-turn explosions are resolved).</summary>
    internal void SwitchToNextFaction()
    {
        TurnRules.EndFactionTurn(this, CurrentFaction);
        if (CurrentFaction == Faction.Player)
        {
            // Hostiles seen during the player's turn stop being shown (OXCE resets the visible flag).
            _spottedThisTurn[(int)Faction.Player].Clear();
        }

        Faction next = CurrentFaction switch
        {
            Faction.Player => Faction.Hostile,
            Faction.Hostile => Faction.Neutral,
            _ => Faction.Player,
        };

        if (next == Faction.Player)
        {
            Turn++;
            foreach (BattleUnit unit in _units)
                for (int f = 0; f < Factions.Count; f++)
                    if (unit.TurnsSinceSeenBy[f] < int.MaxValue) unit.TurnsSinceSeenBy[f]++;
        }

        CurrentFaction = next;
        _spottedThisTurn[(int)next].Clear();
        TurnRules.StartFactionTurn(this, next);
        VisionSystem.Refresh(this);
        Emit(new TurnStarted(Turn, next));
        // Units that lost control act first, lowest id first (the stack runs the last pushed first).
        foreach (BattleUnit unit in ActiveUnitsOf(next).Where(u => u.MoraleState != MoraleState.None).OrderByDescending(u => u.Id))
            _actions.Add(new PanicAction(unit));
        CheckOutcome();
    }

    // ------------------------------------------------------------------ outcome

    internal void CheckOutcome()
    {
        if (IsOver) return;
        BattleOutcome? outcome = Objectives.Evaluate(this);
        if (outcome is null) return;
        Outcome = outcome;
        Emit(new BattleEnded(outcome));
    }

    // ------------------------------------------------------------------ helpers for rules

    internal void Emit(BattleEvent battleEvent) => _events.Add(battleEvent);

    /// <summary>Moves a unit to a tile, keeping tile occupancy consistent.</summary>
    internal void PlaceUnit(BattleUnit unit, Position to)
    {
        if (Map[unit.Position]?.Unit == unit) Map[unit.Position]!.Unit = null;
        unit.Position = to;
        Map[to]!.Unit = unit;
    }

    internal void DropItem(BattleItem item, Position position)
    {
        item.Owner?.Inventory.Remove(item);
        item.Owner = null;
        item.Slot = null;
        item.GroundPosition = position;
        Map[position]!.Items.Add(item);
    }

    internal void DestroyItem(BattleItem item)
    {
        item.Owner?.Inventory.Remove(item);
        if (item.GroundPosition is { } p) Map[p]?.Items.Remove(item);
        item.Owner = null;
        item.GroundPosition = null;
        _items.Remove(item);
    }

    /// <summary>Recomputes vision after something moved or changed (cheap enough to redo fully).</summary>
    internal void RefreshVision() => VisionSystem.Refresh(this);

    /// <summary>Path a unit would walk to reach a tile (for previews and AI); see <see cref="Pathfinder"/>.</summary>
    public PathResult FindPath(BattleUnit unit, Position destination) => Pathfinder.Find(this, unit, destination);

    private int Index(Position p) => (p.Z * Map.Length + p.Y) * Map.Width + p.X;
}
