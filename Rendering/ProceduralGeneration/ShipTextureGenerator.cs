using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StrangeUniverse.Rendering.ProceduralGeneration;

/// <summary>Generates a simple arrow-shaped player ship texture pointing right (+X).</summary>
public static class ShipTextureGenerator
{
    private const int W = 64;
    private const int H = 40;

    public static Texture2D Generate(GraphicsDevice gd)
    {
        var colors = new Color[W * H];
        float cx = W * 0.5f;
        float cy = H * 0.5f;

        // Hull: main body (right-pointing elongated shape)
        var hullColor   = new Color(180, 195, 215);
        var wingColor   = new Color(130, 145, 165);
        var nozzleColor = new Color(100, 110, 130);
        var glowColor   = new Color(80, 160, 255, 200);

        for (int py = 0; py < H; py++)
        {
            for (int px = 0; px < W; px++)
            {
                float nx = (px - 2f)   / (W - 4f);   // 0..1 left to right
                float ny = (py - cy)   / cy;           // -1..1 top to bottom

                // Nose cone: right 30%
                if (nx > 0.70f)
                {
                    float tipWidth = (1f - nx) / 0.30f;  // 1 at 70%, 0 at 100%
                    if (Math.Abs(ny) < tipWidth * 0.45f)
                        colors[py * W + px] = Color.Lerp(hullColor, Color.White, (nx - 0.70f) / 0.30f);
                    continue;
                }

                // Main body: centre stripe
                if (nx >= 0.15f && nx <= 0.70f && Math.Abs(ny) < 0.42f)
                {
                    float shade = 1f - Math.Abs(ny) * 0.3f;
                    colors[py * W + px] = new Color(
                        (int)(hullColor.R * shade),
                        (int)(hullColor.G * shade),
                        (int)(hullColor.B * shade));
                    continue;
                }

                // Wings: triangular sweep from left
                if (nx >= 0.05f && nx <= 0.65f)
                {
                    float maxWingY = (0.65f - nx) / 0.60f;  // widens toward tail
                    if (Math.Abs(ny) < maxWingY * 1.0f && Math.Abs(ny) >= 0.38f)
                    {
                        colors[py * W + px] = wingColor;
                        continue;
                    }
                }

                // Engine nozzle: left 15%
                if (nx < 0.15f && Math.Abs(ny) < 0.28f)
                {
                    colors[py * W + px] = nozzleColor;
                    continue;
                }

                // Engine glow
                if (nx < 0.07f && Math.Abs(ny) < 0.18f)
                {
                    colors[py * W + px] = glowColor;
                }
            }
        }

        var tex = new Texture2D(gd, W, H);
        tex.SetData(colors);
        return tex;
    }
}
