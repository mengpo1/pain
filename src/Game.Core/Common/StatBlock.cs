namespace Game.Core.Common;

/// <summary>
/// Values of a unit's statistics, by stat id (see <c>StatIds</c>). A missing stat reads as 0, so
/// a pack can add statistics without touching the rules that do not use them.
/// </summary>
public sealed class StatBlock
{
    private readonly Dictionary<string, int> _values;

    public StatBlock() => _values = new Dictionary<string, int>(StringComparer.Ordinal);

    public StatBlock(IReadOnlyDictionary<string, int> values) => _values = new Dictionary<string, int>(values, StringComparer.Ordinal);

    public int this[string id]
    {
        get => _values.TryGetValue(id, out int value) ? value : 0;
        set => _values[id] = value;
    }

    public IReadOnlyDictionary<string, int> Values => _values;

    public StatBlock Clone() => new(_values);

    /// <summary>Adds every value of <paramref name="bonus"/> (armour bonuses, training).</summary>
    public void Add(IReadOnlyDictionary<string, int> bonus)
    {
        foreach ((string id, int value) in bonus) this[id] += value;
    }
}
