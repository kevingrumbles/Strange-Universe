using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StrangeUniverse.Rendering.ProceduralGeneration;

public static class AsteroidTextureGenerator
{
    private const int Size = 128;

    /// <summary>
    /// Generates an irregular blob asteroid texture using radial polygon displacement + noise.
    /// </summary>
    public static Texture2D Generate(GraphicsDevice gd, int seed)
    {
        var rng    = new Random(seed);
        var colors = new Color[Size * Size];
        float cx   = Size * 0.5f;
        float cy   = Size * 0.5f;
        float baseR = cx * 0.80f;

        // Build a radial profile: sample N angles with displaced radii
        const int Samples = 24;
        float[] sampleAngles = new float[Samples];
        float[] sampleRadii  = new float[Samples];

        for (int i = 0; i < Samples; i++)
        {
            sampleAngles[i] = MathHelper.TwoPi * i / Samples;
            float disp = (float)(rng.NextDouble() * 0.42 + 0.58);  // 0.58..1.0
            sampleRadii[i]  = baseR * disp;
        }

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist  = (float)Math.Sqrt(dx * dx + dy * dy);
                float angle = (float)Math.Atan2(dy, dx);

                // Interpolate asteroid radius at this angle
                float asteroidR = InterpolatedRadius(angle, sampleAngles, sampleRadii);

                if (dist > asteroidR) continue;

                float normDist = dist / asteroidR;

                // Surface noise
                float noiseVal = NoiseHelper.Remap01(
                    NoiseHelper.Fbm(dx / baseR * 2.5f, dy / baseR * 2.5f, seed, 4, 0.55f, 2f));

                // Base grey-brown rock color
                byte baseR2  = (byte)(80  + (int)(noiseVal * 60f));
                byte baseG   = (byte)(65  + (int)(noiseVal * 48f));
                byte baseB   = (byte)(55  + (int)(noiseVal * 38f));

                // Edge darkening
                float edge = 1f - normDist;
                float dark = (float)Math.Pow(edge, 0.4f);
                baseR2 = (byte)(baseR2 * dark);
                baseG  = (byte)(baseG  * dark);
                baseB  = (byte)(baseB  * dark);

                // Soft anti-aliased edge
                float alpha = normDist > 0.9f ? (1f - normDist) / 0.1f : 1f;
                byte  a     = (byte)(alpha * 255f);
                colors[py * Size + px] = new Color(baseR2, baseG, baseB, a);
            }
        }

        var tex = new Texture2D(gd, Size, Size);
        tex.SetData(colors);
        return tex;
    }

    /// <summary>Linearly interpolates the radial profile between the nearest two sample angles.</summary>
    private static float InterpolatedRadius(float angle, float[] angles, float[] radii)
    {
        int n = angles.Length;
        // Normalise angle to [0, 2π)
        float a = (angle + MathHelper.TwoPi) % MathHelper.TwoPi;

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float a0 = angles[i];
            float a1 = angles[next];
            if (next == 0) a1 += MathHelper.TwoPi;

            if (a >= a0 && a < a1)
            {
                float t = (a - a0) / (a1 - a0);
                return MathHelper.Lerp(radii[i], radii[next], t);
            }
        }
        return radii[0];
    }
}
