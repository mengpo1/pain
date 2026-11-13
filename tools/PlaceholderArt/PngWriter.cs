using System.IO.Compression;
using System.Text;

namespace PlaceholderArt;

/// <summary>Minimal PNG encoder: 8-bit RGBA, non-interlaced, adaptive per-row filters, zlib via ZLibStream.</summary>
public static class PngWriter
{
    static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4) throw new ArgumentException("Pixel buffer size mismatch.", nameof(rgba));

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)width);
        WriteBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // colour type: RGBA
        // ihdr[10..12] = compression 0, filter method 0, no interlace
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", Compress(FilterRows(width, height, rgba)));
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>Applies the PNG filter that minimises the sum of absolute residuals for each row.</summary>
    static byte[] FilterRows(int width, int height, byte[] rgba)
    {
        int stride = width * 4;
        var output = new byte[(stride + 1) * height];
        var prev = new byte[stride];
        var candidates = new byte[5][];
        for (int f = 0; f < 5; f++) candidates[f] = new byte[stride];

        for (int y = 0; y < height; y++)
        {
            var row = new ReadOnlySpan<byte>(rgba, y * stride, stride);
            int best = 0;
            long bestScore = long.MaxValue;
            for (int f = 0; f < 5; f++)
            {
                long score = 0;
                var cand = candidates[f];
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= 4 ? row[i - 4] : 0, b = prev[i], c = i >= 4 ? prev[i - 4] : 0;
                    int predicted = f switch
                    {
                        0 => 0,
                        1 => a,
                        2 => b,
                        3 => (a + b) / 2,
                        _ => Paeth(a, b, c),
                    };
                    byte v = (byte)(row[i] - predicted);
                    cand[i] = v;
                    score += Math.Abs((int)(sbyte)v);
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = f;
                }
            }
            int o = y * (stride + 1);
            output[o] = (byte)best;
            Array.Copy(candidates[best], 0, output, o + 1, stride);
            row.CopyTo(prev);
        }
        return output;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(data);
        return ms.ToArray();
    }

    static void WriteChunk(Stream s, string type, byte[] data)
    {
        var header = new byte[8];
        WriteBigEndian(header, 0, (uint)data.Length);
        Encoding.ASCII.GetBytes(type, 0, 4, header, 4);
        s.Write(header);
        s.Write(data);
        uint crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, header.AsSpan(4, 4));
        crc = UpdateCrc(crc, data);
        var tail = new byte[4];
        WriteBigEndian(tail, 0, crc ^ 0xFFFFFFFFu);
        s.Write(tail);
    }

    static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
