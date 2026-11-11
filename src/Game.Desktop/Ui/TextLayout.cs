namespace Game.Desktop.Ui;

/// <summary>Word wrapping for the interface (any language: splits on spaces only).</summary>
public static class TextLayout
{
    public static IEnumerable<string> Wrap(UiContext ui, string text, float size, float width)
    {
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : $"{line} {word}";
                if (line.Length > 0 && ui.Measure(candidate, size).X > width)
                {
                    yield return line;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            yield return line;
        }
    }
}
