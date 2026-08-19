using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Collections.Generic;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Renders all active projectiles using a layered, additive-blended pipeline.
/// Manages its own GPU textures and particle pool — nothing is allocated per frame.
///
/// Draw pipeline (back to front):
///   1. Particle trail
///   2. Tapered trail
///   3. Outer glow
///   4. Inner glow
///   5. Core bolt
///   6. Hot centre
/// </summary>
public sealed class ProjectileRenderer : IDisposable
{
    // -------------------------------------------------------------------------
    // Textures
    // -------------------------------------------------------------------------

    private readonly Texture2D _radialGlow;   // 128×128 radial soft glow (used for glow layers)
    private readonly Texture2D _boltBody;     // 8×128  elongated bolt body (used for core + trail)
    private readonly Texture2D _particle;     // 16×16  soft circular particle dot

    // -------------------------------------------------------------------------
    // Particle pool
    // -------------------------------------------------------------------------

    private const int MaxParticles = 2048;

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float   Life;          // remaining seconds
        public float   MaxLife;
        public float   Radius;
        public Color   Color;
        public bool    Active;
    }

    private readonly Particle[]  _particles     = new Particle[MaxParticles];
    private readonly Random      _rng           = new();
    private          int         _nextSlot      = 0;

    // -------------------------------------------------------------------------
    // Rendering state
    // -------------------------------------------------------------------------

    private SpriteBatch _spriteBatch => Launcher.RenderService.SpriteBatch;
    private          Matrix         _cameraMatrix;

    // -------------------------------------------------------------------------
    // Constructor — generate all textures once
    // -------------------------------------------------------------------------

    public ProjectileRenderer()
    {
        _radialGlow  = GenerateRadialGlow(128);
        _boltBody    = GenerateBoltBody(128, 8);
        _particle    = GenerateRadialGlow(16);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call once per frame before drawing projectiles to capture the current camera transform.
    /// </summary>
    public void SetCameraMatrix(Matrix matrix) => _cameraMatrix = matrix;

    /// <summary>
    /// Spawn new trail particles for a projectile and advance all live particles.
    /// Call during the Update pass, before Draw.
    /// </summary>
    public void UpdateParticles(IReadOnlyList<Projectile> projectiles, float deltaTime)
    {
        // Advance existing particles
        for (int i = 0; i < MaxParticles; i++)
        {
            ref Particle p = ref _particles[i];
            if (!p.Active) continue;
            p.Life -= deltaTime;
            if (p.Life <= 0f) { p.Active = false; continue; }
            p.Position += p.Velocity * deltaTime;
        }

        // Emit new particles from live projectiles
        foreach (var proj in projectiles)
        {
            int count = proj.Visual.ParticleCount;
            if (count <= 0) continue;

            // Emit ~count particles per second
            float emitRate   = count;
            int   toEmit     = (int)(emitRate * deltaTime) + (_rng.NextDouble() < (emitRate * deltaTime % 1.0) ? 1 : 0);

            float speed      = proj.Velocity.Length();
            Vector2 backward = speed > 0f ? -Vector2.Normalize(proj.Velocity) : Vector2.Zero;

            for (int e = 0; e < toEmit; e++)
            {
                ref Particle p = ref _particles[_nextSlot];
                _nextSlot = (_nextSlot + 1) % MaxParticles;

                float spread  = (float)(_rng.NextDouble() - 0.5) * 0.4f;
                float perpX   = -backward.Y * spread;
                float perpY   =  backward.X * spread;

                p.Active   = true;
                p.Position = proj.Position;
                p.Velocity = new Vector2(
                    backward.X * speed * 0.05f + perpX * speed * 0.03f,
                    backward.Y * speed * 0.05f + perpY * speed * 0.03f);
                p.MaxLife  = 0.12f + (float)_rng.NextDouble() * 0.1f;
                p.Life     = p.MaxLife;
                p.Radius   = 1.5f + (float)_rng.NextDouble() * 1.5f;
                p.Color    = proj.Visual.GlowColor;
            }
        }
    }

    /// <summary>
    /// Draws all projectiles using additive blending. Uses the shared RenderService, which
    /// transparently manages the SpriteBatch session — no manual Begin/End required here.
    /// </summary>
    public void DrawAll(IReadOnlyList<Projectile> projectiles)
    {
        if (projectiles.Count == 0) return;

        // ── Additive pass: particles + glow layers + core bolt + hot centre ───
        Launcher.RenderService.Begin(BatchMode.WorldAdditive, _cameraMatrix);

        DrawParticles();

        foreach (var p in projectiles)
            DrawGlowLayers(p);

        foreach (var p in projectiles)
            DrawCoreLayers(p);
    }

    // -------------------------------------------------------------------------
    // Draw helpers
    // -------------------------------------------------------------------------

    private void DrawParticles()
    {
        for (int i = 0; i < MaxParticles; i++)
        {
            ref Particle p = ref _particles[i];
            if (!p.Active) continue;

            float t     = p.Life / p.MaxLife;        // 1 → 0 as it fades
            float alpha = t * t;                      // quadratic fade
            float scale = p.Radius * 2f / _particle.Width;

            _spriteBatch.Draw(_particle, p.Position,
                null, p.Color * alpha, 0f,
                new Vector2(_particle.Width * 0.5f, _particle.Height * 0.5f),
                scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawGlowLayers(Projectile proj)
    {
        var     vis       = proj.Visual;
        float   rotation  = proj.Rotation;
        Vector2 pos       = proj.Position;

        // Pulse: smoothly oscillates ±PulseAmount around 1
        float pulse       = 1f + MathF.Sin(proj.Age * vis.PulseSpeed) * vis.PulseAmount;
        float glowR       = vis.GlowRadius * pulse;

        // -- Tapered trail (N quads shrinking and fading toward the tail) ------
        if (vis.TrailLength > 0)
        {
            const int TrailSteps = 8;
            Vector2 backward = new Vector2(-MathF.Cos(rotation), -MathF.Sin(rotation));

            for (int i = 0; i < TrailSteps; i++)
            {
                float t       = (i + 1f) / TrailSteps;        // 0 = near head, 1 = far tail
                float offset  = vis.TrailLength * t;
                float width   = vis.TrailWidth * (1f - t);     // tapers to zero
                float alpha   = vis.TrailAlpha * (1f - t) * vis.GlowIntensity;

                if (width < 0.5f || alpha < 0.01f) break;

                Vector2 segPos  = pos + backward * offset;
                float   scaleX  = offset / _boltBody.Width;    // stretch along length axis
                float   scaleY  = width  / _boltBody.Height;   // thickness axis

                _spriteBatch.Draw(_boltBody, segPos,
                    null, vis.GlowColor * alpha, rotation,
                    new Vector2(_boltBody.Width, _boltBody.Height * 0.5f),   // origin at head end
                    new Vector2(scaleX, scaleY),
                    SpriteEffects.None, 0f);
            }
        }

        // -- Outer glow halo ---------------------------------------------------
        {
            float   scale  = (glowR * 2f) / _radialGlow.Width;
            float   alpha  = vis.GlowIntensity * 0.55f;
            Vector2 origin = new Vector2(_radialGlow.Width * 0.5f, _radialGlow.Height * 0.5f);

            _spriteBatch.Draw(_radialGlow, pos, null,
                vis.GlowColor * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        // -- Inner glow (tighter, brighter) ------------------------------------
        {
            float   scale  = (glowR * 0.7f) / _radialGlow.Width;
            float   alpha  = vis.GlowIntensity * 0.7f * pulse;
            Vector2 origin = new Vector2(_radialGlow.Width * 0.5f, _radialGlow.Height * 0.5f);

            _spriteBatch.Draw(_radialGlow, pos, null,
                vis.GlowColor * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawCoreLayers(Projectile proj)
    {
        var     vis      = proj.Visual;
        float   rotation = proj.Rotation;
        Vector2 pos      = proj.Position;

        float pulse      = 1f + MathF.Sin(proj.Age * vis.PulseSpeed) * vis.PulseAmount;

        // -- Core bolt body ----------------------------------------------------
        {
            float scaleX = vis.CoreLength / (float)_boltBody.Width;
            float scaleY = vis.CoreWidth  / (float)_boltBody.Height;
            var   origin = new Vector2(_boltBody.Width * 0.5f, _boltBody.Height * 0.5f);

            _spriteBatch.Draw(_boltBody, pos, null,
                vis.CoreColor, rotation, origin,
                new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);
        }

        // -- Hot centre (tiny bright dot) --------------------------------------
        {
            float   hotR   = (vis.CoreWidth * 0.6f) * pulse;
            float   scale  = (hotR * 2f) / _radialGlow.Width;
            float   alpha  = 0.9f * pulse;
            Vector2 origin = new Vector2(_radialGlow.Width * 0.5f, _radialGlow.Height * 0.5f);

            _spriteBatch.Draw(_radialGlow, pos, null,
                Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        }
    }

    // -------------------------------------------------------------------------
    // Texture generation — called once in constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a square radial glow texture: bright centre, smooth falloff, transparent edges.
    /// </summary>
    private static Texture2D GenerateRadialGlow( int size)
    {
        var tex    = new Texture2D(Launcher.GD, size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx   = (x + 0.5f) - half;
                float dy   = (y + 0.5f) - half;
                float dist = MathF.Sqrt(dx * dx + dy * dy) / half;   // 0 = centre, 1 = edge
                float t    = Math.Clamp(1f - dist, 0f, 1f);
                float a    = t * t * t;                               // cubic falloff
                pixels[y * size + x] = Color.White * a;
            }
        }

        tex.SetData(pixels);
        return tex;
    }

    /// <summary>
    /// Generates a narrow bolt-body texture: bright centre stripe running along the width,
    /// soft falloff across the height. Width = long axis (length of bolt when unrotated),
    /// Height = short axis (thickness) — SpriteBatch rotation turns the Width axis to
    /// face the rotation angle, so length must be the Width dimension.
    /// </summary>
    private static Texture2D GenerateBoltBody(int length, int thickness)
    {
        var   tex     = new Texture2D(Launcher.GD, length, thickness);
        var   pixels  = new Color[length * thickness];
        float halfH   = thickness * 0.5f;

        for (int x = 0; x < length; x++)
        {
            // Fade along the length: brightest in the centre, dark at tips
            float lengthT = x / (float)(length - 1);               // 0 → 1
            float lenFade = MathF.Sin(lengthT * MathF.PI);         // bell curve tip-to-tip

            for (int y = 0; y < thickness; y++)
            {
                float dy      = Math.Abs((y + 0.5f) - halfH) / halfH;   // 0 = centre, 1 = edge
                float widthA  = Math.Clamp(1f - dy * dy, 0f, 1f);        // parabolic thickness falloff
                float a       = widthA * lenFade;
                pixels[y * length + x] = Color.White * a;
            }
        }

        tex.SetData(pixels);
        return tex;
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        _radialGlow.Dispose();
        _boltBody.Dispose();
        _particle.Dispose();
    }
}
