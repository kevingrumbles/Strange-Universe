using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Helpers;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// A generated ship: its hull pictures (premultiplied RGBA, pointing right) and where its engines are.
/// <para>
/// The hull is lit from <see cref="LightAngles"/> directions around it, side by side in one strip (<see cref="AtlasWidth"/> wide, <see cref="Size"/> tall),
/// so the renderer can light a ship from the nearest star by blending the two pictures either side of the star's direction.
/// Picture <c>i</c> is lit from the angle <c>i / LightAngles * 2π</c> in the ship's own frame (0 = from the nose, π/2 = from below the picture).
/// </para>
/// Positions are in a picture's own space: -1 to 1 on each axis from the centre, x toward the nose, y down.
/// </summary>
public sealed class ShipHull
{
    public const int Size = 256;
    public const int LightAngles = 16;
    public const int AtlasWidth = Size * LightAngles;

    /// <summary>The strip of lit pictures, row by row (<see cref="AtlasWidth"/> pixels per row).</summary>
    public uint[] Pixels { get; init; }

    /// <summary>Where each engine's flame starts (the back of the nozzle).</summary>
    public Vector2[] Engines { get; init; }

    /// <summary>How far out the shield bubble reaches, as a fraction of the picture's half-width, at <see cref="ShieldAngles"/> angles around the ship.</summary>
    public float[] ShieldRadius { get; init; }

    public const int ShieldAngles = 72;

    /// <summary>The shield bubble's radius toward <paramref name="angle"/> (radians in the ship's own frame, 0 = the nose), interpolated between samples.</summary>
    public float ShieldRadiusAt(float angle)
    {
        float position = (angle / MathF.Tau - MathF.Floor(angle / MathF.Tau)) * ShieldAngles;
        int first = (int)position % ShieldAngles;
        return MathHelper.Lerp(ShieldRadius[first], ShieldRadius[(first + 1) % ShieldAngles], position - MathF.Floor(position));
    }

    /// <summary>True when the hull covers <paramref name="point"/> (picture space, -1 to 1).</summary>
    public bool Covers(Vector2 point)
    {
        int x = (int)((point.X + 1f) * 0.5f * Size), y = (int)((point.Y + 1f) * 0.5f * Size);
        return x >= 0 && y >= 0 && x < Size && y < Size && (Pixels[y * AtlasWidth + x] >> 24) > 127;
    }

    /// <summary>The pixels of one lit picture on its own.</summary>
    public uint[] Picture(int light)
    {
        var picture = new uint[Size * Size];
        for (int y = 0; y < Size; y++)
            Array.Copy(Pixels, y * AtlasWidth + light * Size, picture, y * Size, Size);
        return picture;
    }
}

/// <summary>
/// Builds a ship's pictures from its <see cref="ShipAppearance"/>: a fuselage whose width changes along its length in separate armour sections, wings with
/// ribs, stripes and gun mounts, engine pods, a cockpit, raised plates, vents and cabin windows, all mirrored about the ship's axis. The shape is a height
/// map, which is lit from many directions with shading, shine, crevice darkening and shadows cast by the raised parts onto the hull.
/// The same appearance always makes the same ship. Pure pixel work with no graphics library, so it is the same on any machine and runs without a window.
/// </summary>
public static class ShipHullGenerator
{
    private const int Oversample = 2;
    private const int Grid = ShipHull.Size * Oversample;

    private const float Tail = -0.9f;
    private const float Nose = 0.95f;
    private const float NozzleX = -0.95f;
    private const int Sections = 4;

    /// <summary>How tall the hull is in pixels for a height of 1, for the shadows the raised parts cast.</summary>
    private const float HeightPixels = 40f;

    private const float LightElevation = 0.45f;
    private const int ShadowSteps = 32;

    public const int FlameWidth = 32;
    public const int FlameHeight = 64;
    public const int ScorchSize = 32;

    private enum Mat : byte { None, Hull, Accent, Glass, Engine, Vent, Window, Eye }

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

    private struct Rect
    {
        public float X, Y, HalfX, HalfY;
        public bool Contains(float x, float ay) => MathF.Abs(x - X) < HalfX && MathF.Abs(ay - Y) < HalfY;
    }

