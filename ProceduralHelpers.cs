using Microsoft.Xna.Framework;
using System;
using static StrangeUniverse.StaticHelpers;

namespace Strange_Universe;

/// <summary>Deterministic value noise and FBm helpers used by all procedural generators.</summary>
public static class ProceduralHelpers
{
    /// <summary>Returns a pseudo-random float in [−1, 1] for integer grid coordinates.</summary>
    private static float Hash(int x, int y, int seed)
    {
        int n = x + y * 57 + seed * 131;
        n = (n << 13) ^ n;
        return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
    }

    /// <summary>Smooth value noise: bilinearly interpolated hash on integer grid.</summary>
    public static float Noise(float x, float y, int seed)
    {
        int ix = (int)Math.Floor(x);
        int iy = (int)Math.Floor(y);
        float fx = x - ix;
        float fy = y - iy;

        // Smoothstep for C1 continuity
        float ux = fx * fx * (3f - 2f * fx);
        float uy = fy * fy * (3f - 2f * fy);

        float v00 = Hash(ix, iy, seed);
        float v10 = Hash(ix + 1, iy, seed);
        float v01 = Hash(ix, iy + 1, seed);
        float v11 = Hash(ix + 1, iy + 1, seed);

        return MathHelper.Lerp(
            MathHelper.Lerp(v00, v10, ux),
            MathHelper.Lerp(v01, v11, ux),
            uy);
    }

    /// <summary>
    /// Fractal Brownian Motion: sums octaves of noise for natural-looking variation.
    /// Returns a value approximately in [−1, 1].
    /// </summary>
    public static float Fbm(float x, float y, int seed,
                            int octaves = 5, float persistence = 0.5f, float lacunarity = 2f)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Noise(x * frequency, y * frequency, seed + i * 1000) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return value / maxValue;
    }

    /// <summary>Remaps a value from [−1,1] to [0,1].</summary>
    public static float Remap01(float v) => (v + 1f) * 0.5f;

    /// <summary>
    /// Ridged multifractal noise: inverts and squares each octave to produce
    /// sharp ridges and peaks — used to create cloud-edge structure.
    /// Returns a value in approximately [0, 1].
    /// </summary>
    public static float RidgedFbm(float x, float y, int seed,
                                   int octaves = 4, float persistence = 0.5f, float lacunarity = 2f)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = Noise(x * frequency, y * frequency, seed + i * 1000);
            n = 1f - Math.Abs(n);   // invert to put peaks at 0-crossings
            n = n * n;              // sharpen ridge tips
            value += n * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return maxValue > 0f ? value / maxValue : 0f;
    }
    public static Color GetSurfaceColor(PlanetType type, float n, int py, int seed)
    {
        return type switch
        {
            PlanetType.Terran => GetTerranColor(n),
            PlanetType.Rocky => GetRockyColor(n),
            PlanetType.GasGiant => GetGasGiantColor(n, py, seed),
            PlanetType.Ice => GetIceColor(n),
            PlanetType.Lava => GetLavaColor(n),
            PlanetType.Ocean => GetOceanColor(n),
            _ => Color.Gray,
        };
    }

    public static Color GetTerranColor(float n)
    {
        if (n < 0.42f) return Color.Lerp(new Color(30, 80, 160), new Color(45, 100, 190), n / 0.42f);
        if (n < 0.55f) return Color.Lerp(new Color(200, 175, 110), new Color(60, 130, 50), (n - 0.42f) / 0.13f);
        if (n < 0.78f) return Color.Lerp(new Color(60, 130, 50), new Color(110, 90, 60), (n - 0.55f) / 0.23f);
        return Color.Lerp(new Color(110, 90, 60), new Color(220, 215, 210), (n - 0.78f) / 0.22f);
    }

    public static Color GetRockyColor(float n)
    {
        var dark = new Color(90, 70, 55);
        var mid = new Color(140, 110, 85);
        var light = new Color(175, 155, 130);
        if (n < 0.5f) return Color.Lerp(dark, mid, n * 2f);
        return Color.Lerp(mid, light, (n - 0.5f) * 2f);
    }

    public static Color GetGasGiantColor(float n, int py, int seed)
    {
        // Horizontal banding
        float band = ProceduralHelpers.Remap01(ProceduralHelpers.Noise(py * 0.05f, 0f, seed + 9999)) * 0.3f;
        float t = (n + band) % 1f;
        if (t < 0.33f) return Color.Lerp(new Color(200, 150, 80), new Color(230, 180, 100), t * 3f);
        if (t < 0.66f) return Color.Lerp(new Color(190, 120, 60), new Color(210, 160, 90), (t - 0.33f) * 3f);
        return Color.Lerp(new Color(220, 170, 95), new Color(200, 130, 70), (t - 0.66f) * 3f);
    }

    public static Color GetIceColor(float n)
    {
        var deep = new Color(180, 210, 240);
        var mid = new Color(215, 230, 245);
        var white = new Color(240, 245, 255);
        if (n < 0.5f) return Color.Lerp(deep, mid, n * 2f);
        return Color.Lerp(mid, white, (n - 0.5f) * 2f);
    }

    public static Color GetLavaColor(float n)
    {
        if (n < 0.35f) return Color.Lerp(new Color(25, 15, 10), new Color(80, 20, 5), n / 0.35f);
        if (n < 0.5f) return Color.Lerp(new Color(80, 20, 5), new Color(200, 60, 0), (n - 0.35f) / 0.15f);
        return Color.Lerp(new Color(200, 60, 0), new Color(255, 200, 20), (n - 0.5f) * 2f);
    }

    public static Color GetOceanColor(float n)
    {
        if (n < 0.6f) return Color.Lerp(new Color(20, 60, 140), new Color(30, 90, 180), n / 0.6f);
        return Color.Lerp(new Color(30, 90, 180), new Color(60, 130, 200), (n - 0.6f) * 2.5f);
    }

    public static Color GetAtmosphereColor(PlanetType type) => type switch
    {
        PlanetType.Terran => new Color(100, 160, 255),
        PlanetType.Ice => new Color(200, 230, 255),
        PlanetType.Ocean => new Color(80, 140, 220),
        PlanetType.GasGiant => new Color(220, 170, 100),
        PlanetType.Lava => new Color(200, 80, 20),
        _ => new Color(160, 140, 120),
    };

    public static readonly Point[] GalaxyConnectionPreferredDirections =
    {
        // Cardinals
        new( 1, 0), new(-1, 0), new( 0, 1), new( 0,-1),

        // Diagonals
        new( 1, 1), new(-1, 1), new( 1,-1), new(-1,-1),

        // 2:1
        new( 2, 1), new(-2, 1), new( 2,-1), new(-2,-1),
        new( 1, 2), new(-1, 2), new( 1,-2), new(-1,-2),

        // 3:1
        new( 3, 1), new(-3, 1), new( 3,-1), new(-3,-1),
        new( 1, 3), new(-1, 3), new( 1,-3), new(-1,-3),

        // 3:2
        new( 3, 2), new(-3, 2), new( 3,-2), new(-3,-2),
        new( 2, 3), new(-2, 3), new( 2,-3), new(-2,-3),

        // Long shallow
        new( 4, 1), new(-4, 1), new( 4,-1), new(-4,-1),
        new( 1, 4), new(-1, 4), new( 1,-4), new(-1,-4),
    };
    public static Point NormalizeDirection(int x, int y)
    {
        int gcd = Gcd(Math.Abs(x), Math.Abs(y));
        return new Point(x / gcd, y / gcd);
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a == 0 ? 1 : a;
    }
}
