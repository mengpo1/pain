namespace Game.Core.Content;

public enum Severity
{
    Info,
    Warning,
    Error,
}

/// <summary>A problem found while loading content, with the file or pack it comes from.</summary>
public sealed record ContentDiagnostic(Severity Severity, string Source, string Message)
{
    public override string ToString() => $"[{Severity}] {Source}: {Message}";
}

public sealed class DiagnosticList
{
    private readonly List<ContentDiagnostic> _items = new();

    public IReadOnlyList<ContentDiagnostic> Items => _items;
    public IEnumerable<ContentDiagnostic> Errors => _items.Where(d => d.Severity == Severity.Error);
    public IEnumerable<ContentDiagnostic> Warnings => _items.Where(d => d.Severity == Severity.Warning);
    public bool HasErrors => _items.Any(d => d.Severity == Severity.Error);

    public void Error(string source, string message) => _items.Add(new ContentDiagnostic(Severity.Error, source, message));
    public void Warning(string source, string message) => _items.Add(new ContentDiagnostic(Severity.Warning, source, message));
    public void Info(string source, string message) => _items.Add(new ContentDiagnostic(Severity.Info, source, message));
}
