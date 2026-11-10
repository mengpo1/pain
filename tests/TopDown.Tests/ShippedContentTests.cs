using TopDown.Content;
using TopDown.Core.World;

namespace TopDown.Tests;

/// <summary>Checks the content shipped in the repository (Content/ and Mods/).</summary>
public class ShippedContentTests
{
    private static string RepositoryRoot
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null && !File.Exists(Path.Combine(directory, "TopDown.slnx")))
                directory = Path.GetDirectoryName(directory);
            return directory ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    private static ContentDatabase Load(params string[] mods) => ContentLoader.Load(new ContentLoadOptions
    {
        ContentRoot = Path.Combine(RepositoryRoot, "Content"),
        ModsRoot = Path.Combine(RepositoryRoot, "Mods"),
        EnabledMods = mods,
    });

    [Fact]
    public void Base_content_has_no_errors_or_warnings()
    {
        ContentDatabase database = Load();

        Assert.Empty(database.Diagnostics.Items.Where(d => d.Severity != Severity.Info).Select(d => d.ToString()));
        Assert.NotEmpty(database.PlayableCampaign);
    }

    [Fact]
    public void Every_base_level_builds_and_runs()
    {
        ContentDatabase database = Load();
        foreach (var level in database.Levels.Values)
        {
            GameWorld world = GameWorld.Create(level, database.Definitions);
            for (int i = 0; i < 240; i++) world.Step(1f / 120f, default);
            Assert.True(world.Player.IsAlive || world.Outcome == WorldOutcome.Defeat);
        }
    }

    [Fact]
    public void Every_text_exists_in_french_and_english()
    {
        ContentDatabase database = Load();
        var french = database.Locales["fr"];
        var english = database.Locales["en"];

        Assert.Empty(english.Keys.Except(french.Keys));
        Assert.Empty(french.Keys.Except(english.Keys));
    }
}
