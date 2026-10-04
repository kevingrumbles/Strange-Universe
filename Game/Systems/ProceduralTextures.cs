using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe;
using System;

namespace Strange_Universe.Game.Systems;

/// <summary>Procedural texture generators for world bodies. Render side only; main thread only.</summary>
public static class ProceduralTextures
{
    public const int AsteroidSize = 128;
    public const int PlanetSize = 256;
    public const int StarSize = 512;

    /// <summary>
    /// Generates an irregular rocky asteroid texture with bump-mapped lighting,
    /// layered surface detail, cracks, and a natural rock color palette.
    /// </summary>
    public static Texture2D Asteroid(GraphicsDevice gd, int seed)
    {
        var rng = new Random(seed);
        var colors = new Color[AsteroidSize * AsteroidSize];
        float cx = AsteroidSize * 0.5f;
        float cy = AsteroidSize * 0.5f;
        float baseR = cx * 0.80f;

        // -- Silhouette: 24 radial control points, smoothstep-interpolated ------
        const int Samples = 24;
        float[] sampleAngles = new float[Samples];
        float[] sampleRadii = new float[Samples];
        for (int i = 0; i < Samples; i++)
        {
            sampleAngles[i] = MathHelper.TwoPi * i / Samples;
            float disp = (float)(rng.NextDouble() * 0.40 + 0.60);   // 0.60–1.00
            sampleRadii[i] = baseR * disp;
        }

        // -- Light direction: upper-left, lifted above plane ---------------------
        float lx = -0.55f, ly = -0.45f, lz = 0.70f;
        float ll = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz);
        lx /= ll; ly /= ll; lz /= ll;

