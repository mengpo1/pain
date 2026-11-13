namespace PlaceholderArt;

/// <summary>Deterministic hash noise. Value noise is periodic so tile textures wrap seamlessly.</summary>
public static class Noise
{
    public static uint HashU(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x27D4EB2Fu + 0x165667B1u;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h << 13) | (h >> 19);
            h *= 0xC2B2AE3Du;
            h ^= (uint)y * 0x9E3779B1u;
            h = (h << 17) | (h >> 15);
            h *= 0x85EBCA77u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    /// <summary>Uniform value in [0, 1) for an integer lattice point.</summary>
    public static float Hash(int x, int y, int seed) => (HashU(x, y, seed) >> 8) / 16777216f;

    /// <summary>Smooth value noise in [0, 1], periodic with periodX / periodY lattice cells.</summary>
    public static float Value(float x, float y, int periodX, int periodY, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int x0 = Mod(xi, periodX), x1 = Mod(xi + 1, periodX);
        int y0 = Mod(yi, periodY), y1 = Mod(yi + 1, periodY);
        float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed);
        float c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
        return top + (bottom - top) * fy;
    }

    /// <summary>
    /// Fractal value noise over a unit tile: (u, v) in [0, 1) is exactly one period, so the result tiles.
    /// cellsX / cellsY set the first-octave lattice resolution (different values stretch the grain).
    /// </summary>
    public static float Tile(float u, float v, int cellsX, int cellsY, int octaves, int seed)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            int px = cellsX << o, py = cellsY << o;
            sum += amp * Value(u * px, v * py, px, py, seed + o * 7919);
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;
}
