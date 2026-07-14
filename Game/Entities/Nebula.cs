using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace StrangeUniverse.Game.Entities;

/// <summary>
/// Generates a world-space nebula texture from three independently domain-warped
/// color layers that blend additively.  Where two layers overlap their hues mix
/// like colored light (red+blue=purple, blue+green=cyan, orange+blue=white, etc.),
/// producing genuine color variety across the cloud.
/// Generated once at startup and rendered as a single world-space quad.
/// </summary>
public class Nebula
{
    public string Id { get; }
    public Texture2D Texture { get; private set; }
    public const int Size = 1024;

    // Large prime stride so each layer's seed range is well separated,
    // producing completely uncorrelated warp and density fields per layer.
    private const int LayerStride = 7919;

    public Nebula(string id)
    {
        Id = id;
        Generate();
    }
    private void Generate()
    {
        int    baseSeed = StaticHelpers.SeedHash(Id);
        var rng    = new Random(baseSeed);
        var pixels = new Color[Size * Size];

        // Pick 3 strongly-contrasting hues for this nebula
        int[]  triplet = StaticHelpers.NebulaTriplets[rng.Next(StaticHelpers.NebulaTriplets.Length)];
        Color  c0      = StaticHelpers.NebulaColorPool[triplet[0]];
        Color  c1      = StaticHelpers.NebulaColorPool[triplet[1]];
        Color  c2      = StaticHelpers.NebulaColorPool[triplet[2]];

        // Pre-normalise to [0,1] float so the inner loop avoids repeated division
        float r0f = c0.R / 255f;  float g0f = c0.G / 255f;  float b0f = c0.B / 255f;
        float r1f = c1.R / 255f;  float g1f = c1.G / 255f;  float b1f = c1.B / 255f;
        float r2f = c2.R / 255f;  float g2f = c2.G / 255f;  float b2f = c2.B / 255f;

        // Each row is independent — safe to parallelise across all CPU cores.
        Parallel.For(0, Size, py =>
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
                float d0 = StaticHelpers.NebulaLayerDensity(u, v, baseSeed + LayerStride * 0) * edge;
                float d1 = StaticHelpers.NebulaLayerDensity(u, v, baseSeed + LayerStride * 1) * edge;
                float d2 = StaticHelpers.NebulaLayerDensity(u, v, baseSeed + LayerStride * 2) * edge;

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
        });

        var tex = new Texture2D(Launcher.GD, Size, Size);
        tex.SetData(pixels);
        Texture = tex;
    }
}
