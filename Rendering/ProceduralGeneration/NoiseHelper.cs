using System;
using Microsoft.Xna.Framework;

namespace StrangeUniverse.Rendering.ProceduralGeneration;

/// <summary>Deterministic value noise and FBm helpers used by all procedural generators.</summary>
public static class NoiseHelper
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

        float v00 = Hash(ix,     iy,     seed);
        float v10 = Hash(ix + 1, iy,     seed);
        float v01 = Hash(ix,     iy + 1, seed);
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
        float value    = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue  = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value    += Noise(x * frequency, y * frequency, seed + i * 1000) * amplitude;
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
        float value     = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue  = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = Noise(x * frequency, y * frequency, seed + i * 1000);
            n = 1f - Math.Abs(n);   // invert to put peaks at 0-crossings
            n = n * n;              // sharpen ridge tips
            value    += n * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return maxValue > 0f ? value / maxValue : 0f;
    }
}
