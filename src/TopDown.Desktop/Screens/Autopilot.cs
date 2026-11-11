using TopDown.Core.Definitions;
using TopDown.Core.World;
using NVector2 = System.Numerics.Vector2;

namespace TopDown.Desktop.Screens;

/// <summary>
/// Test driver enabled with <c>--autopilot</c>: plays a level by itself so smoke tests and
/// screenshots exercise real gameplay. Not meant to be a good player.
/// </summary>
internal sealed class Autopilot
{
    private readonly List<NVector2> _path = new();
    private float _repathTime;
    private int _pathIndex;

    public void Drive(GameWorld world, ref PlayerCommands commands, float time)
    {
        Actor player = world.Player;
        if (!player.IsAlive) return;
        if (player.State == ActorState.Executing)
        {
            // Deliver the blows of the execution.
            commands.AttackPressed = true;
            return;
        }

        if (world.Combat.FindExecutionTarget(player) is not null)
        {
            commands.InteractPressed = true;
            commands.Move = NVector2.Zero;
            return;
        }

        Actor? enemy = world.Actors
            .Where(a => a.IsAlive && !a.IsDown && player.IsHostileTo(a))
            .OrderBy(a => NVector2.Distance(a.Position, player.Position))
            .FirstOrDefault();

        NVector2 goal;
        if (player.Weapon is null && world.Weapons.FirstOrDefault(w => !w.IsFlying) is { } weapon
            && NVector2.Distance(weapon.Position, player.Position) < 260f)
        {
            goal = weapon.Position;
            if (world.Combat.FindPickup(player) is not null) commands.PickupOrThrowPressed = true;
        }
        else if (enemy is not null)
        {
            goal = enemy.Position;
            bool visible = world.Physics.HasLineOfSight(player.Position, enemy.Position, Blocking.Projectiles);
            commands.AimTarget = enemy.Position;
            float reach = world.Combat.CurrentWeapon(player) is { IsFirearm: true } ? 500f : 40f;
            if (visible && NVector2.Distance(enemy.Position, player.Position) < reach)
            {
                commands.AttackPressed = true;
                commands.AttackHeld = true;
            }
        }
        else if (world.Objectives.Current is { Zone: { } zone } && world.Zones.TryGetValue(zone, out var rect))
        {
            goal = rect.Center;
        }
        else
        {
            commands.Move = NVector2.Zero;
            return;
        }

        if (time > _repathTime || _path.Count == 0)
        {
            world.Pathfinder.FindPath(player.Position, goal, _path);
            _pathIndex = 0;
            _repathTime = time + 0.3f;
        }

        while (_pathIndex < _path.Count - 1 && NVector2.Distance(_path[_pathIndex], player.Position) < 10f) _pathIndex++;
        NVector2 target = _path.Count > 0 ? _path[Math.Min(_pathIndex, _path.Count - 1)] : goal;
        NVector2 direction = target - player.Position;
        commands.Move = direction.LengthSquared() > 4f ? NVector2.Normalize(direction) : NVector2.Zero;
        if (enemy is null) commands.AimTarget = player.Position + commands.Move * 50f;
    }
}
