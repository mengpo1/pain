using TopDown.Content;

namespace TopDown.Tests;

public class LocalizerTests
{
    private static Localizer Create(string language)
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["menu.play"] = "Play",
                ["only.english"] = "Only in English",
                ["ammo.one"] = "{count} round",
                ["ammo.other"] = "{count} rounds",
                ["hello"] = "Hello {name}!",
            },
            ["fr"] = new Dictionary<string, string>
            {
                ["menu.play"] = "Jouer",
                ["ammo.one"] = "{count} munition",
                ["ammo.other"] = "{count} munitions",
                ["hello"] = "Bonjour {name} !",
            },
        };
        return new Localizer(tables, language, "en");
    }

    [Fact]
    public void Texts_come_from_the_current_language()
    {
        Assert.Equal("Jouer", Create("fr")["menu.play"]);
        Assert.Equal("Play", Create("en")["menu.play"]);
    }

    [Fact]
    public void Missing_texts_fall_back_and_are_reported_once()
    {
        Localizer texts = Create("fr");
        var missing = new List<string>();
        texts.MissingKey += missing.Add;

        Assert.Equal("Only in English", texts["only.english"]);
        Assert.Equal("Only in English", texts["only.english"]);
        Assert.Equal("nowhere", texts["nowhere"]);

        Assert.Equal(2, missing.Count);
        Assert.Contains(missing, m => m.Contains("only.english"));
    }

    [Fact]
    public void Unknown_language_uses_the_fallback()
    {
        Assert.Equal("en", Create("xx").Language);
    }

    [Fact]
    public void Language_can_change_at_runtime()
    {
        Localizer texts = Create("en");
        texts.SetLanguage("fr");
        Assert.Equal("Jouer", texts["menu.play"]);
    }

    [Fact]
    public void Placeholders_are_replaced()
    {
        Assert.Equal("Bonjour Alex !", Create("fr").Format("hello", ("name", "Alex")));
    }

    [Theory]
    [InlineData("fr", 0, "0 munition")]
    [InlineData("fr", 1, "1 munition")]
    [InlineData("fr", 2, "2 munitions")]
    [InlineData("en", 0, "0 rounds")]
    [InlineData("en", 1, "1 round")]
    [InlineData("en", 5, "5 rounds")]
    public void Plurals_follow_the_language_rules(string language, int count, string expected)
    {
        Assert.Equal(expected, Create(language).Format("ammo", ("count", count)));
    }

    [Fact]
    public void Unicode_text_is_preserved()
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ja"] = new Dictionary<string, string> { ["title"] = "夜の監視カメラ — «test» ✓" },
            ["en"] = new Dictionary<string, string>(),
        };
        Assert.Equal("夜の監視カメラ — «test» ✓", new Localizer(tables, "ja", "en")["title"]);
    }
}
