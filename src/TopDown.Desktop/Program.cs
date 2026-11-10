using System.Globalization;
using TopDown.Desktop;

LaunchOptions options;
try
{
    options = ParseArguments(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    PrintUsage();
    return 2;
}

using var game = new GameHost(options);
game.Run();
return 0;

static LaunchOptions ParseArguments(string[] args)
{
    var options = new LaunchOptions();
    for (int i = 0; i < args.Length; i++)
    {
        string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value after {args[i]}.");
        switch (args[i])
        {
            case "--content": options.ContentRoot = Value(); break;
            case "--mods": options.ModsRoot = Value(); break;
            case "--profile": options.Profile = Value(); break;
            case "--mod": options.ExtraMods.Add(Value()); break;
            case "--level": options.Level = Value(); break;
            case "--lang": options.Language = Value(); break;
            case "--screenshot": options.Screenshot = Value(); break;
            case "--frames": options.Frames = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--autopilot": options.Autopilot = true; break;
            case "--mute": options.Mute = true; break;
            case "--size":
                string[] size = Value().Split('x');
                options.Width = int.Parse(size[0], CultureInfo.InvariantCulture);
                options.Height = int.Parse(size[1], CultureInfo.InvariantCulture);
                break;
            case "--help":
            case "-h":
                PrintUsage();
                Environment.Exit(0);
                break;
            default: throw new ArgumentException($"Unknown option '{args[i]}'.");
        }
    }

    return options;
}

static void PrintUsage() => Console.WriteLine("""
    Usage: TopDown [options]
      --profile <id>      content profile from Content/profiles.json (e.g. action, noct)
      --mod <id>          also load this installed mod (repeatable)
      --level <id>        start this level directly
      --lang <code>       language (fr, en…)
      --content <dir>     content folder (default: next to the executable)
      --mods <dir>        mods folder (default: next to the executable)
      --size <WxH>        window size
      --mute              no audio
      --autopilot         the game plays itself (testing)
      --frames <n>        quit after n frames (testing)
      --screenshot <png>  with --frames: save the last frame
    """);
