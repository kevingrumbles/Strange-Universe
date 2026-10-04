using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>Hit visuals: shield flares, hull scorch flashes and hull debris.</summary>
public abstract partial class Ship
{
    /// <summary>
    /// Visual effects produced by recent hits. Owned by the ship so they follow it
    /// and are cleaned up with it.
    /// </summary>
    [JsonIgnore] public List<ShipImpact> Impacts { get; } = new();

    /// <summary>Hull fragments blasted off by unshielded hits.</summary>
    [JsonIgnore] public List<HullDebris> Debris { get; } = new();

    private static readonly Random _impactRng = new();

    /// <summary>Colour of the shield flare arc.</summary>
    private static readonly Color ShieldFlareColor = new(120, 190, 255);

    /// <summary>Colour of cool hull fragments.</summary>
    private static readonly Color HullDebrisColor = new(180, 180, 190);

    /// <summary>
    /// Produces the hit visual: a curved shield flare when the shield absorbed the
    /// blow, or hull spall when it struck bare hull.
    /// </summary>
    private void SpawnImpactEffect(Projectile projectile, bool shieldWasUp)
    {
        Vector2 toImpact = projectile.Position - Position;

        float angle = toImpact.LengthSquared() > 0.0001f
            ? MathF.Atan2(toImpact.Y, toImpact.X)
            : projectile.Rotation;

        var visual = projectile.Visual;

        // Severity scales the effect so heavy weapons read louder than light ones.
        float severity = MathHelper.Clamp(projectile.Damage / 40f, 0.15f, 1.5f);

        if (shieldWasUp)
        {
            // The shield sits just outside the hull; the flare hugs that surface.
            float shieldRadius = Radius * 1.35f;

            Impacts.Add(new ShipImpact
            {
                ShieldHit = true,
                Position  = Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * shieldRadius,
                Angle     = angle,
                Radius    = shieldRadius,
                ArcWidth  = MathHelper.Lerp(0.35f, 0.9f, MathHelper.Clamp(severity, 0f, 1f)),
                Color     = ShieldFlareColor,
                MaxLife   = 0.35f,
                Life      = 0.35f,
            });

            return;
        }

        // Bare hull: brief scorch flash plus a spray of fragments.
        Impacts.Add(new ShipImpact
        {
            ShieldHit = false,
            Position  = projectile.Position,
            Angle     = angle,
            Radius    = MathHelper.Lerp(4f, 12f, MathHelper.Clamp(severity, 0f, 1f)),
            ArcWidth  = 0f,
            Color     = visual.CoreColor,
            MaxLife   = 0.22f,
            Life      = 0.22f,
        });

        SpawnHullDebris(projectile, angle, severity);
    }

    /// <summary>
    /// Throws hull fragments outward from the impact, spraying back along the
    /// projectile's path.
    /// </summary>
    private void SpawnHullDebris(Projectile projectile, float impactAngle, float severity)
    {
        int count = Math.Clamp(6 + (int)(severity * 14f), 6, 26);

        // Fragments spall back out of the hull, away from the ship centre.
        float baseAngle = impactAngle;

        for (int i = 0; i < count; i++)
        {
            float bias   = (float)(_impactRng.NextDouble() + _impactRng.NextDouble()) * 0.5f;
            float spread = (bias - 0.5f) * MathHelper.Pi;
            float angle  = baseAngle + spread;

            float speed = MathHelper.Lerp(30f, 190f, (float)_impactRng.NextDouble())
                        * (0.5f + severity * 0.5f);

            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float life = MathHelper.Lerp(0.25f, 0.8f, (float)_impactRng.NextDouble());

            // Mix hot sparks with cooler hull fragments.
            Color color = _impactRng.NextDouble() < 0.45
                ? projectile.Visual.CoreColor
                : HullDebrisColor;

            Debris.Add(new HullDebris
            {
                Position        = projectile.Position,
                Velocity        = Velocity + direction * speed,
                Rotation        = (float)(_impactRng.NextDouble() * MathHelper.TwoPi),
                AngularVelocity = MathHelper.Lerp(-10f, 10f, (float)_impactRng.NextDouble()),
                Size            = MathHelper.Lerp(1f, 3.5f, (float)_impactRng.NextDouble()),
                Color           = color,
                MaxLife         = life,
                Life            = life,
            });
        }
    }

    /// <summary>Advances hit visuals and discards finished ones.</summary>
    private void UpdateImpactEffects(float deltaTime)
    {
        for (int i = Impacts.Count - 1; i >= 0; i--)
        {
            Impacts[i].Update(deltaTime);
            if (Impacts[i].IsExpired)
                Impacts.RemoveAt(i);
        }

        for (int i = Debris.Count - 1; i >= 0; i--)
        {
            Debris[i].Update(deltaTime);
            if (Debris[i].IsExpired)
                Debris.RemoveAt(i);
        }
    }
}
