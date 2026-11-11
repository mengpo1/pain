using Game.Core.Battle;
using Game.Core.Battle.Ai;
using Game.Core.Content;
using Game.Core.Saves;

namespace Game.Tests;

public sealed class SaveTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "opentactics-saves-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    /// <summary>Plays AI turns on both sides; returns a fingerprint of what happened.</summary>
    private static string Play(Battle battle, int commands)
    {
        var ai = new AiController(battle);
        for (int i = 0; i < commands && !battle.IsOver; i++)
        {
            BattleCommand command = ai.NextCommand();
            if (!battle.Execute(command).Accepted) ai.Refused(command);
            battle.RunUntilIdle();
        }

        return string.Join("|", battle.Units.Select(u => $"{u.Id}:{u.Position}:{u.Tu}:{u.Health}:{u.Status}")) + $"#{battle.Turn}{battle.CurrentFaction}";
    }

    [Fact]
    public void A_saved_battle_continues_exactly_as_the_original()
    {
        ContentDatabase content = ShippedContentTests.Load("horizon");
        Battle original = BattleFactory.Create(content.Rules, content.Rules.Missions["first_contact"], 21);
        Play(original, 25);
        string path = Path.Combine(_folder, "battle.json");
        BattleSaves.Write(original, content, path);

        Battle restored = BattleSaves.Read(path, content);
        Assert.Equal(original.Turn, restored.Turn);
        Assert.Equal(original.CurrentFaction, restored.CurrentFaction);
        Assert.Equal(original.Random.State, restored.Random.State);
        Assert.Equal(original.Items.Count, restored.Items.Count);
        foreach (BattleUnit unit in original.Units)
        {
            BattleUnit copy = restored.GetUnit(unit.Id)!;
            Assert.Equal((unit.Position, unit.Tu, unit.Health, unit.Status, unit.Morale), (copy.Position, copy.Tu, copy.Health, copy.Status, copy.Morale));
            Assert.Equal(unit.Inventory.Select(i => (i.Id, i.Slot)), copy.Inventory.Select(i => (i.Id, i.Slot)));
        }

        // Both continue identically: same rolls, same decisions.
        Assert.Equal(Play(original, 40), Play(restored, 40));
    }

    [Fact]
    public void Saving_twice_produces_the_same_battle()
    {
        ContentDatabase content = ShippedContentTests.Load("mistvale");
        Battle battle = BattleFactory.Create(content.Rules, content.Rules.Missions["hold_the_mill"], 3);
        Play(battle, 10);
        string path = Path.Combine(_folder, "a.json");
        BattleSaves.Write(battle, content, path);
        BattleSaves.Write(BattleSaves.Read(path, content), content, path + "2");
        static string Strip(string json) => string.Join('\n', json.Split('\n').Where(l => !l.Contains("savedAt", StringComparison.Ordinal)));
        Assert.Equal(Strip(File.ReadAllText(path)), Strip(File.ReadAllText(path + "2")));
    }

    [Fact]
    public void A_save_of_another_game_is_refused_with_a_clear_message()
    {
        ContentDatabase horizon = ShippedContentTests.Load("horizon");
        Battle battle = BattleFactory.Create(horizon.Rules, horizon.Rules.Missions["first_contact"], 1);
        string path = Path.Combine(_folder, "h.json");
        BattleSaves.Write(battle, horizon, path);
        SaveException error = Assert.Throws<SaveException>(() => BattleSaves.Read(path, ShippedContentTests.Load("mistvale")));
        Assert.Contains("horizon", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_save_needing_a_missing_mod_lists_what_is_missing()
    {
        ContentDatabase modded = ShippedContentTests.Load("horizon", "horizon_arsenal");
        Battle battle = BattleFactory.Create(modded.Rules, modded.Rules.Missions["first_contact"], 1);
        string path = Path.Combine(_folder, "m.json");
        BattleSaves.Write(battle, modded, path);
        SaveException error = Assert.Throws<SaveException>(() => BattleSaves.Read(path, ShippedContentTests.Load("horizon")));
        Assert.Contains("rail_rifle", error.Message, StringComparison.Ordinal);
        Assert.Contains("horizon_arsenal", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupted_or_future_saves_are_refused()
    {
        ContentDatabase content = ShippedContentTests.Load("horizon");
        Directory.CreateDirectory(_folder);
        string broken = Path.Combine(_folder, "broken.json");
        File.WriteAllText(broken, "{ \"format\": 1, ");
        Assert.Throws<SaveException>(() => BattleSaves.Read(broken, content));

        Battle battle = BattleFactory.Create(content.Rules, content.Rules.Missions["first_contact"], 1);
        BattleSave save = BattleSaves.Capture(battle, content);
        save.Format = 99;
        Assert.Contains("format 99", Assert.Throws<SaveException>(() => BattleSaves.Restore(save, content)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_busy_battle_cannot_be_saved()
    {
        ContentDatabase content = ShippedContentTests.Load("horizon");
        Battle battle = BattleFactory.Create(content.Rules, content.Rules.Missions["first_contact"], 1);
        BattleUnit unit = battle.ActiveUnitsOf(Faction.Player).First();
        Assert.True(battle.Execute(new MoveCommand(unit.Id, unit.Position.Offset(1, 0))).Accepted || battle.Execute(new MoveCommand(unit.Id, unit.Position.Offset(-1, 0))).Accepted);
        Assert.Throws<SaveException>(() => BattleSaves.Write(battle, content, Path.Combine(_folder, "busy.json")));
    }
}
