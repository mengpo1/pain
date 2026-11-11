namespace TopDown.Core.Gameplay;

/// <summary>
/// What the player carries besides the weapon in hand: spare ammunition per type, medkits and
/// mission items. Deliberately minimal: no weight, no slots.
/// </summary>
public sealed class Inventory
{
    private readonly Dictionary<string, int> _ammo = new(StringComparer.Ordinal);
    private readonly List<(string Id, string? NameKey)> _items = new();

    public IReadOnlyDictionary<string, int> Ammo => _ammo;
    public int Medkits { get; internal set; }

    /// <summary>Mission items in the order they were found.</summary>
    public IReadOnlyList<(string Id, string? NameKey)> Items => _items;

    public int AmmoOf(string type) => _ammo.GetValueOrDefault(type);

    public bool Has(string itemId) => _items.Any(item => item.Id == itemId);

    internal void AddAmmo(string type, int amount)
    {
        if (amount > 0) _ammo[type] = AmmoOf(type) + amount;
    }

    /// <summary>Takes up to <paramref name="amount"/> rounds; returns how many were taken.</summary>
    internal int TakeAmmo(string type, int amount)
    {
        int taken = Math.Min(Math.Max(0, amount), AmmoOf(type));
        if (taken > 0) _ammo[type] = AmmoOf(type) - taken;
        return taken;
    }

    internal bool AddItem(string id, string? nameKey)
    {
        if (Has(id)) return false;
        _items.Add((id, nameKey));
        return true;
    }
}
