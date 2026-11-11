namespace Game.Core.Common;

/// <summary>
/// Deterministic random numbers (SplitMix64). The whole state is one number, so a battle can be
/// saved and resumed with exactly the same future rolls, and a test can replay a scenario.
/// Ranges are inclusive at both ends, like OpenXcom's RNG::generate, so rule formulas read the same.
/// </summary>
public sealed class GameRandom
{
    public GameRandom(ulong seed) => State = seed;

    /// <summary>Internal state; restoring it restores the sequence.</summary>
    public ulong State { get; set; }

    public ulong NextULong()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Integer between <paramref name="min"/> and <paramref name="max"/>, both included.</summary>
    public int Next(int min, int max)
    {
        if (max < min) (min, max) = (max, min);
        ulong range = (ulong)((long)max - min) + 1UL;
        // Rejection sampling: no modulo bias.
        ulong limit = ulong.MaxValue - ulong.MaxValue % range;
        ulong value;
        do value = NextULong();
        while (value >= limit);
        return (int)((long)min + (long)(value % range));
    }

    /// <summary>Real number in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>True with a probability of <paramref name="percent"/> % (0 never, 100 always).</summary>
    public bool Percent(int percent) => Next(0, 99) < percent;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(0, items.Count - 1)];
}
