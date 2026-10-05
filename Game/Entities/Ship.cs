using Microsoft.Xna.Framework;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities.ShipParts;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Base class for all ships (player and NPCs). A facade over small components:
/// <see cref="ShipPhysics"/>, <see cref="ShipDurability"/>, <see cref="ShipSensors"/>,
/// <see cref="WeaponSystem"/> and <see cref="ShipNavigator"/>.
/// The public/JSON surface is unchanged.
/// </summary>
public abstract class Ship
{
    private ShipStats _shipType;

    /// <summary>
    /// Serializer-facing setter: only <see cref="ShipStats.ShipTypeName"/> is persisted,
    /// so assignment resolves the full preset via <see cref="ApplyShipTypeByName"/>.
    /// </summary>
    public ShipStats ShipType
    {
        get => _shipType;
        set
        {
            if (value is null)
                _shipType = null;
            else
                ApplyShipTypeByName(value.ShipTypeName);
        }
    }

    /// <summary>
    /// Resolves a <see cref="ShipStats"/> preset by name (falling back to the default preset)
    /// and applies it. This is the single place where the hidden lookup behind the
    /// <see cref="ShipType"/> setter happens.
    /// </summary>
    public void ApplyShipTypeByName(string shipTypeName)
    {
        _shipType = ShipStats.FromName(shipTypeName);
    }

    public string Name { get; set; }
    public int? CurrentHullStrength   { get => Durability.Hull;   set => Durability.Hull = value; }
    public int? CurrentShieldStrength { get => Durability.Shield; set => Durability.Shield = value; }
    public int? CurrentFuelLevel      { get => Durability.Fuel;   set => Durability.Fuel = value; }
    [JsonConverter(typeof(EquipmentListConverter))]
    public List<Equipment> Equipment { get; set; } = new();
    [JsonIgnore] public string Id { get; set; }
    [JsonIgnore] public Ship Target { get => Sensors.Target; set => Sensors.Target = value; }
    [JsonIgnore] public Vector2 CurrentGravity => StarSystem == null ? Vector2.Zero : StarSystem.Spatial.CalculateGravityAtLocation(Position);
    [JsonIgnore] public float Radius => ShipType.Radius;
    /// <summary>The system this ship is in. Null until attached (see <see cref="Entities.StarSystem.AddNpc"/> / <see cref="Universe"/>).</summary>
    [JsonIgnore] public StarSystem StarSystem { get; set; }

    // -- Components ---------------------------------------------------------
    [JsonIgnore] public ShipPhysics    Physics    { get; }
    [JsonIgnore] public ShipDurability Durability { get; }
    [JsonIgnore] public ShipSensors    Sensors    { get; }
    [JsonIgnore] public WeaponSystem   Weapons    { get; }
    [JsonIgnore] public ShipNavigator  Navigator  { get; }

    public Vector2 Position
    {
        get => Physics.Transform.Position;
        set => Physics.Transform.Position = value;
    }
    public float Rotation
    {
        get => Physics.Transform.Rotation;
        set => Physics.Transform.Rotation = value;
    }
    public float Scale
    {
        get => Physics.Transform.Scale;
        set => Physics.Transform.Scale = value;
    }
    public Vector2 Velocity
    {
        get => Physics.Body.Velocity;
        set => Physics.Body.Velocity = value;
    }
    [JsonIgnore] public Vector2 Forward => Physics.Transform.Forward;
    [JsonIgnore] public float Speed => Velocity.Length();
    [JsonIgnore] public float MaxSpeed => ShipType.MaxSpeed;
    [JsonIgnore] public int MaxHullStrength => Durability.MaxHull;
    [JsonIgnore] public int MaxShieldStrength => Durability.MaxShield;
    [JsonIgnore] public int MaxFuelLevel => Durability.MaxFuel;
    [JsonIgnore] public float CurrentHullPercentage => Durability.HullPercentage;
    [JsonIgnore] public float CurrentShieldPercentage => Durability.ShieldPercentage;
    [JsonIgnore] public float CurrentFuelPercentage => Durability.FuelPercentage;
    [JsonIgnore] public float MandevilleRadius => StarSystem == null ? 0f : StarSystem.MandevilleRadius;
    [JsonIgnore] public float DistanceFromSystemCenter => DistanceTo(Vector2.Zero);

    protected Ship(string name, string shipType) : this()
    {
        Name = name;
        ApplyShipTypeByName(shipType);
        Physics.Body.Mass = ShipType.Mass;
    }

    /// <summary>
    /// Parameterless constructor used by System.Text.Json. Runtime components are
    /// created here because they are not persisted.
    /// </summary>
    protected Ship()
    {
        Physics    = new ShipPhysics(() => ShipType);
        Durability = new ShipDurability(() => ShipType);
        Sensors    = new ShipSensors(this);
        Weapons    = new WeaponSystem(() => Equipment);
        Navigator  = new ShipNavigator(this);
    }

