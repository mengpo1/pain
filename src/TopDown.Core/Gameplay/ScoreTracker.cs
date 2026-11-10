using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Core.Gameplay;

/// <summary>Optional score with combos. Kills are counted even when the score is disabled.</summary>
public sealed class ScoreTracker
{
    private readonly ScoreRules _rules;

    internal ScoreTracker(ScoreRules rules) => _rules = rules;

    public bool Enabled => _rules.Enabled;
    public int Total { get; private set; }
    public int Kills { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public float ComboTimeLeft { get; private set; }
    public float ComboWindow => _rules.ComboWindow;
    public int LastKillPoints { get; private set; }

    internal void OnKill(HitMethod method)
    {
        Kills++;
        if (!Enabled) return;
        Combo = ComboTimeLeft > 0f ? Combo + 1 : 1;
        BestCombo = Math.Max(BestCombo, Combo);
        ComboTimeLeft = _rules.ComboWindow;
        int basePoints = _rules.Points.GetValueOrDefault(MethodKey(method), 100);
        LastKillPoints = basePoints * Combo;
        Total += LastKillPoints;
    }

    internal void Update(float dt)
    {
        if (ComboTimeLeft <= 0f) return;
        ComboTimeLeft -= dt;
        if (ComboTimeLeft <= 0f) Combo = 0;
    }

    public static string MethodKey(HitMethod method) => method switch
    {
        HitMethod.Melee => "melee",
        HitMethod.Firearm => "firearm",
        HitMethod.Thrown => "thrown",
        HitMethod.Door => "door",
        HitMethod.Execution => "execution",
        _ => "other",
    };
}
