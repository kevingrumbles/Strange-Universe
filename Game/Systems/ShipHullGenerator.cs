using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Helpers;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// A generated ship: its lit hull picture (premultiplied RGBA, pointing right, <see cref="Size"/> square) and where its engines are.
/// Positions are in the picture's own space: -1 to 1 on each axis from the centre, x toward the nose, y down.
/// </summary>
public sealed class ShipHull
{
    public const int Size = 256;

    public uint[] Pixels { get; init; }

    /// <summary>Where each engine's flame starts (the back of the nozzle).</summary>
    public Vector2[] Engines { get; init; }
}

/// <summary>
/// Builds a ship's picture from its <see cref="ShipAppearance"/>: a fuselage whose width changes along its length in separate armour sections, wings,
/// engine pods and a cockpit, all mirrored about the ship's axis, then lit, panelled and edged. The same appearance always makes the same ship.
/// Pure pixel work with no graphics library, so it is the same on any machine and runs without a window.
/// </summary>
public static class ShipHullGenerator
{
    private const int Oversample = 2;
    private const int Grid = ShipHull.Size * Oversample;

    private const float Tail = -0.9f;
    private const float Nose = 0.95f;
    private const float NozzleX = -0.95f;
    private const float PodHalfWidth = 0.075f;
    private const int Sections = 4;

    public const int FlameWidth = 32;
    public const int FlameHeight = 64;

    private enum Mat : byte { None, Hull, Accent, Glass, Engine }

    private struct Cell
    {
        public float H;
        public Mat M;
    }

    private sealed class Wing
    {
        public Vector2 FrontRoot, BackRoot, BackTip, FrontTip;
        public float RootY, Span;
    }

    private sealed class Design
    {
        public float Body;
        public readonly float[] Profile = new float[7];
        public readonly float[] SectionScale = new float[Sections];
        public readonly List<Wing> Wings = new();
        public readonly List<float> EngineY = new();
        public float ConeX = 0.5f, ConeRadiusX = 0.2f, ConeRadiusY;
        public float SeamOffsetX, SeamOffsetY;

        public Cell At(float x, float y)
        {
            var best = new Cell();
            float ay = MathF.Abs(y);

            void Put(float h, Mat m)
            {
                if (h > best.H) { best.H = h; best.M = m; }
            }

            float t = (x - Tail) / (Nose - Tail);
            if (t is >= 0f and <= 1f)
            {
                float w = HalfWidth(t);
                if (ay < w)
                {
                    float u = ay / w;
                    Put(0.45f + 0.4f * MathF.Sqrt(1f - u * u), u < 0.1f && t > 0.3f ? Mat.Accent : Mat.Hull);
                }
            }

            foreach (var wing in Wings)
                if (InWing(wing, x, ay))
                {
                    float along = (ay - wing.RootY) / (wing.Span - wing.RootY);
                    Put(0.28f + 0.12f * (1f - along), along > 0.75f ? Mat.Accent : Mat.Hull);
                }

            foreach (float py in EngineY)
            {
                float d = MathF.Abs(y - py) / PodHalfWidth;
                if (d < 1f && x is >= NozzleX and <= -0.5f)
                    Put(0.5f + 0.15f * MathF.Sqrt(1f - d * d), x < -0.88f ? Mat.Engine : Mat.Hull);

                // A strut ties a pod that sits outboard of the fuselage back to it.
                if (MathF.Abs(py) > Body * 0.8f && ay < MathF.Abs(py) && x is >= -0.62f and <= -0.52f)
                    Put(0.35f, Mat.Hull);
            }

            if (best.H > 0f)
            {
                float dx = (x - ConeX) / ConeRadiusX, dy = y / ConeRadiusY;
                float d2 = dx * dx + dy * dy;
                if (d2 < 1f)
                    Put(0.95f - 0.2f * d2, Mat.Glass);
            }
            return best;
        }