    /// <summary>
    /// Recomputes runtime-only physics state after construction or deserialization.
    /// Art is loaded on the render side (<c>AssetService.EnsureShipArt</c>) using
    /// <see cref="ShipStats.SpriteName"/>, <see cref="ShipStats.SplashName"/> and <see cref="SplashArtKey"/>.
    /// </summary>
    public virtual void Generate()
    {
        // Mass depends on ShipType, which is not available to the JSON constructor,
        // so it is (re)computed once everything is populated.
        Physics.Body.Mass = ShipType.Mass;
    }

    /// <summary>Cache key used to look up this ship's splash/portrait art.</summary>
    [JsonIgnore] public string SplashArtKey => SplashArtKeyFor(ShipType);

    public static string SplashArtKeyFor(ShipStats shipType) => $"splash_{shipType.ShipTypeName}";

    // -- Movement -----------------------------------------------------------
    /// <inheritdoc cref="ShipPhysics.ApplyThrust"/>
    public void ApplyThrust(float deltaTime) => Physics.ApplyThrust(deltaTime);

    /// <inheritdoc cref="ShipPhysics.RotateTowards"/>
    public bool RotateTowards(float targetAngle, float deltaTime, float threshold = float.MaxValue)
        => Physics.RotateTowards(targetAngle, deltaTime, threshold);

    /// <inheritdoc cref="ShipPhysics.ApplyRotation"/>
    public void ApplyRotation(Direction d, float deltaTime) => Physics.ApplyRotation(d, deltaTime);

    /// <summary>
    /// Runs navigation, applies gravity and integrates physics, then advances weapon cooldowns.
    /// Should be called after all control inputs are applied.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (Target != null && !Sensors.IsTargetValid(Target))
            Sensors.ClearTarget();

        Navigator.Update(deltaTime);

        // Gravity is not capped by MaxSpeed - it can push ships beyond their normal limits
        Physics.Integrate(StarSystem.Spatial.CalculateGravityAtLocation(Position), deltaTime);

        Weapons.UpdateCooldowns(deltaTime);
    }

    // -- Navigation ---------------------------------------------------------
    [JsonIgnore] public bool HasActiveNavTask => Navigator.HasActiveTask;

    public void EnqueueNavTask(NavTask task) => Navigator.Enqueue(task);

    // -- Weapons ------------------------------------------------------------
    /// <summary>Fires all ready primary weapons into the current system's projectile list.</summary>
    public void FireWeapons()
    {
        if (StarSystem == null)
            return;
        Weapons.Fire(this, StarSystem.Projectiles);
    }

    // -- Damage -------------------------------------------------------------
    /// <summary>
    /// Applies an incoming projectile, depleting shields first and then hull, and
    /// notifies the system so the renderer can show the hit.
    /// </summary>
    public void ApplyDamage(Projectile projectile)
    {
        if (projectile == null || IsDestroyed)
            return;

        // Captured before damage lands so the effect reflects the shield state that took the hit.
        bool shieldWasUp = CurrentShieldStrength is > 0;

        Durability.ApplyDamage(projectile.Damage, projectile.Owner);
        StarSystem?.RaiseShipHit(this, projectile, shieldWasUp);
    }

    /// <inheritdoc cref="ShipDurability.ApplyDamage"/>
    public void ApplyDamage(int damage, Ship source = null) => Durability.ApplyDamage(damage, source);

    [JsonIgnore] public bool IsDestroyed => Durability.IsDestroyed;

    [JsonIgnore] public Ship LastDamageSource => Durability.LastDamageSource;

    // -- Sensors / targeting -----------------------------------------------
    public float DistanceTo(Vector2 position) => Vector2.Distance(Position, position);

    public void SetTarget(Ship target) => Sensors.SetTarget(target);
    public void ClearTarget() => Sensors.ClearTarget();
    public Ship GetNearestTarget() => Sensors.GetNearestTarget();
    public bool IsTargetValid(Ship target) => Sensors.IsTargetValid(target);

    public bool IsApproachingCollision(Vector2 position, float dangerRadius, float lookAheadTime = 5f)
        => Sensors.IsApproachingCollision(position, dangerRadius, lookAheadTime);

    /// <summary>Returns true if the ship is outside the Mandeville radius and free of gravity (can jump).</summary>
    public bool CanJump() => !IsInsideMandevilleRadius() && CurrentGravity == Vector2.Zero;

    /// <summary>Returns true if the ship is inside the Mandeville radius.</summary>
    public bool IsInsideMandevilleRadius() => DistanceFromSystemCenter < MandevilleRadius;
}
