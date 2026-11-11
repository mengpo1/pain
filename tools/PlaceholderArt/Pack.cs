namespace PlaceholderArt;

/// <summary>An output asset folder; records everything written so the tool can print a summary.</summary>
public sealed class Pack(string root)
{
    public string Root { get; } = root;

    public List<string> Written { get; } = [];

    public void Png(string relativePath, Canvas canvas)
    {
        Write(relativePath, canvas.ToPng());
        Written.Add($"{relativePath}  {canvas.Width}x{canvas.Height}");
    }

    /// <summary>
    /// Writes a mono 16-bit WAV, normalised to <paramref name="peak"/> (so nothing clips).
    /// One-shots get a DC blocker and tiny edge fades against clicks; loops are written untouched
    /// so their wrap-around stays seamless.
    /// </summary>
    public void Wav(string relativePath, float[] samples, double peak = 0.89, bool loop = false)
    {
        if (!loop) samples.DcBlock().FadeIn(0.0005).FadeOut(0.008);
        samples.Normalize(peak);
        Write(relativePath, WavWriter.Encode(samples, Dsp.Rate));
        Written.Add($"{relativePath}  {samples.Length / (double)Dsp.Rate:0.00}s{(loop ? " (loop)" : "")}");
    }

    public void Text(string relativePath, string content)
    {
        Write(relativePath, System.Text.Encoding.UTF8.GetBytes(content.ReplaceLineEndings("\n")));
        Written.Add(relativePath);
    }

    /// <summary>Writes a file next to the asset folder (the pack's root), such as a generated asset manifest.</summary>
    public void PackFile(string name, string content)
    {
        string path = Path.Combine(Path.GetDirectoryName(Root)!, name);
        File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(content.ReplaceLineEndings("\n")));
        Written.Add("../" + name);
    }

    void Write(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