        /// <summary>The fuselage's half width at <paramref name="t"/> (0 at the tail, 1 at the nose): the smooth profile, stepped by armour section.</summary>
        private float HalfWidth(float t)
        {
            float pos = t * (Profile.Length - 1);
            int i = Math.Min((int)pos, Profile.Length - 2);
            float f = (1f - MathF.Cos((pos - i) * MathF.PI)) * 0.5f;
            float profile = MathHelper.Lerp(Profile[i], Profile[i + 1], f);

            float s = t * Sections;
            int k = Math.Min((int)s, Sections - 1);
            float frac = s - k;
            float scale = k > 0 && frac < 0.08f ? MathHelper.Lerp(SectionScale[k - 1], SectionScale[k], frac / 0.08f) : SectionScale[k];

            return Body * profile * scale;
        }

        private static bool InWing(Wing w, float x, float ay)
        {
            if (ay < w.RootY || ay > w.Span) return false;
            var p = new Vector2(x, ay);
            float s1 = Cross(w.FrontRoot, w.BackRoot, p), s2 = Cross(w.BackRoot, w.BackTip, p),
                  s3 = Cross(w.BackTip, w.FrontTip, p), s4 = Cross(w.FrontTip, w.FrontRoot, p);
            return (s1 >= 0 && s2 >= 0 && s3 >= 0 && s4 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0 && s4 <= 0);
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
    }

    private static Design Build(ShipAppearance a, Random rng)
    {
        var d = new Design { Body = a.BodyWidth };

        float noseEnd = (1f - a.NoseSharpness) * 0.5f + 0.03f;
        float[] baseProfile = { 0.75f, 0.95f, 1f, 0.95f, 0.8f, 0.55f, noseEnd };
        for (int i = 0; i < baseProfile.Length; i++)
            d.Profile[i] = baseProfile[i] * (i is > 0 and < 5 ? 1f + ((float)rng.NextDouble() - 0.5f) * 0.2f : 1f);
        for (int i = 0; i < Sections; i++)
            d.SectionScale[i] = 0.88f + (float)rng.NextDouble() * 0.2f;

        for (int pair = 0; pair < a.WingPairs; pair++)
        {
            float shrink = pair == 0 ? 1f : 0.6f;
            float x0 = pair == 0 ? -0.1f : -0.55f;
            float chord = a.WingChord * shrink, span = a.WingSpan * shrink, sweep = a.WingSweep * shrink;
            float rootY = a.BodyWidth * 0.5f;
            float j1 = ((float)rng.NextDouble() - 0.5f) * 0.08f, j2 = ((float)rng.NextDouble() - 0.5f) * 0.08f;

            d.Wings.Add(new Wing
            {
                FrontRoot = new Vector2(x0 + chord * 0.5f, rootY),
                BackRoot = new Vector2(x0 - chord * 0.5f, rootY),
                BackTip = new Vector2(x0 - chord * 0.5f - sweep + j1, span),
                FrontTip = new Vector2(x0 + chord * 0.18f - sweep + j2, span),
                RootY = rootY,
                Span = span,
            });
        }

        int engines = Math.Clamp(a.EngineCount, 1, 4);
        for (int i = 0; i < engines; i++)
            d.EngineY.Add(engines == 1 ? 0f : (i - (engines - 1) * 0.5f) * 2.2f * a.BodyWidth);

        d.ConeRadiusY = a.BodyWidth * 0.5f;
        d.SeamOffsetX = (float)rng.NextDouble();
        d.SeamOffsetY = (float)rng.NextDouble();
        return d;
    }