    private sealed class Design
    {
        public float Body, Blockiness, PodHalfWidth;
        public readonly float[] Profile = new float[7];
        public readonly float[] SectionScale = new float[Sections];
        public readonly List<Wing> Wings = new();
        public readonly List<float> EngineY = new();
        public readonly List<Rect> Plates = new();
        public readonly List<Rect> Vents = new();
        public readonly List<Vector2> Windows = new();
        public float ConeX = 0.5f, ConeRadiusX = 0.2f, ConeRadiusY;
        public float HammerFront, HammerHalfY, HammerSweep; // the hammerhead crossbar; none when HammerHalfY is 0
        public float BodyEnd = Nose; // the fuselage stops here
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
            if (t is >= 0f and <= 1f && x <= BodyEnd)
            {
                float w = HalfWidth(t);
                if (ay < w)
                {
                    float u = ay / w;
                    // A round cross-section for a smooth hull, squarer and flatter on top as it gets blockier.
                    float p = MathHelper.Lerp(2f, 8f, Blockiness);
                    Put(0.45f + 0.4f * MathF.Pow(1f - MathF.Pow(u, p), 1f / p), u < 0.1f && t > 0.3f ? Mat.Accent : Mat.Hull);
                    AddFuselageDetail(ref best, x, ay);
                }
            }

            foreach (var wing in Wings)
                if (InWing(wing, x, ay))
                {
                    float along = (ay - wing.RootY) / (wing.Span - wing.RootY);
                    float h = 0.28f + 0.12f * (1f - along);

                    if (Frac(along * 7f) < 0.12f) h += 0.025f; // ribs running out to the tip
                    if (EdgeDistance(wing, x, ay) < 0.022f) h += 0.04f; // raised leading edge

                    bool stripe = along is > 0.1f and < 0.5f && Frac(x * 8f + ay * 6f) < 0.35f;
                    Put(h, along > 0.75f || stripe ? Mat.Accent : Mat.Hull);
                }

            if (Wings.Count > 0)
                AddGuns(ref best, x, ay);

            foreach (float py in EngineY)
            {
                float d = MathF.Abs(y - py) / PodHalfWidth;
                if (d < 1f && x is >= NozzleX and <= -0.5f)
                {
                    float h = 0.5f + 0.15f * MathF.Sqrt(1f - d * d);
                    if (Frac(x * 18f) < 0.25f) h += 0.03f; // rings along the pod
                    if (x < -0.9f && d < 0.5f) h -= 0.12f; // the bell of the nozzle
                    Put(h, x < -0.88f ? Mat.Engine : Mat.Hull);
                }

                // A strut ties a pod that sits outboard of the fuselage back to it.
                if (MathF.Abs(py) > HalfWidth((-0.57f - Tail) / (Nose - Tail)) * 0.8f && ay < MathF.Abs(py) && x is >= -0.62f and <= -0.52f)
                    Put(0.35f, Mat.Hull);
            }

            if (HammerHalfY > 0f)
                AddHammer(ref best, x, ay);

            if (best.H > 0f)
            {
                float dx = (x - ConeX) / ConeRadiusX, dy = y / ConeRadiusY;
                float d2 = dx * dx + dy * dy;
                if (d2 < 1f)
                    Put(0.95f - 0.2f * d2, Mat.Glass);
            }
            return best;
        }

        private float HammerEdge(float ay) => HammerFront - HammerSweep * (ay / HammerHalfY) * (ay / HammerHalfY);

        /// <summary>
        /// The hammerhead: a crossbar thick in the middle and thin at the tips, its front edge bowed back toward the tips, with red tips and an eye at each tip.
        /// </summary>
        private void AddHammer(ref Cell cell, float x, float ay)
        {
            float u = ay / HammerHalfY;
            float front = HammerEdge(ay);
            if (u < 1f)
            {
                float depth = 0.2f + 0.26f * (1f - u);
                if (x < front && x > front - depth)
                {
                    float h = 0.6f + 0.22f * (1f - u);
                    if (front - x < 0.025f) h += 0.05f;                     // a raised lip along the front edge
                    if (ay < 0.05f || (u > 0.3f && Frac(u * 5f) < 0.06f)) h += 0.04f; // spine and ribs
                    PutHigher(ref cell, h, u > 0.8f || front - x < 0.025f ? Mat.Accent : Mat.Hull);
                }
            }

            // An eye at each tip.
            float eyeY = HammerHalfY - 0.11f, eyeX = HammerEdge(eyeY) - 0.07f;
            float dx = x - eyeX, dy = ay - eyeY;
            if (dx * dx + dy * dy < 0.03f * 0.03f)
                PutHigher(ref cell, 0.9f, Mat.Eye);
        }

