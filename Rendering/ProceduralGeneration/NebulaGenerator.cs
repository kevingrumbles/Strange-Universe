using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StrangeUniverse.Rendering.ProceduralGeneration;

/// <summary>Generates a large, soft nebula background texture using layered FBm noise.</summary>
public static class NebulaGenerator
{
    private const int Size = 512;

    public static Texture2D Generate(GraphicsDevice gd, int seed)
    {
        var colors = new Color[Size * Size];

        // Pick two complementary nebula hues from the seed
        var rng    = new Random(seed);
        Color hue1 = new Color(
            (byte)rng.Next(50, 120),
            (byte)rng.Next(20,  80),
            (byte)rng.Next(120, 200));
        Color hue2 = new Color(
            (byte)rng.Next(120, 200),
            (byte)rng.Next(60, 130),
            (byte)rng.Next(20,  80));
        Color hue3 = new Color(
            (byte)rng.Next(20,  70),
            (byte)rng.Next(100, 180),
            (byte)rng.Next(80, 160));

        for (int py = 0; py < Size; py++)
        {
            float fy = py / (float)Size;
            for (int px = 0; px < Size; px++)
            {
                float fx = px / (float)Size;

                // Three layers of FBm at different scales
                float n1 = NoiseHelper.Remap01(NoiseHelper.Fbm(fx * 4f, fy * 4f, seed,          5, 0.55f, 2.1f));
                float n2 = NoiseHelper.Remap01(NoiseHelper.Fbm(fx * 2f, fy * 2f, seed + 5000,   4, 0.5f,  2.0f));
                float n3 = NoiseHelper.Remap01(NoiseHelper.Fbm(fx * 7f, fy * 7f, seed + 10000,  3, 0.45f, 1.9f));

                // Density threshold: only render where noise is above threshold
                float density = Math.Max(0f, n1 * 0.6f + n2 * 0.3f + n3 * 0.1f - 0.38f) * 1.6f;
                density = Math.Min(density, 1f);

                if (density < 0.01f) continue;

                // Blend hues by second noise layer
                Color c = Color.Lerp(
                    Color.Lerp(hue1, hue2, n2),
                    hue3, n3 * 0.5f);

                byte alpha = (byte)(density * 75f);   // max 75/255 — subtle
                colors[py * Size + px] = new Color(c.R, c.G, c.B, alpha);
            }
        }

        var tex = new Texture2D(gd, Size, Size);
        tex.SetData(colors);
        return tex;
    }
}