    /// <summary>Makes the ship for <paramref name="appearance"/>.</summary>
    public static ShipHull Generate(ShipAppearance appearance)
    {
        var rng = new Random(appearance.HullSeed);
        var design = Build(appearance, rng);

        const int n = ShipHull.Size;
        var height = new float[n * n];
        var cover = new float[n * n];
        var material = new Mat[n * n];

        for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                int covered = 0;
                float sum = 0f, top = 0f;
                Mat mat = Mat.None;
                for (int sy = 0; sy < Oversample; sy++)
                    for (int sx = 0; sx < Oversample; sx++)
                    {
                        float x = ((px * Oversample + sx + 0.5f) / Grid) * 2f - 1f;
                        float y = ((py * Oversample + sy + 0.5f) / Grid) * 2f - 1f;
                        Cell c = design.At(x, y);
                        if (c.H <= 0f) continue;
                        covered++;
                        sum += c.H;
                        if (c.H > top) { top = c.H; mat = c.M; }
                    }
                int i = py * n + px;
                cover[i] = covered / (float)(Oversample * Oversample);
                height[i] = covered > 0 ? sum / covered : 0f;
                material[i] = mat;
            }

        var pixels = new uint[n * n];
        var light = Vector3.Normalize(new Vector3(-0.45f, -0.55f, 0.7f));
        float H(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : height[y * n + x];
        float Cov(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : cover[y * n + x];

        for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                int i = py * n + px;
                if (cover[i] <= 0f) continue;

                var normal = Vector3.Normalize(new Vector3(-(H(px + 1, py) - H(px - 1, py)) * 24f, -(H(px, py + 1) - H(px, py - 1)) * 24f, 1f));
                float diffuse = MathF.Max(0f, Vector3.Dot(normal, light));
                float shade = 0.4f + 0.85f * diffuse;

                Vector3 color = BaseColor(appearance, material[i]);
                float nx = (px + 0.5f) / n * 2f - 1f, ny = (py + 0.5f) / n * 2f - 1f;

                if (material[i] is Mat.Hull or Mat.Accent)
                {
                    shade *= PanelShade(design, appearance.HullSeed, nx, ny);
                }
                else if (material[i] == Mat.Glass)
                {
                    color += new Vector3(1f, 1f, 1f) * (0.5f * MathF.Pow(diffuse, 12f));
                }
                else if (material[i] == Mat.Engine && nx < -0.9f)
                {
                    color = Vector3.Lerp(color, new Vector3(0.8f, 0.35f, 0.12f), 0.55f);
                }

                color *= shade;

                // A darker rim where the hull meets space.
                if (cover[i] >= 1f && (Cov(px - 1, py) < 0.75f || Cov(px + 1, py) < 0.75f || Cov(px, py - 1) < 0.75f || Cov(px, py + 1) < 0.75f))
                    color *= 0.8f;

                pixels[i] = Pack(color, cover[i]);
            }

        if (design.Wings.Count > 0)
        {
            Wing wing = design.Wings[0];
            PaintLight(pixels, wing.FrontTip.X, -wing.FrontTip.Y, new Vector3(1f, 0.2f, 0.2f));
            PaintLight(pixels, wing.FrontTip.X, wing.FrontTip.Y, new Vector3(0.2f, 1f, 0.4f));
        }

        var engines = new Vector2[design.EngineY.Count];
        for (int i = 0; i < engines.Length; i++)
            engines[i] = new Vector2(NozzleX, design.EngineY[i]);

        return new ShipHull { Pixels = pixels, Engines = engines };
    }

    private static Vector3 BaseColor(ShipAppearance a, Mat m) => m switch
    {
        Mat.Accent => new Vector3(a.AccentColor.R, a.AccentColor.G, a.AccentColor.B) / 255f,
        Mat.Glass => new Vector3(70, 140, 200) / 255f,
        Mat.Engine => new Vector3(55, 55, 65) / 255f,
        _ => new Vector3(a.HullColor.R, a.HullColor.G, a.HullColor.B) / 255f,
    };

