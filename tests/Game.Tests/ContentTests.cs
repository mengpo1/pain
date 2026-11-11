using Game.Core.Battle;
using Game.Core.Battle.Ai;
using Game.Core.Content;
using Game.Core.Definitions;

namespace Game.Tests;

/// <summary>Loads the shipped packs from the repository's Content folder.</summary>
public class ShippedContentTests
{
    public static readonly TheoryData<string> Packs = new() { "horizon", "mistvale" };

    internal static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "Game.slnx"))) directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }

    internal static ContentDatabase Load(string pack, params string[] mods) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(RepositoryRoot(), "Content"),
        ModsRoot = Path.Combine(RepositoryRoot(), "Mods"),
        GamePack = pack,
        EnabledMods = mods,
    });

    [Theory]
    [MemberData(nameof(Packs))]
    public void Pack_loads_without_errors_or_warnings(string pack)
    {
        ContentDatabase content = Load(pack);
        Assert.Empty(content.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Select(d => d.ToString()));
        Assert.NotEmpty(content.PlayableMissions);
    }

    [Theory]
    [MemberData(nameof(Packs))]
    public void Every_text_key_exists_in_both_languages(string pack)
    {
        ContentDatabase content = Load(pack);
        IReadOnlyDictionary<string, string> en = content.Locales["en"], fr = content.Locales["fr"];
        Assert.Empty(en.Keys.Except(fr.Keys));
        Assert.Empty(fr.Keys.Except(en.Keys));
    }

    [Theory]
    [MemberData(nameof(Packs))]
    public void Every_mission_can_be_played_by_the_ai_on_both_sides(string pack)
    {
        ContentDatabase content = Load(pack);
        foreach (MissionDefinition mission in content.PlayableMissions)
        {
            Battle battle = BattleFactory.Create(content.Rules, mission, seed: 7);
            Assert.NotEmpty(battle.ActiveUnitsOf(Faction.Player));
            var ai = new AiController(battle);
            for (int i = 0; i < 3000 && !battle.IsOver && battle.Turn <= 12; i++)
            {
                BattleCommand command = ai.NextCommand();
                if (!battle.Execute(command).Accepted) ai.Refused(command);
                battle.RunUntilIdle();
            }

            Assert.True(battle.IsOver || battle.Turn > 12, $"{mission.Id}: the battle stalled on turn {battle.Turn}.");
        }
    }

    [Fact]
    public void Generated_maps_are_reproducible_from_the_seed()
    {
        ContentDatabase content = Load("horizon");
        MissionDefinition mission = content.Rules.Missions["patrol_sweep"];
        static string Layout(Battle battle) => string.Concat(battle.Map.Tiles.Select(t => t.Object?.Id ?? t.Floor?.Id ?? "-"));
        Assert.Equal(Layout(BattleFactory.Create(content.Rules, mission, 11)), Layout(BattleFactory.Create(content.Rules, mission, 11)));
        Assert.NotEqual(Layout(BattleFactory.Create(content.Rules, mission, 11)), Layout(BattleFactory.Create(content.Rules, mission, 12)));
    }
}
