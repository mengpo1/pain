namespace Game.Core.Common;

/// <summary>
/// The eight facing directions, numbered like OpenXcom: 0 = north (-Y), then clockwise
/// (1 = north-east, 2 = east, … 7 = north-west). Odd numbers are diagonals.
/// </summary>
public static class Directions
{
    public const int Count = 8;

    private static readonly (int Dx, int Dy)[] Deltas =
    [
        (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1),
    ];

    public static (int Dx, int Dy) Delta(int direction) => Deltas[Normalize(direction)];

    public static bool IsDiagonal(int direction) => (direction & 1) == 1;

    public static int Normalize(int direction) => ((direction % Count) + Count) % Count;

    /// <summary>Direction of a one-tile step, or -1 when the delta is not a single step.</summary>
    public static int FromStep(int dx, int dy)
    {
        for (int i = 0; i < Count; i++)
            if (Deltas[i] == (dx, dy)) return i;
        return -1;
    }

    /// <summary>Closest of the eight directions to look from <paramref name="from"/> towards <paramref name="to"/>.</summary>
    public static int Towards(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return 0;
        // atan2 with north = 0 and clockwise angles: (dx, -dy) in the usual maths convention.
        double angle = Math.Atan2(dx, -dy);
        int sector = (int)Math.Round(angle / (Math.PI / 4));
        return Normalize(sector);
    }

    /// <summary>Number of 1/8 turns needed to go from one facing to another (0 to 4).</summary>
    public static int TurnSteps(int from, int to)
    {
        int difference = Math.Abs(Normalize(to) - Normalize(from));
        return Math.Min(difference, Count - difference);
    }

    /// <summary>+1 to turn clockwise, -1 counter-clockwise, 0 if already facing.</summary>
    public static int TurnStep(int from, int to)
    {
        int difference = Normalize(to - from);
        if (difference == 0) return 0;
        return difference <= Count / 2 ? 1 : -1;
    }
}