        private static void PutHigher(ref Cell cell, float h, Mat m)
        {
            if (h > cell.H) { cell.H = h; cell.M = m; }
        }

        /// <summary>Raised plates, recessed vent grills and cabin windows on the fuselage.</summary>
        private void AddFuselageDetail(ref Cell cell, float x, float ay)
        {
            foreach (var plate in Plates)
                if (plate.Contains(x, ay))
                {
                    cell.H += 0.07f;
                    break;
                }

            foreach (var vent in Vents)
                if (vent.Contains(x, ay) && Frac(ay / 0.014f) < 0.5f)
                {
                    cell.H -= 0.05f;
                    cell.M = Mat.Vent;
                    return;
                }

            foreach (var window in Windows)
            {
                float dx = x - window.X, dy = ay - window.Y;
                if (dx * dx + dy * dy < 0.016f * 0.016f)
                {
                    cell.M = Mat.Window;
                    return;
                }
            }
        }

        /// <summary>Gun barrels on the first wing pair, sticking out past the leading edge.</summary>
        private void AddGuns(ref Cell cell, float x, float ay)
        {
            Wing w = Wings[0];
            float gunY = w.RootY + 0.55f * (w.Span - w.RootY);
            float along = (gunY - w.RootY) / (w.Span - w.RootY);
            float leading = MathHelper.Lerp(w.FrontRoot.X, w.FrontTip.X, along);

            bool barrel = x > leading && x < leading + 0.16f && MathF.Abs(ay - gunY) < 0.012f;
            bool mount = x > leading - 0.05f && x < leading + 0.02f && MathF.Abs(ay - gunY) < 0.03f;
            if ((barrel || mount) && 0.4f > cell.H)
            {
                cell.H = 0.4f;
                cell.M = Mat.Engine;
            }
        }

        /// <summary>The fuselage's half width at <paramref name="t"/> (0 at the tail, 1 at the nose): the smooth profile, stepped by armour section.</summary>
        public float HalfWidth(float t)
        {
            float pos = t * (Profile.Length - 1);
            int i = Math.Min((int)pos, Profile.Length - 2);
            float f = (1f - MathF.Cos((pos - i) * MathF.PI)) * 0.5f;
            float profile = MathHelper.Lerp(Profile[i], Profile[i + 1], f);

            float s = t * Sections;
            int k = Math.Min((int)s, Sections - 1);
            float frac = s - k;
            float scale = k > 0 && frac < 0.04f ? MathHelper.Lerp(SectionScale[k - 1], SectionScale[k], frac / 0.04f) : SectionScale[k];

            return Body * profile * scale;
        }

        private static float Frac(float v) => v - MathF.Floor(v);

        private static bool InWing(Wing w, float x, float ay)
        {
            if (ay < w.RootY || ay > w.Span) return false;
            var p = new Vector2(x, ay);
            float s1 = Cross(w.FrontRoot, w.BackRoot, p), s2 = Cross(w.BackRoot, w.BackTip, p),
                  s3 = Cross(w.BackTip, w.FrontTip, p), s4 = Cross(w.FrontTip, w.FrontRoot, p);
            return (s1 >= 0 && s2 >= 0 && s3 >= 0 && s4 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0 && s4 <= 0);
        }

        private static float EdgeDistance(Wing w, float x, float ay)
        {
            Vector2 edge = w.FrontRoot - w.FrontTip;
            return MathF.Abs(Cross(w.FrontTip, w.FrontRoot, new Vector2(x, ay))) / edge.Length();
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
    }

