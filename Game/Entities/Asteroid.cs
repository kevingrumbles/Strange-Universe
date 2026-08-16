using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe;
using StrangeUniverse.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace StrangeUniverse.Game.Entities;

public class Asteroid
{
    /// <summary>Current ship position in world space.</summary>
    [JsonIgnore]
    public Vector2 Position
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }

    /// <summary>Current ship heading (rotation in radians).</summary>
    [JsonIgnore]
    public float Rotation
    {
        get => Transform.Rotation;
        set => Transform.Rotation = value;
    }

    [JsonIgnore]
    public float Scale
    {
        get => Transform.Scale;
        set => Transform.Scale = value;
    }

    /// <summary>Forward direction vector based on current rotation.</summary>
    [JsonIgnore]
    public Vector2 Forward
    {
        get => Transform.Forward;
    }
    /// <summary>Current velocity vector.</summary>
    public Vector2 Velocity
    {
        get => Physics.Velocity;
        set => Physics.Velocity = value;
    }
    public float AngularVelocity
    {
        get => Physics.AngularVelocity;
        set => Physics.AngularVelocity = value;
    }
    private Transform   Transform { get; } = new();
    private PhysicsBody Physics   { get; } = new();
    public float       Radius    { get; set; }
    public string      TextureId { get; set; } = string.Empty;
    private const int  Size = 128;

    public Asteroid(float angle, float orbit, float radius, string textureId, Random asteroidsRng)
    {
        Radius = radius;
        TextureId = textureId;
        Position = new Vector2((float)Math.Cos(angle) * orbit, (float)Math.Sin(angle) * orbit);
        Rotation = (float)(asteroidsRng.NextDouble() * MathHelper.TwoPi);

        float speed = MathHelper.Lerp(8f, 30f, (float)asteroidsRng.NextDouble());
        float perpAngle = angle + MathHelper.PiOver2;
        Velocity = new Vector2(
            (float)Math.Cos(perpAngle) * speed,
            (float)Math.Sin(perpAngle) * speed);
        AngularVelocity = MathHelper.Lerp(-0.4f, 0.4f, (float)asteroidsRng.NextDouble());
    }
    public void Update(float deltaTime)
    {
        Physics.Integrate(Transform, deltaTime);
    }

    /// <summary>
    /// Generates an irregular rocky asteroid texture with bump-mapped lighting,
    /// layered surface detail, cracks, and a natural rock color palette.
    /// </summary>
    public static Texture2D Generate(GraphicsDevice gd, int seed)
    {
        var rng = new Random(seed);
        var colors = new Color[Size * Size];
        float cx = Size * 0.5f;
        float cy = Size * 0.5f;
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

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float angle = (float)Math.Atan2(dy, dx);

                // Low-freq control-point radius at this angle
                float asteroidR = StaticHelpers.AsteroidInterpolatedRadius(angle, sampleAngles, sampleRadii);

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
                // Blend from dark gray-brown → medium warm gray using surface mix
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

                colors[py * Size + px] = new Color(
                    (byte)Math.Clamp(cr, 0f, 255f),
                    (byte)Math.Clamp(cg, 0f, 255f),
                    (byte)Math.Clamp(cb, 0f, 255f),
                    (byte)(alpha * 255f));
            }
        }

        var tex = new Texture2D(gd, Size, Size);
        tex.SetData(colors);
        return tex;
    }
}
