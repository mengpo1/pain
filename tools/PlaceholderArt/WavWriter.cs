namespace PlaceholderArt;

/// <summary>Writes mono 16-bit PCM WAV files.</summary>
public static class WavWriter
{
    /// <summary>Encodes samples in [-1, 1] (values outside are clamped, but callers normalise first).</summary>
    public static byte[] Encode(float[] samples, int sampleRate)
    {
        int dataBytes = samples.Length * 2;
        using var ms = new MemoryStream(44 + dataBytes);
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write("RIFF"u8);
            w.Write(36 + dataBytes);
            w.Write("WAVE"u8);
            w.Write("fmt "u8);
            w.Write(16);                // fmt chunk size
            w.Write((short)1);          // PCM
            w.Write((short)1);          // mono
            w.Write(sampleRate);
            w.Write(sampleRate * 2);    // byte rate
            w.Write((short)2);          // block align
            w.Write((short)16);         // bits per sample
            w.Write("data"u8);
            w.Write(dataBytes);
            foreach (float s in samples)
                w.Write((short)Math.Clamp((int)Math.Round(s * 32767.0), -32768, 32767));
        }
        return ms.ToArray();
    }
}
