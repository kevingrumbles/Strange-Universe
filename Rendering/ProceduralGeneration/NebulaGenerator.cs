using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StrangeUniverse.Rendering.ProceduralGeneration;

/// <summary>
/// Generates a world-space nebula texture from three independently domain-warped
/// color layers that blend additively.  Where two layers overlap their hues mix
/// like colored light (red+blue=purple, blue+green=cyan, orange+blue=white, etc.),
/// producing genuine color variety across the cloud.
/// Generated once at startup and rendered as a single world-space quad.
/// </summary>
public static class NebulaGenerator
{
    private const int Size = 2048;

    // Eight vivid hues spread across the color wheel so any triplet produces
    // clearly distinct, strongly-contrasting color regions.
    private static readonly Color[] ColorPool =
    {
        new Color(215,  40,  45),   // 0  crimson
        new Color(235, 115,  15),   // 1  orange
        new Color( 40,  75, 220),   // 2  cobalt blue
        new Color( 20, 185,  80),   // 3  emerald green
        new Color(200,  20, 190),   // 4  magenta
        new Color( 80,  15, 215),   // 5  deep purple
        new Color( 15, 195, 215),   // 6  cyan / teal
        new Color(220, 195,  20),   // 7  gold
    };

    // Each triplet is hand-picked so the three colors are well-separated
    // in hue, guaranteeing visible color variety in every nebula.
    private static readonly int[][] Triplets =
    {
        new[] { 0, 2, 4 },  // Crimson  + Blue    + Magenta
        new[] { 1, 2, 5 },  // Orange   + Blue    + Purple
        new[] { 0, 3, 2 },  // Crimson  + Emerald + Blue
        new[] { 4, 1, 2 },  // Magenta  + Orange  + Blue
        new[] { 5, 1, 6 },  // Purple   + Orange  + Cyan
        new[] { 2, 3, 4 },  // Blue     + Emerald + Magenta
        new[] { 4, 6, 1 },  // Magenta  + Cyan    + Orange
        new[] { 5, 0, 6 },  // Purple   + Crimson + Cyan
    };

    // Large prime stride so each layer's seed range is well separated,
    // producing completely uncorrelated warp and density fields per layer.
    private const int LayerStride = 7919;

    public static Texture2D Generate(int seed)
    {
        var rng    = new Random(seed);
        var pixels = new Color[Size * Size];

        // Pick 3 strongly-contrasting hues for this nebula
        int[]  triplet = Triplets[rng.Next(Triplets.Length)];
        Color  c0      = ColorPool[triplet[0]];
        Color  c1      = ColorPool[triplet[1]];
        Color  c2      = ColorPool[triplet[2]];

        // Pre-normalise to [0,1] float so the inner loop avoids repeated division
        float r0f = c0.R / 255f;  float g0f = c0.G / 255f;  float b0f = c0.B / 255f;
        float r1f = c1.R / 255f;  float g1f = c1.G / 255f;  float b1f = c1.B / 255f;
        float r2f = c2.R / 255f;  float g2f = c2.G / 255f;  float b2f = c2.B / 255f;

        for (int py = 0; py < Size; py++)
        {
            float v = py / (float)(Size - 1);   // [0, 1]

            for (int px = 0; px < Size; px++)
            {
                float u = px / (float)(Size - 1);   // [0, 1]

                // ── Radial edge fade ─────────────────────────────────────────
                // Fades the nebula to transparent near the texture border so
                // there are never hard rectangular edges visible in the world.
                float cx   = u - 0.5f;
                float cy   = v - 0.5f;
                float dist = (float)Math.Sqrt(cx * cx + cy * cy) / 0.5f;
                float edge = Math.Max(0f, 1f - (float)Math.Pow(dist * 0.88f, 3.5f));
                if (edge < 0.01f) continue;

                // ── Three independent cloud layers ───────────────────────────
                // Each layer is domain-warped with its own seed so its swirl
                // pattern is unique.  Their RGB contributions accumulate
                // additively — exactly like mixing coloured gas emission.
                // Distinct regions glow their own hue; overlaps produce mixed
                // secondary colours (crimson+blue=purple, blue+cyan=teal, etc.)
                float d0 = LayerDensity(u, v, seed + LayerStride * 0) * edge;
                float d1 = LayerDensity(u, v, seed + LayerStride * 1) * edge;
                float d2 = LayerDensity(u, v, seed + LayerStride * 2) * edge;

                float maxDens = Math.Max(d0, Math.Max(d1, d2));
                if (maxDens < 0.01f) continue;

                // Additive colour accumulation
                float r = d0 * r0f + d1 * r1f + d2 * r2f;
                float g = d0 * g0f + d1 * g1f + d2 * g2f;
                float b = d0 * b0f + d1 * b1f + d2 * b2f;

                // Luminance cap: prevents heavily-overlapping regions from
                // washing out to near-white while preserving the hue direction.
                float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                if (lum > 0.82f)
                {
                    float inv = 0.82f / lum;
                    r *= inv;
                    g *= inv;
                    b *= inv;
                }

                // Max alpha ~180 so background stars bleed through visibly
                byte alpha = (byte)(maxDens * 180f);

                pixels[py * Size + px] = new Color(
                    (byte)Math.Min(255, (int)(r * 255f)),
                    (byte)Math.Min(255, (int)(g * 255f)),
                    (byte)Math.Min(255, (int)(b * 255f)),
                    alpha);
            }
        }

        var tex = new Texture2D(Launcher.GD, Size, Size);
        tex.SetData(pixels);
        return tex;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes one cloud layer's density at (u, v) using a domain-warped FBm
    /// mixed with ridged noise.  Returns [0, 1]: 0 = dark void, 1 = dense core.
    /// Each unique <paramref name="seed"/> produces an entirely different shape.
    /// </summary>
    private static float LayerDensity(float u, float v, int seed)
    {
        // Domain warp: displace the sample point with FBm so the resulting
        // cloud has organic curves, spirals, and trailing tendrils rather than
        // the repeating blobs that plain FBm produces.
        float q0 = NoiseHelper.Fbm(u * 2.8f,        v * 2.8f,        seed,        3, 0.50f, 2.0f);
        float q1 = NoiseHelper.Fbm(u * 2.8f + 5.2f, v * 2.8f + 1.3f, seed + 1000, 3, 0.50f, 2.0f);
        float wu  = u + q0 * 0.44f;
        float wv  = v + q1 * 0.44f;

        // Primary cloud mass
        float densBase   = NoiseHelper.Remap01(
            NoiseHelper.Fbm(wu * 3.5f, wv * 3.5f, seed + 2000, 4, 0.50f, 2.05f));

        // Ridged layer: bright filaments and sharpened cloud edges
        float densRidged = NoiseHelper.RidgedFbm(wu * 2.6f, wv * 2.6f, seed + 4000, 3);

        float total = densBase * 0.65f + densRidged * 0.35f;

        // Threshold removes thin uniform haze, leaving distinct cloud masses
        // separated by genuine dark voids.
        const float Threshold = 0.41f;
        float remapped = Math.Max(0f, total - Threshold) / (1f - Threshold);

        // Power curve: widens the contrast gap between thin wisps and dense cores
        return (float)Math.Pow(remapped, 1.6f);
    }
}