    /// <summary>Panel seams, a slightly different tone for each plate and fine grain, so the hull is not one flat colour.</summary>
    private static float PanelShade(Design d, int seed, float x, float y)
    {
        float gx = x * 14f + d.SeamOffsetX, gy = MathF.Abs(y) * 9f + d.SeamOffsetY;
        float shade = 1f;
        if (gx - MathF.Floor(gx) < 0.045f) shade *= 0.8f;
        if (gy - MathF.Floor(gy) < 0.05f) shade *= 0.85f;

        shade *= 0.94f + 0.12f * Hash01(seed, (int)MathF.Floor(gx), (int)MathF.Floor(gy));
        shade *= 0.97f + 0.06f * Hash01(seed + 1, (int)(x * 400f), (int)(MathF.Abs(y) * 400f));
        return shade;
    }

    private static float Hash01(int seed, int a, int b)
    {
        unchecked
        {
            uint h = (uint)seed * 374761393u + (uint)a * 668265263u + (uint)b * 2147483647u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    private static void PaintLight(uint[] pixels, float x, float y, Vector3 color)
    {
        const int n = ShipHull.Size;
        float cx = (x + 1f) * 0.5f * n, cy = (y + 1f) * 0.5f * n;
        for (int py = (int)cy - 4; py <= (int)cy + 4; py++)
            for (int px = (int)cx - 4; px <= (int)cx + 4; px++)
            {
                if (px < 0 || py < 0 || px >= n || py >= n) continue;
                float d = MathF.Sqrt((px + 0.5f - cx) * (px + 0.5f - cx) + (py + 0.5f - cy) * (py + 0.5f - cy));
                if (d > 3.5f) continue;
                float a = MathHelper.Clamp(1.2f - d / 3.5f, 0f, 1f);
                if (pixels[py * n + px] != 0u)
                    pixels[py * n + px] = Pack(Vector3.Lerp(Unpack(pixels[py * n + px]), color, a), 1f);
            }
    }

    private static uint Pack(Vector3 color, float alpha)
    {
        byte Channel(float v) => (byte)(Math.Clamp(v, 0f, 1f) * alpha * 255f + 0.5f);
        return Rgba.Pack(Channel(color.X), Channel(color.Y), Channel(color.Z), (byte)(alpha * 255f + 0.5f));
    }

    /// <summary>The colour of a fully opaque packed pixel, as 0 to 1.</summary>
    private static Vector3 Unpack(uint p) => new(((p) & 0xFF) / 255f, ((p >> 8) & 0xFF) / 255f, ((p >> 16) & 0xFF) / 255f);

    /// <summary>
    /// The engine flame, <see cref="FlameWidth"/> by <see cref="FlameHeight"/>: starts at the top centre and runs down, white-hot at the nozzle through orange
    /// to red at the tip. Premultiplied, for additive drawing.
    /// </summary>
    public static uint[] Flame()
    {
        var pixels = new uint[FlameWidth * FlameHeight];
        for (int y = 0; y < FlameHeight; y++)
        {
            float t = y / (float)(FlameHeight - 1);
            float halfWidth = (FlameWidth * 0.5f - 1f) * MathF.Pow(1f - t, 0.7f) * (0.35f + 0.65f * MathF.Min(1f, t * 6f));
            for (int x = 0; x < FlameWidth; x++)
            {
                if (halfWidth <= 0f) continue;
                float d = MathF.Abs(x + 0.5f - FlameWidth * 0.5f) / halfWidth;
                if (d >= 1f) continue;

                float intensity = (1f - d * d) * MathF.Pow(1f - t, 0.5f);
                Vector3 color = intensity > 0.6f
                    ? Vector3.Lerp(new Vector3(1f, 0.6f, 0.2f), new Vector3(1f, 0.96f, 0.8f), (intensity - 0.6f) / 0.4f)
                    : Vector3.Lerp(new Vector3(0.8f, 0.2f, 0.08f), new Vector3(1f, 0.6f, 0.2f), intensity / 0.6f);
                pixels[y * FlameWidth + x] = Pack(color, intensity);
            }
        }
        return pixels;
    }
}
