using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Render-side hit visuals: shield flares, hull scorch flashes and hull debris.
/// Populated from <see cref="StarSystem.ShipHit"/> events; the simulation holds no visual state.
/// </summary>
public class ShipImpactEffects
{
    private static readonly Color ShieldFlareColor = new(120, 190, 255);
    private static readonly Color HullDebrisColor  = new(180, 180, 190);

    private readonly Random _rng = new();
    private readonly Dictionary<Ship, ShipEffects> _byShip = new();
    private StarSystem _subscribed;

    public sealed class ShipEffects
    {
        public List<ShipImpact> Impacts { get; } = new();
        public List<HullDebris> Debris  { get; } = new();
        public bool IsEmpty => Impacts.Count == 0 && Debris.Count == 0;
    }

    private static readonly ShipEffects Empty = new();

    /// <summary>Subscribes to the active system's hit events, switching on system change.</summary>
    public void Attach(StarSystem system)
    {
        if (ReferenceEquals(system, _subscribed))
            return;

        if (_subscribed != null)
            _subscribed.ShipHit -= OnShipHit;

        _byShip.Clear();
        _subscribed = system;

        if (_subscribed != null)
            _subscribed.ShipHit += OnShipHit;
    }

    public ShipEffects For(Ship ship) => ship != null && _byShip.TryGetValue(ship, out var fx) ? fx : Empty;

    public void Update(float deltaTime)
    {
        List<Ship> finished = null;
        foreach (var (ship, fx) in _byShip)
        {
            for (int i = fx.Impacts.Count - 1; i >= 0; i--)
            {
                fx.Impacts[i].Update(deltaTime);
                if (fx.Impacts[i].IsExpired) fx.Impacts.RemoveAt(i);
            }
            for (int i = fx.Debris.Count - 1; i >= 0; i--)
            {
                fx.Debris[i].Update(deltaTime);
                if (fx.Debris[i].IsExpired) fx.Debris.RemoveAt(i);
            }
            if (fx.IsEmpty) (finished ??= new()).Add(ship);
        }
        if (finished != null)
            foreach (var s in finished) _byShip.Remove(s);
    }

    private void OnShipHit(Ship ship, Projectile projectile, bool shieldWasUp)
    {
        if (!_byShip.TryGetValue(ship, out var fx))
            _byShip[ship] = fx = new ShipEffects();

        Vector2 toImpact = projectile.Position - ship.Position;
        float angle = toImpact.LengthSquared() > 0.0001f
            ? MathF.Atan2(toImpact.Y, toImpact.X)
            : projectile.Rotation;

        // Severity scales the effect so heavy weapons read louder than light ones.
        float severity = MathHelper.Clamp(projectile.Damage / 40f, 0.15f, 1.5f);

        if (shieldWasUp)
        {
            // The shield sits just outside the hull; the flare hugs that surface.
            float shieldRadius = ship.Radius * 1.35f;
            fx.Impacts.Add(new ShipImpact
            {
                ShieldHit = true,
                Position  = ship.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * shieldRadius,
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
        fx.Impacts.Add(new ShipImpact
        {
            ShieldHit = false,
            Position  = projectile.Position,
            Angle     = angle,
            Radius    = MathHelper.Lerp(4f, 12f, MathHelper.Clamp(severity, 0f, 1f)),
            ArcWidth  = 0f,
            Color     = ProjectileVisuals.For(projectile.WeaponName).CoreColor,
            MaxLife   = 0.22f,
            Life      = 0.22f,
        });

        SpawnHullDebris(fx, ship, projectile, angle, severity);
    }

    /// <summary>Throws hull fragments outward from the impact, back along the projectile's path.</summary>
    private void SpawnHullDebris(ShipEffects fx, Ship ship, Projectile projectile, float impactAngle, float severity)
    {
        int count = Math.Clamp(6 + (int)(severity * 14f), 6, 26);

        for (int i = 0; i < count; i++)
        {
            float bias   = (float)(_rng.NextDouble() + _rng.NextDouble()) * 0.5f;
            float spread = (bias - 0.5f) * MathHelper.Pi;
            float angle  = impactAngle + spread;

            float speed = MathHelper.Lerp(30f, 190f, (float)_rng.NextDouble()) * (0.5f + severity * 0.5f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float life = MathHelper.Lerp(0.25f, 0.8f, (float)_rng.NextDouble());

            // Mix hot sparks with cooler hull fragments.
            Color color = _rng.NextDouble() < 0.45 ? ProjectileVisuals.For(projectile.WeaponName).CoreColor : HullDebrisColor;

            fx.Debris.Add(new HullDebris
            {
                Position        = projectile.Position,
                Velocity        = ship.Velocity + direction * speed,
                Rotation        = (float)(_rng.NextDouble() * MathHelper.TwoPi),
                AngularVelocity = MathHelper.Lerp(-10f, 10f, (float)_rng.NextDouble()),
                Size            = MathHelper.Lerp(1f, 3.5f, (float)_rng.NextDouble()),
                Color           = color,
                MaxLife         = life,
                Life            = life,
            });
        }
    }
}
