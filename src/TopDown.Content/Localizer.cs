using System.Globalization;
using System.Text;

namespace TopDown.Content;

/// <summary>
/// Looks up player-facing texts by key. Placeholders use <c>{name}</c>. When a <c>count</c>
/// argument is given and the keys <c>key.one</c> / <c>key.other</c> exist, the plural form
/// matching the language is used. Missing keys fall back to the fallback language, then to
/// the key itself, and are reported once through <see cref="MissingKey"/>.
/// </summary>
public sealed class Localizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _tables;
    private readonly HashSet<string> _reported = new();

    public Localizer(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables, string language, string fallbackLanguage)
    {
        _tables = tables;
        FallbackLanguage = fallbackLanguage;
        Language = tables.ContainsKey(language) ? language : fallbackLanguage;
    }

    public string Language { get; private set; }
    public string FallbackLanguage { get; }
    public IReadOnlyCollection<string> Languages => _tables.Keys.ToList();

    /// <summary>Raised the first time a key is missing in the current language.</summary>
    public event Action<string>? MissingKey;

    public void SetLanguage(string language)
    {
        if (_tables.ContainsKey(language)) Language = language;
    }

    public string this[string key] => Get(key);

    public bool Has(string key) => Lookup(Language, key) is not null || Lookup(FallbackLanguage, key) is not null;

    public string Get(string key)
    {
        if (Lookup(Language, key) is { } text) return text;
        Report(key);
        return Lookup(FallbackLanguage, key) ?? key;
    }

    public string Format(string key, params (string Name, object Value)[] arguments)
    {
        string resolvedKey = key;
        foreach ((string name, object value) in arguments)
        {
            if (name != "count" || !TryGetNumber(value, out double count)) continue;
            string pluralKey = $"{key}.{PluralCategory(Language, count)}";
            if (Has(pluralKey)) resolvedKey = pluralKey;
            else if (Has($"{key}.other")) resolvedKey = $"{key}.other";
            break;
        }

        return Substitute(Get(resolvedKey), arguments);
    }

    /// <summary>CLDR-style plural category, simplified to "one"/"other".</summary>
    public static string PluralCategory(string language, double count)
    {
        string baseLanguage = language.Split('-', '_')[0].ToLowerInvariant();
        return baseLanguage switch
        {
            // French (and Portuguese) treat 0 and 1 as singular.
            "fr" or "pt" => Math.Abs(count) < 2 ? "one" : "other",
            // Languages without plural forms.
            "ja" or "zh" or "ko" => "other",
            _ => count == 1 ? "one" : "other",
        };
    }

    private string? Lookup(string language, string key) =>
        _tables.TryGetValue(language, out var table) && table.TryGetValue(key, out string? text) ? text : null;

    private void Report(string key)
    {
        if (_reported.Add($"{Language}:{key}")) MissingKey?.Invoke($"Missing text '{key}' for language '{Language}'.");
    }

    private static string Substitute(string text, (string Name, object Value)[] arguments)
    {
        if (arguments.Length == 0 || !text.Contains('{')) return text;
        var builder = new StringBuilder(text);
        foreach ((string name, object value) in arguments)
            builder.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static bool TryGetNumber(object value, out double number)
    {
        switch (value)
        {
            case int i: number = i; return true;
            case long l: number = l; return true;
            case float f: number = f; return true;
            case double d: number = d; return true;
            default: number = 0; return false;
        }
    }
}
