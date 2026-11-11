using System.Globalization;
using Game.Desktop;

LaunchOptions options;
try
{
    options = ParseArguments(args);
}
catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
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
            case "--pack": options.Pack = Value(); break;
            case "--mod": options.Mods.Add(Value()); break;
            case "--mission": options.Mission = Value(); break;
            case "--seed": options.Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--lang": options.Language = Value(); break;
            case "--screenshot": options.Screenshot = Value(); break;
            case "--frames": options.Frames = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--autoplay": options.Autoplay = true; break;
            case "--mute": options.Mute = true; break;
            case "--size":
                string[] size = Value().Split('x');
                if (size.Length != 2) throw new ArgumentException("--size expects WIDTHxHEIGHT, e.g. 1280x800.");
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
    Usage: OpenTactics [options]
      --pack <id>         game pack to play (folder in Content/: horizon, mistvale…)
      --mod <id>          also load this installed mod (repeatable)
      --mission <id>      start this mission directly
      --seed <n>          battle seed (same seed, same map and rolls)
      --lang <code>       language (fr, en…)
      --content <dir>     content folder (default: next to the executable)
      --mods <dir>        mods folder (default: next to the executable)
      --size <WxH>        window size
      --mute              no audio
      --autoplay          the AI also plays your side (demo, tests)
      --frames <n>        quit after n frames (tests)
      --screenshot <png>  with --frames: save the last frame
    """);
