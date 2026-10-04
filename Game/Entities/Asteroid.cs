using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe;
using Strange_Universe.Game.Components;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

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

    /// <summary>Radius the asteroid had at full health, used to scale damage shrinkage.</summary>
    public float BaseRadius { get; private set; }

    public int MaxHullStrength => (int)Math.Round(BaseRadius * 2f);
    public int CurrentHullStrength { get; set; }

    /// <summary>
    /// Fragments thrown off by recent impacts. Owned by the asteroid so they
    /// inherit its texture and are cleaned up with it.
    /// </summary>
    [JsonIgnore] public List<AsteroidShard> Shards { get; } = new();

    /// <summary>Smallest radius an asteroid can be chipped down to before it is destroyed.</summary>
    private const float MinRadius = 3f;

    /// <summary>Portion of the radius that erodes as the asteroid takes damage.</summary>
    private const float ErodeFraction = 0.55f;

    private static readonly Random _impactRng = new();

    /// <summary>True once the asteroid's hull has been fully depleted.</summary>
    [JsonIgnore] public bool IsDestroyed => CurrentHullStrength <= 0;

    /// <summary>
    /// True once the asteroid is destroyed and its debris has finished playing.
    /// The owning system removes it at this point.
    /// </summary>
    [JsonIgnore] public bool IsGone => IsDestroyed && Shards.Count == 0;

    /// <summary>
    /// Applies an incoming projectile to the asteroid's hull, spalling shards from
    /// the impact point and eroding the rock in proportion to the damage dealt.
    /// Destruction is handled by the owning system once <see cref="IsDestroyed"/> is observed.
    /// </summary>
    public void ApplyDamage(Strange_Universe.Game.Entities.Projectile projectile)
    {
        if (projectile == null || IsDestroyed)
            return;

        int damage = projectile.Damage;

        CurrentHullStrength = Math.Max(0, CurrentHullStrength - damage);

        float severity = MaxHullStrength > 0
            ? MathHelper.Clamp(damage / (float)MaxHullStrength, 0.05f, 1f)
            : 0.25f;

        // Erode the rock, then shift what remains away from the struck face.
        float shrink = UpdateRadiusFromHull();
        ApplyErosionOffset(projectile.Position, shrink, severity);

        // Shards spall back toward where the shot came from.
        Vector2 inbound = projectile.Velocity.LengthSquared() > 0.0001f
            ? Vector2.Normalize(projectile.Velocity)
            : Vector2.UnitX;

        SpawnShards(projectile.Position, inbound, damage);

        // A killing blow blows the remaining rock apart in all directions.
        if (IsDestroyed)
            SpawnBreakupShards();
    }

    /// <summary>
    /// Scatters the remains of the asteroid when it is destroyed, using the radius
    /// it had just before shattering.
    /// </summary>
    private void SpawnBreakupShards()
    {
        int count = Math.Clamp(20 + (int)(Radius * 1.8f), 20, 64);

        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_impactRng.NextDouble() * MathHelper.TwoPi);
            float speed = MathHelper.Lerp(25f, 190f, (float)_impactRng.NextDouble());
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            float life = MathHelper.Lerp(0.6f, 1.8f, (float)_impactRng.NextDouble());

            Shards.Add(new AsteroidShard
            {
                Position        = Position + direction * Radius * (float)_impactRng.NextDouble() * 0.7f,
                Velocity        = Velocity + direction * speed,
                Rotation        = (float)(_impactRng.NextDouble() * MathHelper.TwoPi),
                AngularVelocity = MathHelper.Lerp(-7f, 7f, (float)_impactRng.NextDouble()),
                Radius          = Math.Max(1.5f, Radius * MathHelper.Lerp(0.08f, 0.34f, (float)_impactRng.NextDouble())),
                TextureId       = TextureId,
                MaxLife         = life,
                Life            = life,
            });
        }
    }

    /// <summary>Applies raw damage to the asteroid's hull and erodes its radius.</summary>
    public void ApplyDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed)
            return;

        CurrentHullStrength = Math.Max(0, CurrentHullStrength - damage);
        UpdateRadiusFromHull();
    }

    /// <summary>
    /// Shrinks the asteroid toward <see cref="MinRadius"/> as its hull depletes, so
    /// heavier damage visibly carves more material away.
    /// </summary>
    /// <returns>How much the radius shrank as a result of this call.</returns>
    private float UpdateRadiusFromHull()
    {
        if (MaxHullStrength <= 0)
            return 0f;

        float healthFraction = MathHelper.Clamp(
            CurrentHullStrength / (float)MaxHullStrength, 0f, 1f);

        float target   = BaseRadius * (1f - ErodeFraction * (1f - healthFraction));
        float previous = Radius;

        Radius = Math.Max(MinRadius, target);

        return Math.Max(0f, previous - Radius);
    }

    /// <summary>
    /// Material is carved off the struck face, so the remaining mass settles away
    /// from the impact rather than shrinking around a fixed centre. The body also
    /// takes a small kick and tumble from the hit.
    /// </summary>
    private void ApplyErosionOffset(Vector2 impactPoint, float shrinkAmount, float severity)
    {
        if (shrinkAmount <= 0f)
            return;

        Vector2 awayFromImpact = Position - impactPoint;

        awayFromImpact = awayFromImpact.LengthSquared() > 0.0001f
            ? Vector2.Normalize(awayFromImpact)
            : new Vector2(
                (float)(_impactRng.NextDouble() * 2.0 - 1.0),
                (float)(_impactRng.NextDouble() * 2.0 - 1.0));

        // Recentre on what is left of the rock, with a little jitter so repeated
        // hits from the same angle don't track a perfectly straight line.
        float jitter = (float)(_impactRng.NextDouble() - 0.5) * 0.4f;
        var   offset = new Vector2(
            awayFromImpact.X - awayFromImpact.Y * jitter,
            awayFromImpact.Y + awayFromImpact.X * jitter);

        Position += offset * shrinkAmount;

        // Momentum transferred by the shot that knocked the fragment loose.
        Velocity        += offset * shrinkAmount * MathHelper.Lerp(1.5f, 5f, severity);
        AngularVelocity += MathHelper.Lerp(-1.5f, 1.5f, (float)_impactRng.NextDouble()) * severity;
    }

    /// <summary>
    /// Throws fragments off the impact point. Shard count and spread scale with
    /// the damage dealt relative to the asteroid's total hull.
    /// </summary>
    private void SpawnShards(Vector2 impactPoint, Vector2 inboundDirection, int damage)
    {
        if (damage <= 0) return;

        float severity = MaxHullStrength > 0
            ? MathHelper.Clamp(damage / (float)MaxHullStrength, 0.05f, 1f)
            : 0.25f;

        int count = Math.Clamp(10 + (int)(severity * 38f), 10, 48);

        // Spall cone points back along the projectile's path.
        float baseAngle = MathF.Atan2(-inboundDirection.Y, -inboundDirection.X);

        for (int i = 0; i < count; i++)
        {
            // Wide cone, biased toward the centre so the spray still reads directionally.
            float bias   = (float)(_impactRng.NextDouble() + _impactRng.NextDouble()) * 0.5f;
            float spread = (bias - 0.5f) * MathHelper.Pi * 1.1f;
            float angle  = baseAngle + spread;

            float speed = MathHelper.Lerp(20f, 210f, (float)_impactRng.NextDouble())
                        * (0.5f + severity);

            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float life = MathHelper.Lerp(0.3f, 1.1f, (float)_impactRng.NextDouble());

            // Scatter origins slightly so fragments don't all erupt from one pixel.
            Vector2 origin = impactPoint + new Vector2(
                (float)(_impactRng.NextDouble() - 0.5) * Radius * 0.35f,
                (float)(_impactRng.NextDouble() - 0.5) * Radius * 0.35f);

            Shards.Add(new AsteroidShard
            {
                Position        = origin,
                Velocity        = Velocity + direction * speed,
                Rotation        = (float)(_impactRng.NextDouble() * MathHelper.TwoPi),
                AngularVelocity = MathHelper.Lerp(-9f, 9f, (float)_impactRng.NextDouble()),
                Radius          = Math.Max(1f, Radius * MathHelper.Lerp(0.05f, 0.24f, (float)_impactRng.NextDouble())),
                TextureId       = TextureId,
                MaxLife         = life,
                Life            = life,
            });
        }
    }

    public Asteroid(float angle, float orbit, float radius, string textureId, Random asteroidsRng)
    {
        Radius = radius;
        BaseRadius = radius;
        CurrentHullStrength = MaxHullStrength;
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

        for (int i = Shards.Count - 1; i >= 0; i--)
        {
            Shards[i].Update(deltaTime);
            if (Shards[i].IsExpired)
                Shards.RemoveAt(i);
        }
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
