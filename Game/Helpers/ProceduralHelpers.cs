using Microsoft.Xna.Framework;
using System;

namespace Strange_Universe;

public enum PlanetType { Terran, Rocky, GasGiant, Ice, Lava, Ocean }

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

    /// <summary>Deterministic 32-bit FNV-1a hash of a string.</summary>
    public static int SeedHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261u;

            foreach (char c in s ?? string.Empty)
                hash = (hash ^ c) * 16777619u;

            return (int)hash;
        }
    }

    public static float NextWeightedFloat(
        this Random random,
        float? min,
        float? max,
        int samples = 3)
    {
        float sum = 0;

        for (int i = 0; i < samples; i++)
            sum += (float)random.NextDouble();

        float normalized = sum / samples;

        return (min ?? 0) + normalized * ((max ?? 1) - (min ?? 0));
    }

    public static readonly Color[] StarColors = new[]
    {
        new Color(255, 240, 180),   // warm yellow (G-type)
        new Color(255, 200, 120),   // orange (K-type)
        new Color(255, 160, 80),    // orange-red (M-type)
        new Color(180, 210, 255),   // blue-white (A-type)
    };

    public static Color PlanetMinimapColor(PlanetType type) => type switch
    {
        PlanetType.Terran => new Color(70, 160, 220),
        PlanetType.Rocky => new Color(160, 130, 100),
        PlanetType.GasGiant => new Color(210, 170, 100),
        PlanetType.Ice => new Color(200, 225, 255),
        PlanetType.Lava => new Color(220, 70, 30),
        PlanetType.Ocean => new Color(30, 100, 200),
        _ => Color.LightGray,
    };

    // Eight vivid hues spread across the color wheel so any triplet produces
    // clearly distinct, strongly-contrasting color regions.
    public static readonly Color[] NebulaColorPool =
    {
        new Color(215,  40,  45),   // 0  crimson
        new Color(235, 115,  15),   // 1  orange
        new Color( 40,  75, 220),   // 2  cobalt blue
        new Color( 20, 185,  80),   // 3  emerald green
        new Color(200,  20, 190),   // 4  magenta
        new Color( 80,  15, 215),   // 5  deep purple
        new Color( 15, 195, 215),   // 6  cyan / teal
        new Color(220, 195,  20)};  // 7  gold

    // Each triplet is hand-picked so the three colors are well-separated
    // in hue, guaranteeing visible color variety in every nebula.
    public static readonly int[][] NebulaTriplets =
    {
        new[] { 0, 2, 4 },  // Crimson  + Blue    + Magenta
        new[] { 1, 2, 5 },  // Orange   + Blue    + Purple
        new[] { 0, 3, 2 },  // Crimson  + Emerald + Blue
        new[] { 4, 1, 2 },  // Magenta  + Orange  + Blue
        new[] { 5, 1, 6 },  // Purple   + Orange  + Cyan
        new[] { 2, 3, 4 },  // Blue     + Emerald + Magenta
        new[] { 4, 6, 1 },  // Magenta  + Cyan    + Orange
        new[] { 5, 0, 6 }}; // Purple   + Crimson + Cyan

    /// <summary>
    /// Remaps UV coordinates to a periodic domain using sine/cosine so noise functions
    /// wrap seamlessly at u=0/1 and v=0/1 boundaries. Returns (x, y) in noise space.
    /// </summary>
    private static (float x, float y) MakePeriodicUV(float u, float v)
    {
        float angleU = u * MathHelper.TwoPi;
        float angleV = v * MathHelper.TwoPi;

        // Project to 2D using two circles offset in 4D space
        // This avoids the singularity at the poles of a single circle
        float x = (float)Math.Cos(angleU) + (float)Math.Cos(angleV);
        float y = (float)Math.Sin(angleU) + (float)Math.Sin(angleV);

        return (x, y);
    }

    private static float TileableFbm(float u, float v, int seed,
                                      int octaves = 5, float persistence = 0.5f, float lacunarity = 2f)
    {
        var (x, y) = MakePeriodicUV(u, v);
        return Fbm(x, y, seed, octaves, persistence, lacunarity);
    }

    private static float TileableRidgedFbm(float u, float v, int seed,
                                            int octaves = 4, float persistence = 0.5f, float lacunarity = 2f)
    {
        var (x, y) = MakePeriodicUV(u, v);
        return RidgedFbm(x, y, seed, octaves, persistence, lacunarity);
    }

    /// <summary>
    /// Computes one cloud layer's density at (u, v) using a domain-warped FBm
    /// mixed with ridged noise.  Returns [0, 1]: 0 = dark void, 1 = dense core.
    /// </summary>
    public static float NebulaLayerDensity(float u, float v, int seed)
    {
        float q0 = Fbm(u * 2.8f, v * 2.8f, seed, 3, 0.50f, 2.0f);
        float q1 = Fbm(u * 2.8f + 5.2f, v * 2.8f + 1.3f, seed + 1000, 3, 0.50f, 2.0f);
        float wu = u + q0 * 0.44f;
        float wv = v + q1 * 0.44f;

        float densBase = Remap01(Fbm(wu * 3.5f, wv * 3.5f, seed + 2000, 4, 0.50f, 2.05f));
        float densRidged = RidgedFbm(wu * 2.6f, wv * 2.6f, seed + 4000, 3);

        float total = densBase * 0.65f + densRidged * 0.35f;

        const float Threshold = 0.41f;
        float remapped = Math.Max(0f, total - Threshold) / (1f - Threshold);

        return (float)Math.Pow(remapped, 1.6f);
    }

    /// <summary>
    /// Tileable version of NebulaLayerDensity that wraps seamlessly at u=0/1 and v=0/1.
    /// Used for infinite scrolling nebula backgrounds.
    /// </summary>
    public static float TileableNebulaLayerDensity(float u, float v, int seed)
    {
        float q0 = TileableFbm(u, v, seed, 3, 0.50f, 2.0f);
        float q1 = TileableFbm(u, v, seed + 1000, 3, 0.50f, 2.0f);

        float wu = (u + q0 * 0.44f);
        float wv = (v + q1 * 0.44f);
        wu = wu - (float)Math.Floor(wu);
        wv = wv - (float)Math.Floor(wv);

        float densBase = Remap01(TileableFbm(wu, wv, seed + 2000, 4, 0.50f, 2.05f));
        float densRidged = TileableRidgedFbm(wu, wv, seed + 4000, 3);

        float total = densBase * 0.65f + densRidged * 0.35f;

        const float Threshold = 0.41f;
        float remapped = Math.Max(0f, total - Threshold) / (1f - Threshold);

        return (float)Math.Pow(remapped, 1.6f);
    }
}
