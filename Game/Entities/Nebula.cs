using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace StrangeUniverse.Game.Entities;

/// <summary>
/// Generates a seamless tileable nebula texture from three independently domain-warped
/// color layers that blend additively.  Where two layers overlap their hues mix
/// like colored light (red+blue=purple, blue+green=cyan, orange+blue=white, etc.),
/// producing genuine color variety across the cloud.
/// Generated once at startup and used for infinite scrolling background.
/// </summary>
public class Nebula : IDisposable
{
    public string Id { get; }
    public Texture2D Texture { get; private set; }

    // Configuration constants
    public const int Size = 4096;              // Large texture for varied scrolling
    public const float BaseOpacity = 0.70f;    // Base alpha multiplier
    public const float EdgeFadeWidth = 0.08f;  // Fade width at tile edges (0.08 = 8% on each side)
    public const float ParallaxFactor = 0.05f; // Parallax scroll rate (slower than stars)

    // Large prime stride so each layer's seed range is well separated,
    // producing completely uncorrelated warp and density fields per layer.
    private const int LayerStride = 7919;
    public float Density = 1.0f;         // Overall density multiplier (higher = more visible nebula)
    public float Brightness = 0.80f;      // Color brightness multiplier

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
        Density = rng.NextWeightedFloat(.80f, 3.0f);
        Brightness = rng.NextWeightedFloat(0.40f, 1.0f);

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
            float v = py / (float)Size;   // [0, 1] - wraps at edges

            for (int px = 0; px < Size; px++)
            {
                float u = px / (float)Size;   // [0, 1] - wraps at edges

                // Calculate edge fade - fade to transparent near texture borders
                // This creates a smooth blend between tiles, hiding seams
                float edgeFadeU = CalculateEdgeFade(u);
                float edgeFadeV = CalculateEdgeFade(v);
                float edgeFade = edgeFadeU * edgeFadeV;

                // Three independent tileable cloud layers
                // Each layer uses tileable noise so the texture wraps seamlessly
                float d0 = StaticHelpers.TileableNebulaLayerDensity(u, v, baseSeed + LayerStride * 0) * Density;
                float d1 = StaticHelpers.TileableNebulaLayerDensity(u, v, baseSeed + LayerStride * 1) * Density;
                float d2 = StaticHelpers.TileableNebulaLayerDensity(u, v, baseSeed + LayerStride * 2) * Density;

                float maxDens = Math.Max(d0, Math.Max(d1, d2));
                if (maxDens < 0.01f) continue;

                // Apply edge fade to density
                d0 *= edgeFade;
                d1 *= edgeFade;
                d2 *= edgeFade;
                maxDens *= edgeFade;

                // Additive colour accumulation with brightness multiplier
                float r = (d0 * r0f + d1 * r1f + d2 * r2f) * Brightness;
                float g = (d0 * g0f + d1 * g1f + d2 * g2f) * Brightness;
                float b = (d0 * b0f + d1 * b1f + d2 * b2f) * Brightness;

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

                // Alpha based on density, using BaseOpacity constant
                byte alpha = (byte)(maxDens * 255f * BaseOpacity);

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

    /// <summary>
    /// Calculates a smooth fade from 1.0 at the center to 0.0 at the edges.
    /// Uses smoothstep for a natural falloff that hides tile seams.
    /// </summary>
    private static float CalculateEdgeFade(float t)
    {
        // Distance from nearest edge (0.0 at edges, 0.5 at center)
        float distFromEdge = Math.Min(t, 1.0f - t);

        // Normalize to fade range using the configurable EdgeFadeWidth constant
        float fade = Math.Clamp(distFromEdge / EdgeFadeWidth, 0f, 1f);

        // Apply smoothstep for smooth transition
        return fade * fade * (3f - 2f * fade);
    }

    public void Dispose()
    {
        Texture?.Dispose();
        Texture = null!;
    }
}
