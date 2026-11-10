using System.Numerics;
using TopDown.Core.World;

namespace TopDown.Core.Ai;

/// <summary>
/// A* on the tile grid, 8 directions, without cutting corners. Buffers are reused between
/// searches so pathfinding does not allocate per call.
/// </summary>
public sealed class Pathfinder
{
    private static readonly (int Dx, int Dy, float Cost)[] Neighbours =
    [
        (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
        (1, 1, 1.4142f), (1, -1, 1.4142f), (-1, 1, 1.4142f), (-1, -1, 1.4142f),
    ];

    private readonly TileMap _map;
    private readonly Func<int, int, bool> _isWalkable;
    private readonly float[] _cost;
    private readonly int[] _parent;
    private readonly int[] _stamp;
    private readonly PriorityQueue<int, float> _open = new();
    private readonly List<int> _reversed = new();
    private int _currentStamp;

    public Pathfinder(TileMap map, Func<int, int, bool> isWalkable)
    {
        _map = map;
        _isWalkable = isWalkable;
        int size = map.Width * map.Height;
        _cost = new float[size];
        _parent = new int[size];
        _stamp = new int[size];
    }

    /// <summary>Maximum number of expanded tiles before giving up.</summary>
    public int MaxExpandedNodes { get; init; } = 6000;

    public bool IsWalkable(int x, int y) => _map.InBounds(x, y) && _isWalkable(x, y);

    /// <summary>
    /// Fills <paramref name="result"/> with waypoints (tile centres, then the exact goal).
    /// Returns false when the goal cannot be reached.
    /// </summary>
    public bool FindPath(Vector2 from, Vector2 to, List<Vector2> result)
    {
        result.Clear();
        (int startX, int startY) = _map.TileAt(from);
        (int goalX, int goalY) = _map.TileAt(to);
        bool exactGoal = IsWalkable(goalX, goalY);
        if (!exactGoal && !TryNearestWalkable(ref goalX, ref goalY, startX, startY)) return false;
        if (!_map.InBounds(startX, startY)) return false;

        int width = _map.Width;
        int start = startY * width + startX;
        int goal = goalY * width + goalX;
        if (start == goal)
        {
            result.Add(exactGoal ? to : _map.TileCenter(goalX, goalY));
            return true;
        }

        _currentStamp++;
        _open.Clear();
        Visit(start, 0f, -1);
        _open.Enqueue(start, Heuristic(startX, startY, goalX, goalY));

        int expanded = 0;
        while (_open.TryDequeue(out int node, out float priority))
        {
            if (node == goal) break;
            int x = node % width;
            int y = node / width;
            // Skip stale queue entries (a cheaper route was found after this one was queued).
            if (priority - Heuristic(x, y, goalX, goalY) > _cost[node] + 0.001f) continue;
            if (++expanded > MaxExpandedNodes) return false;

            foreach ((int dx, int dy, float stepCost) in Neighbours)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!IsWalkable(nx, ny)) continue;
                if (dx != 0 && dy != 0 && (!IsWalkable(x + dx, y) || !IsWalkable(x, y + dy))) continue;

                int next = ny * width + nx;
                float cost = _cost[node] + stepCost;
                if (_stamp[next] == _currentStamp && cost >= _cost[next]) continue;
                Visit(next, cost, node);
                _open.Enqueue(next, cost + Heuristic(nx, ny, goalX, goalY));
            }
        }

        if (_stamp[goal] != _currentStamp) return false;

        _reversed.Clear();
        for (int node = goal; node != start; node = _parent[node]) _reversed.Add(node);
        for (int i = _reversed.Count - 1; i >= 0; i--)
        {
            int node = _reversed[i];
            result.Add(_map.TileCenter(node % width, node / width));
        }

        if (exactGoal) result[^1] = to;
        return true;
    }

    private void Visit(int node, float cost, int parent)
    {
        _stamp[node] = _currentStamp;
        _cost[node] = cost;
        _parent[node] = parent;
    }

    /// <summary>Walkable tile around an unwalkable goal, the closest to the start among the nearest ring.</summary>
    private bool TryNearestWalkable(ref int x, ref int y, int fromX, int fromY)
    {
        for (int radius = 1; radius <= 3; radius++)
        {
            int bestX = 0, bestY = 0, bestDistance = int.MaxValue;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (!IsWalkable(x + dx, y + dy)) continue;
                    int distance = (x + dx - fromX) * (x + dx - fromX) + (y + dy - fromY) * (y + dy - fromY);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestX = x + dx;
                    bestY = y + dy;
                }
            }

            if (bestDistance == int.MaxValue) continue;
            x = bestX;
            y = bestY;
            return true;
        }

        return false;
    }

    private static float Heuristic(int x, int y, int goalX, int goalY)
    {
        int dx = Math.Abs(x - goalX);
        int dy = Math.Abs(y - goalY);
        return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
    }
}
