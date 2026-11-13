namespace TopDown.Desktop;

/// <summary>Where content, mods, settings and logs live.</summary>
public sealed class AppPaths
{
    private const string AppFolder = "TopDown";

    public AppPaths(string? contentOverride, string? modsOverride)
    {
        string baseDirectory = AppContext.BaseDirectory;
        ContentRoot = Path.GetFullPath(contentOverride ?? Path.Combine(baseDirectory, "Content"));
        ModsRoot = Path.GetFullPath(modsOverride ?? Path.Combine(baseDirectory, "Mods"));

        string config = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
        if (string.IsNullOrEmpty(config)) config = baseDirectory;
        UserDirectory = Path.Combine(config, AppFolder);
        Directory.CreateDirectory(UserDirectory);
    }

    public string ContentRoot { get; }
    public string ModsRoot { get; }
    public string UserDirectory { get; }
    public string SettingsFile => Path.Combine(UserDirectory, "settings.json");
    public string LogFile => Path.Combine(UserDirectory, "log.txt");
    public string ProfilesFile => Path.Combine(ContentRoot, "profiles.json");
}