        for (int py = 0; py < AsteroidSize; py++)
        {
            for (int px = 0; px < AsteroidSize; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float angle = (float)Math.Atan2(dy, dx);

                // Low-freq control-point radius at this angle
                float asteroidR = MathHelpers.AsteroidInterpolatedRadius(angle, sampleAngles, sampleRadii);

                // High-freq edge bumps - small protrusions and chipped indentations
                float edgeBump = ProceduralHelpers.Fbm(
                    (float)Math.Cos(angle) * 4.5f,
                    (float)Math.Sin(angle) * 4.5f,
                    seed + 91, 3, 0.50f, 2f);
                asteroidR += edgeBump * baseR * 0.085f;   // ±8.5% fine-detail bumps

                if (dist > asteroidR + 1.5f) continue;   // early-out past AA fringe

                float normDist = dist / Math.Max(asteroidR, 0.001f);

                float u = dx / baseR;
                float v = dy / baseR;

                // -- Surface layers ----------------------------------------------
                // Layer 1 – large rocky regions
                float rocky = ProceduralHelpers.Remap01(
                    ProceduralHelpers.Fbm(u * 2.0f, v * 2.0f, seed, 5, 0.55f, 2.0f));
                // Layer 2 – fine surface grain
                float grain = ProceduralHelpers.Remap01(
                    ProceduralHelpers.Fbm(u * 5.0f, v * 5.0f, seed + 17, 3, 0.50f, 2.0f));
                // Layer 3 – cracks via ridged noise, sharpened
                float crack = ProceduralHelpers.RidgedFbm(
                    u * 4.5f, v * 4.5f, seed + 43, 4, 0.55f, 2.1f);
                crack = (float)Math.Pow(crack, 1.6);

                // -- Bump-mapped surface normal ----------------------------------
                // Central-difference gradient of an FBm height field
                const float Eps = 0.035f;
                const float BumpStr = 0.55f;
                float h = ProceduralHelpers.Remap01(ProceduralHelpers.Fbm(u * 2.5f, v * 2.5f, seed + 7, 4, 0.50f, 2f));
                float hx = ProceduralHelpers.Remap01(ProceduralHelpers.Fbm((u + Eps) * 2.5f, v * 2.5f, seed + 7, 4, 0.50f, 2f));
                float hy = ProceduralHelpers.Remap01(ProceduralHelpers.Fbm(u * 2.5f, (v + Eps) * 2.5f, seed + 7, 4, 0.50f, 2f));
                float bx = (hx - h) / Eps * BumpStr;
                float by = (hy - h) / Eps * BumpStr;

                // Sphere base normal (implicit sphere that fits the asteroid body)
                float snx = dx / baseR;
                float sny = dy / baseR;
                float snz = (float)Math.Sqrt(Math.Max(0f, 1f - snx * snx - sny * sny));

                float nx = snx + bx;
                float ny = sny + by;
                float nz = snz;
                float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl > 0.001f) { nx /= nl; ny /= nl; nz /= nl; }

                float diffuse = Math.Max(0f, nx * lx + ny * ly + nz * lz);
                float lighting = 0.25f + 0.75f * diffuse;   // ambient + Lambert

                // -- Color palette -----------------------------------------------
                // Blend from dark gray-brown ? medium warm gray using surface mix
                float blend = rocky * 0.60f + grain * 0.40f;

                float cr = MathHelper.Lerp(48f, 148f, blend);
                float cg = MathHelper.Lerp(44f, 130f, blend);
                float cb = MathHelper.Lerp(38f, 105f, blend);

                // Brown-tan warm shift in mid tones
                float warm = (float)Math.Max(0.0, Math.Sin(blend * Math.PI));
                cr += warm * 22f;
                cg += warm * 12f;
                cb -= warm * 2f;

                // Crack network darkens the surface
                float crackDark = 1f - crack * 0.50f;
                cr *= crackDark;
                cg *= crackDark;
                cb *= crackDark;

                // Apply lighting
                cr *= lighting;
                cg *= lighting;
                cb *= lighting;

                // Rim ambient-occlusion - darken toward the silhouette edge
                float rim = (float)Math.Pow(Math.Max(0f, 1f - normDist), 0.28f);
                cr *= rim;
                cg *= rim;
                cb *= rim;

                // Soft anti-aliased silhouette edge
                float alpha = normDist > 0.93f
                    ? Math.Clamp((1f - normDist) / 0.07f, 0f, 1f)
                    : 1f;

                colors[py * AsteroidSize + px] = new Color(
                    (byte)Math.Clamp(cr, 0f, 255f),
                    (byte)Math.Clamp(cg, 0f, 255f),
                    (byte)Math.Clamp(cb, 0f, 255f),
                    (byte)(alpha * 255f));
            }
        }

        var tex = new Texture2D(gd, AsteroidSize, AsteroidSize);
        tex.SetData(colors);
        return tex;
    }

    /// <summary>Generates a procedural planet texture.</summary>
    public static Texture2D Planet(GraphicsDevice gd, PlanetType type, int seed)
    {
        var colors = new Color[PlanetSize * PlanetSize];
        float cx = PlanetSize * 0.5f;
        float cy = PlanetSize * 0.5f;
        float r = cx - 2;

        // Light direction (slightly above left)
        var light = Vector3.Normalize(new Vector3(-0.4f, -0.6f, 0.8f));

        for (int py = 0; py < PlanetSize; py++)
        {
            for (int px = 0; px < PlanetSize; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 > r * r) continue;

                float dist = (float)Math.Sqrt(d2);
                float normDist = dist / r;

                // Sphere normal (Lambert)
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - normDist * normDist));
                var normal = Vector3.Normalize(new Vector3(dx / r, dy / r, nz));
                float diffuse = Math.Max(0f, Vector3.Dot(normal, light));
                float ambient = 0.25f;
                float lighting = ambient + (1f - ambient) * diffuse;

                // Sample noise for surface detail
                float noiseScale = 3.5f;
                float nx = (dx / r + 1f) * noiseScale;
                float ny = (dy / r + 1f) * noiseScale;
                float n = ProceduralHelpers.Remap01(ProceduralHelpers.Fbm(nx, ny, seed, 6, 0.5f, 2f));

                // Atmosphere edge glow
                float edgeFactor = 1f - normDist;

                Color surfaceColor = ProceduralHelpers.GetSurfaceColor(type, n, py, seed);

                // Darken surface by lighting
                surfaceColor = new Color(
                    (int)(surfaceColor.R * lighting),
                    (int)(surfaceColor.G * lighting),
                    (int)(surfaceColor.B * lighting));

                // Atmospheric rim
                Color atmColor = ProceduralHelpers.GetAtmosphereColor(type);
                float rimStrength = (float)Math.Pow(1f - edgeFactor, 4f);
                surfaceColor = Color.Lerp(surfaceColor, atmColor, rimStrength * 0.7f);

                // Planet body is fully opaque; only the outermost 5% fades for a soft edge
                byte alpha = normDist < 0.95f
                    ? (byte)255
                    : (byte)(Math.Max(0f, 1f - (normDist - 0.95f) / 0.05f) * 255f);
                colors[py * PlanetSize + px] = new Color(surfaceColor.R, surfaceColor.G, surfaceColor.B, alpha);
            }
        }

        var tex = new Texture2D(gd, PlanetSize, PlanetSize);
        tex.SetData(colors);
        return tex;
    }

    /// <summary>
    /// Generates a glowing star texture: bright core, coloured corona, soft halo, and ray spikes.
    /// </summary>
    public static Texture2D Star(GraphicsDevice gd, Color starColor, Random rng)
    {
        var colors = new Color[StarSize * StarSize];
        float cx = StarSize * 0.5f;
        float cy = StarSize * 0.5f;
        float maxR = cx;

        // Generate 8 ray angles
        float[] rayAngles = new float[8];
        for (int i = 0; i < 8; i++)
            rayAngles[i] = MathHelper.TwoPi * i / 8f + (float)rng.NextDouble() * 0.2f;

        for (int py = 0; py < StarSize; py++)
        {
            for (int px = 0; px < StarSize; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float normDist = dist / maxR;   // 0 = centre, 1 = edge

                if (normDist > 1f) continue;

                // Core glow — inverse square falloff
                float glow = Math.Max(0f, 1f - normDist);
                glow = glow * glow * glow;

                // Ray contribution
                float angle = (float)Math.Atan2(dy, dx);
                float rayContrib = 0f;
                foreach (float ra in rayAngles)
                {
                    float angleDiff = Math.Abs(MathHelpers.StarDeltaAngle(angle, ra));
                    float rayWidth = 0.04f + normDist * 0.03f;
                    if (angleDiff < rayWidth)
                    {
                        float rayFall = 1f - angleDiff / rayWidth;
                        rayContrib = Math.Max(rayContrib, rayFall * (1f - normDist) * 0.6f);
                    }
                }

                float totalLight = Math.Min(1f, glow + rayContrib);
                if (totalLight <= 0f) continue;

                // Colour: white at core, starColor mid, transparent at edge
                Color pixel;
                if (normDist < 0.08f)
                    pixel = Color.Lerp(Color.White, starColor, normDist / 0.08f);
                else
                    pixel = Color.Lerp(starColor, Color.Transparent, (normDist - 0.08f) / 0.92f);

                byte alpha = (byte)Math.Min(255, (int)(totalLight * 255f));
                colors[py * StarSize + px] = new Color(pixel.R, pixel.G, pixel.B, alpha);
            }
        }

        var tex = new Texture2D(gd, StarSize, StarSize);
        tex.SetData(colors);
        return tex;
    }
}
