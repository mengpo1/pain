namespace Game.Desktop;

/// <summary>Minimal log: console plus a text file in the user folder.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;

    public static void Open(string path)
    {
        try
        {
            _file = new StreamWriter(path, append: false) { AutoFlush = true };
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Cannot open log file '{path}': {exception.Message}");
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
        lock (Gate)
        {
            (level == "ERROR" ? Console.Error : Console.Out).WriteLine(line);
            _file?.WriteLine(line);
        }
    }

    public static void Close()
    {
        lock (Gate)
        {
            _file?.Dispose();
            _file = null;
        }
    }
}