    private static Design Build(ShipAppearance a, Random rng)
    {
        float Next() => (float)rng.NextDouble();
        var d = new Design { Body = a.BodyWidth };

        d.Blockiness = a.Blockiness;
        d.PodHalfWidth = MathHelper.Clamp(a.BodyWidth * 0.4f, 0.04f, 0.075f);

        float noseEnd = (1f - a.NoseSharpness) * 0.5f + 0.03f;
        float[] smooth = { 0.75f, 0.95f, 1f, 0.95f, 0.8f, 0.55f, noseEnd };
        float[] boxy = { 1f, 1f, 1f, 1f, 1f, 0.95f, noseEnd };
        for (int i = 0; i < smooth.Length; i++)
            d.Profile[i] = MathHelper.Lerp(smooth[i], boxy[i], a.Blockiness) * (i is > 0 and < 5 ? 1f + (Next() - 0.5f) * 0.2f * (1f - a.Blockiness) : 1f);

        // Boxy ships are built from modules of clearly different widths; sleek ones are almost uniform.
        float variation = MathHelper.Lerp(0.08f, 0.4f, a.Blockiness);
        for (int i = 0; i < Sections; i++)
            d.SectionScale[i] = 1f - variation * 0.5f + Next() * variation;

        for (int pair = 0; pair < a.WingPairs; pair++)
        {
            float shrink = pair == 0 ? 1f : 0.6f;
            float x0 = pair == 0 ? -0.1f : -0.55f;
            float chord = a.WingChord * shrink, span = a.WingSpan * shrink, sweep = a.WingSweep * shrink;
            float rootY = a.BodyWidth * 0.5f;
            float j1 = (Next() - 0.5f) * 0.08f, j2 = (Next() - 0.5f) * 0.08f;

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
            d.EngineY.Add((i - (engines - 1) * 0.5f) * 2.1f * d.PodHalfWidth);

        d.ConeRadiusY = a.BodyWidth * 0.5f;
        if (a.HammerSpan > 0f)
        {
            d.HammerHalfY = a.HammerSpan;
            d.HammerFront = 0.72f;
            d.HammerSweep = 0.24f;
            d.BodyEnd = 0.5f;
            d.ConeX = 0.5f;
            d.ConeRadiusX = 0.1f;
        }
        d.SeamOffsetX = Next();
        d.SeamOffsetY = Next();

        // Detail is placed where the fuselage is wide enough to carry it.
        float WidthAt(float x) => d.HalfWidth((x - Tail) / (Nose - Tail));

        for (int i = 0; i < 14; i++)
        {
            float x = -0.7f + Next() * 1.3f;
            var plate = new Rect { X = x, HalfX = (0.03f + Next() * 0.06f) * (1f + a.Blockiness), HalfY = (0.02f + Next() * 0.04f) * (1f + a.Blockiness) };
            plate.Y = Next() * MathF.Max(0f, WidthAt(x) * 0.85f - plate.HalfY);
            d.Plates.Add(plate);
        }
        for (int i = 0; i < 3; i++)
        {
            float x = -0.6f + Next() * 0.8f;
            d.Vents.Add(new Rect { X = x, Y = i == 0 ? 0f : WidthAt(x) * 0.45f, HalfX = 0.05f + Next() * 0.03f, HalfY = 0.03f });
        }
        for (int i = 0; i < a.WindowCount; i++)
        {
            float x = -0.35f + (a.WindowCount == 1 ? 0.4f : 0.85f * i / (a.WindowCount - 1));
            d.Windows.Add(new Vector2(x, WidthAt(x) * 0.62f));
        }
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

        Parallel.For(0, n, py =>
        {
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
        });

        float H(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : height[y * n + x];
        float Cov(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? 0f : cover[y * n + x];

        // Everything that does not depend on where the light is: colour, surface direction and how tucked-away each pixel is.
        var albedo = new Vector3[n * n];
        var normals = new Vector3[n * n];
        var occlusion = new float[n * n];
        var emissive = new bool[n * n];

        for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                int i = py * n + px;
                if (cover[i] <= 0f) continue;

                normals[i] = Vector3.Normalize(new Vector3(-(H(px + 1, py) - H(px - 1, py)) * 44f, -(H(px, py + 1) - H(px, py - 1)) * 44f, 1f));

                // Crevices are darker: compare with the average height around.
                float around = 0f;
                for (int oy = -3; oy <= 3; oy++)
                    for (int ox = -3; ox <= 3; ox++)
                        around += H(px + ox, py + oy);
                occlusion[i] = MathHelper.Clamp(1f - (around / 49f - height[i]) * 1.6f, 0.55f, 1f);

                float nx = (px + 0.5f) / n * 2f - 1f, ny = (py + 0.5f) / n * 2f - 1f;
                Vector3 color = BaseColor(appearance, material[i]);

                if (material[i] is Mat.Hull or Mat.Accent)
                    color *= PanelShade(design, appearance.HullSeed, nx, ny);
                else if (material[i] == Mat.Engine && nx < -0.9f)
                    color = Vector3.Lerp(color, new Vector3(0.8f, 0.35f, 0.12f), 0.55f);
                else if (material[i] is Mat.Window or Mat.Eye)
                    emissive[i] = true;

                // A darker rim where the hull meets space.
                if (cover[i] >= 1f && (Cov(px - 1, py) < 0.75f || Cov(px + 1, py) < 0.75f || Cov(px, py - 1) < 0.75f || Cov(px, py + 1) < 0.75f))
                    color *= 0.8f;

                albedo[i] = color;
            }

        var pixels = new uint[ShipHull.AtlasWidth * n];
        Parallel.For(0, ShipHull.LightAngles, light =>
        {
            float angle = light / (float)ShipHull.LightAngles * MathF.Tau;
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            float horizontal = MathF.Sqrt(1f - LightElevation * LightElevation);
            var toLight = new Vector3(cos * horizontal, sin * horizontal, LightElevation);
            var halfway = Vector3.Normalize(toLight + Vector3.UnitZ);
            float rise = LightElevation / horizontal; // height gained per pixel travelled toward the light

            for (int py = 0; py < n; py++)
                for (int px = 0; px < n; px++)
                {
                    int i = py * n + px;
                    if (cover[i] <= 0f) continue;

                    Vector3 color;
                    if (emissive[i])
                    {
                        color = material[i] == Mat.Eye ? new Vector3(1f, 0.1f, 0.06f) : new Vector3(0.85f, 0.5f, 0.2f);
                    }
                    else
                    {
                        float diffuse = MathF.Max(0f, Vector3.Dot(normals[i], toLight));
                        float shadow = Shadow(height, n, px, py, cos, sin, rise);
                        float shine = material[i] switch
                        {
                            Mat.Glass => 0.9f,
                            Mat.Vent => 0f,
                            _ => 0.22f,
                        };
                        float spec = shine * MathF.Pow(MathF.Max(0f, Vector3.Dot(normals[i], halfway)), material[i] == Mat.Glass ? 40f : 18f);

                        float lit = (0.18f + 1.05f * diffuse) * occlusion[i] * shadow;
                        color = albedo[i] * lit + new Vector3(1f, 1f, 1f) * (spec * shadow);
                    }

                    pixels[py * ShipHull.AtlasWidth + light * n + px] = Pack(color, cover[i]);
                }
        });

        if (design.Wings.Count > 0)
        {
            Wing wing = design.Wings[0];
            for (int light = 0; light < ShipHull.LightAngles; light++)
            {
                PaintLight(pixels, light, wing.FrontTip.X, -wing.FrontTip.Y, new Vector3(1f, 0.2f, 0.2f));
                PaintLight(pixels, light, wing.FrontTip.X, wing.FrontTip.Y, new Vector3(0.2f, 1f, 0.4f));
            }
        }

        var engines = new Vector2[design.EngineY.Count];
        for (int i = 0; i < engines.Length; i++)
            engines[i] = new Vector2(NozzleX, design.EngineY[i]);

        return new ShipHull { Pixels = pixels, Engines = engines, ShieldRadius = ShieldBubble(cover, n) };
    }

    /// <summary>
    /// The shield bubble: how far the hull reaches from the centre at each angle, widened so that thin parts such as wing tips sit inside a smooth curve
    /// instead of poking through it, plus a margin.
    /// </summary>
    private static float[] ShieldBubble(float[] cover, int n)
    {
        const int A = ShipHull.ShieldAngles;
        var reach = new float[A];
        for (int i = 0; i < A; i++)
        {
            float cos = MathF.Cos(i / (float)A * MathF.Tau), sin = MathF.Sin(i / (float)A * MathF.Tau);
            for (float r = 1.2f; r > 0f; r -= 1f / n)
            {
                int x = (int)((cos * r + 1f) * 0.5f * n), y = (int)((sin * r + 1f) * 0.5f * n);
                if (x >= 0 && y >= 0 && x < n && y < n && cover[y * n + x] > 0.5f) { reach[i] = r; break; }
            }
        }

        var widened = new float[A];
        for (int i = 0; i < A; i++)
            for (int o = -4; o <= 4; o++)
                widened[i] = MathF.Max(widened[i], reach[(i + o + A) % A] - MathF.Abs(o) * 0.02f);

        for (int i = 0; i < A; i++)
        {
            float sum = 0f;
            for (int o = -2; o <= 2; o++) sum += widened[(i + o + A) % A];
            reach[i] = sum / 5f + 0.1f;
        }
        return reach;
    }

    /// <summary>A soft dark blotch, <see cref="ScorchSize"/> square: a hull scorch mark. Premultiplied black.</summary>
    public static uint[] Scorch()
    {
        var pixels = new uint[ScorchSize * ScorchSize];
        for (int y = 0; y < ScorchSize; y++)
            for (int x = 0; x < ScorchSize; x++)
            {
                float dx = (x + 0.5f) / ScorchSize * 2f - 1f, dy = (y + 0.5f) / ScorchSize * 2f - 1f;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float alpha = d >= 1f ? 0f : MathF.Pow(1f - d, 1.3f);
                pixels[y * ScorchSize + x] = Pack(Vector3.Zero, alpha);
            }
        return pixels;
    }

    /// <summary>1 where the light reaches the pixel, less where a taller part of the hull between it and the light throws a shadow on it.</summary>
    private static float Shadow(float[] height, int n, int px, int py, float dirX, float dirY, float rise)
    {
        float start = height[py * n + px] * HeightPixels;
        float shadow = 1f;
        for (int s = 1; s <= ShadowSteps; s++)
        {
            int x = (int)MathF.Round(px + dirX * s), y = (int)MathF.Round(py + dirY * s);
            if (x < 0 || y < 0 || x >= n || y >= n) break;

            float blocked = height[y * n + x] * HeightPixels - (start + s * rise);
            if (blocked > 0f)
                shadow = MathF.Min(shadow, 1f - 0.55f * MathF.Min(1f, blocked / 5f));
        }
        return shadow;
    }

    private static Vector3 BaseColor(ShipAppearance a, Mat m) => m switch
    {
        Mat.Accent => new Vector3(a.AccentColor.R, a.AccentColor.G, a.AccentColor.B) / 255f,
        Mat.Glass => new Vector3(70, 140, 200) / 255f,
        Mat.Engine => new Vector3(55, 55, 65) / 255f,
        Mat.Vent => new Vector3(60, 64, 72) / 255f,
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

    private static void PaintLight(uint[] pixels, int picture, float x, float y, Vector3 color)
    {
        const int n = ShipHull.Size;
        float cx = (x + 1f) * 0.5f * n, cy = (y + 1f) * 0.5f * n;
        for (int py = (int)cy - 4; py <= (int)cy + 4; py++)
            for (int px = (int)cx - 4; px <= (int)cx + 4; px++)
            {
                if (px < 0 || py < 0 || px >= n || py >= n) continue;
                float d = MathF.Sqrt((px + 0.5f - cx) * (px + 0.5f - cx) + (py + 0.5f - cy) * (py + 0.5f - cy));
                if (d > 3.5f) continue;

                int at = py * ShipHull.AtlasWidth + picture * n + px;
                if (pixels[at] == 0u) continue;
                float a = MathHelper.Clamp(1.2f - d / 3.5f, 0f, 1f);
                pixels[at] = Pack(Vector3.Lerp(Unpack(pixels[at]), color, a), 1f);
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
