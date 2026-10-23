using Microsoft.Xna.Framework;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>
/// Base class for all ships (player and NPCs). A facade over small components:
/// <see cref="ShipPhysics"/>, <see cref="ShipDurability"/>, <see cref="ShipSensors"/>,
/// <see cref="WeaponSystem"/> and <see cref="ShipNavigator"/>.
/// The public/JSON surface is unchanged.
/// </summary>
public abstract class Ship
{
    /// <summary>
    /// This ship's type. Only its name is saved (see <see cref="ShipStatsConverter"/>). Set it with <see cref="AssignShipType"/>,
    /// which also gives the ship the type's default loadout.
    /// </summary>
    [JsonConverter(typeof(ShipStatsConverter))]
    public ShipStats ShipType { get; set; }

    /// <summary>
    /// Makes this ship a ship of <paramref name="shipType"/>: it takes the type's default loadout as its equipment (replacing
    /// what it had) and starts full of hull, shield and fuel. The loadout can be changed from there. The ship type object
    /// should not be given to another ship (make a new one, such as <c>new Shuttle()</c>, for each ship).
    /// </summary>
    public void AssignShipType(ShipStats shipType)
    {
        ShipType = shipType ?? throw new ArgumentNullException(nameof(shipType));
        Physics.Body.Mass = shipType.Flight.Mass;

        Equipment = new List<EquipmentComponent>(shipType.DefaultLoadout);

        // Filled last: the maximums include what the equipment adds.
        CurrentHullStrength = MaxHullStrength;
        CurrentShieldStrength = MaxShieldStrength;
        CurrentFuelLevel = MaxFuelLevel;
    }

    public string Name { get; set; }

    /// <summary>
    /// The money this ship carries. The player spends it at spaceports; an NPC has some from the moment it is made, for the player to loot later.
    /// Saved with the player (a save from before credits existed reads as 0).
    /// </summary>
    public int Credits { get; set; }

    public bool CanAfford(int amount) => amount >= 0 && Credits >= amount;

    /// <summary>Takes <paramref name="amount"/> from the ship's credits. Returns false, changing nothing, when it has less than that.</summary>
    public bool TrySpend(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (Credits < amount)
            return false;

        Credits -= amount;
        return true;
    }

    /// <summary>Adds <paramref name="amount"/> to the ship's credits.</summary>
    public void Earn(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Credits += amount;
    }
    public int? CurrentHullStrength   { get => Durability.Hull;   set => Durability.Hull = value; }
    public int? CurrentShieldStrength { get => Durability.Shield; set => Durability.Shield = value; }
    public int? CurrentFuelLevel      { get => Durability.Fuel;   set => Durability.Fuel = value; }
    [JsonConverter(typeof(EquipmentListConverter))]
    public List<EquipmentComponent> Equipment { get; set; } = new();

    // -- Equipment limits and value ------------------------------------------

    /// <summary>The total mass of the equipment the ship carries.</summary>
    [JsonIgnore] public float EquipmentMass => Equipment.Sum(item => item.Mass);

    /// <summary>What the equipment the ship carries is worth (the sum of the item prices).</summary>
    [JsonIgnore] public int EquipmentValue => Equipment.Sum(item => item.Price);

    /// <summary>What the ship is worth, for a trade-in: the ship itself plus everything it carries.</summary>
    [JsonIgnore] public int ShipValue => ShipType.Price + EquipmentValue;

    /// <summary>How many items of the same kind as <paramref name="item"/> (the same class: every weapon is one kind) the ship carries.</summary>
    public int CountLike(EquipmentComponent item) => Equipment.Count(other => other.GetType() == item.GetType());

    /// <summary>
    /// Whether <paramref name="item"/> can be installed: there must be a free slot of its kind, meaning fewer of that class than its
    /// <see cref="EquipmentComponent.SlotLimit"/> (<see cref="InstallCheck.NoSlot"/> otherwise, always for an unknown item), and its mass must fit in what the ship can still carry (<see cref="InstallCheck.TooHeavy"/>). The slot is checked first.
    /// </summary>
    public InstallCheck CheckInstall(EquipmentComponent item)
    {
        if (CountLike(item) >= item.SlotLimit(ShipType.Limits))
            return InstallCheck.NoSlot;
        if (EquipmentMass + item.Mass > ShipType.Limits.MaxMass)
            return InstallCheck.TooHeavy;
        return InstallCheck.Ok;
    }

    /// <summary>Adds <paramref name="item"/> to the ship's equipment if it fits (see <see cref="CheckInstall"/>); returns what the check said.</summary>
    public InstallCheck Install(EquipmentComponent item)
    {
        var check = CheckInstall(item);
        if (check == InstallCheck.Ok)
            Equipment.Add(item);
        return check;
    }

    /// <summary>Takes <paramref name="item"/> off the ship. Returns false when the ship does not carry it.</summary>
    public bool Uninstall(EquipmentComponent item) => Equipment.Remove(item);
    [JsonIgnore] public string Id { get; set; }
    [JsonIgnore] public Ship Target { get => Sensors.Target; set => Sensors.Target = value; }
    [JsonIgnore] public Vector2 CurrentGravity => StarSystem == null ? Vector2.Zero : StarSystem.Spatial.CalculateGravityAtLocation(Position);
    [JsonIgnore] public float Radius => ShipType.Flight.Radius;
    [JsonIgnore] public StarSystem StarSystem { get; set; }

    // -- Components ---------------------------------------------------------
    [JsonIgnore] public ShipPhysics    Physics    { get; }
    [JsonIgnore] public ShipDurability Durability { get; }
    [JsonIgnore] public ShipSensors    Sensors    { get; }
    [JsonIgnore] public WeaponSystem   Weapons    { get; }
    [JsonIgnore] public ShipNavigator  Navigator  { get; }

    public virtual Vector2 Position
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
    [JsonIgnore] public float MaxSpeed => ShipType.Flight.MaxSpeed;
    [JsonIgnore] public int MaxHullStrength => Durability.MaxHull;
    [JsonIgnore] public int MaxShieldStrength => Durability.MaxShield;
    [JsonIgnore] public int MaxFuelLevel => Durability.MaxFuel;
    [JsonIgnore] public float CurrentHullPercentage => Durability.HullPercentage;
    [JsonIgnore] public float CurrentShieldPercentage => Durability.ShieldPercentage;
    [JsonIgnore] public float CurrentFuelPercentage => Durability.FuelPercentage;
    [JsonIgnore] public float MaxAcceleration => Physics.Body.Mass > 0f ? ShipType.Flight.ThrustForce / Physics.Body.Mass : 0f;
    [JsonIgnore] public float MandevilleRadius => StarSystem == null ? 0f : StarSystem.MandevilleRadius;
    [JsonIgnore] public float DistanceFromSystemCenter => DistanceTo(Vector2.Zero);

    protected Ship(string name, ShipStats shipType) : this()
    {
        Name = name;
        AssignShipType(shipType);
    }

    /// <summary>
    /// Parameterless constructor used by System.Text.Json. Runtime components are
    /// created here because they are not persisted.
    /// </summary>
    protected Ship()
    {
        Physics    = new ShipPhysics(() => ShipType.Flight);
        Durability = new ShipDurability(() => ShipType.Durability, () => Equipment);
        Sensors    = new ShipSensors(this);
        Weapons    = new WeaponSystem(() => Equipment);
        Navigator  = new ShipNavigator(this);
    }

    /// <summary>
    /// Recomputes runtime-only physics state after construction or deserialization.
    /// Art is loaded on the render side (<c>AssetService.EnsureShipArt</c>) using
    /// <see cref="ShipAppearance.SpriteName"/>, <see cref="ShipAppearance.SplashName"/> and <see cref="SplashArtKey"/>.
    /// </summary>
    public virtual void Generate()
    {
        // Mass depends on ShipType, which is not available to the JSON constructor,
        // so it is (re)computed once everything is populated.
        Physics.Body.Mass = ShipType.Flight.Mass;
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

        // Nav tasks all need a system; a detached ship keeps its queue until it is attached.
        if (StarSystem != null)
            Navigator.Update(deltaTime);

        // Detached ships (no system yet) feel no gravity.
        Vector2 gravity = StarSystem?.Spatial.CalculateGravityAtLocation(Position) ?? Vector2.Zero;

        // Gravity is not capped by MaxSpeed - it can push ships beyond their normal limits
        Physics.Integrate(gravity, deltaTime);

        Weapons.UpdateCooldowns(deltaTime);
        Durability.Update(deltaTime);
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

    // -- Hostility ----------------------------------------------------------
    private readonly HashSet<Ship> _hostiles = new();

    /// <summary>Ships this ship treats as enemies (for example, ones that shot it). Not saved.</summary>
    [JsonIgnore] public IReadOnlyCollection<Ship> Hostiles => _hostiles;

    public bool IsHostileTo(Ship other) => other != null && _hostiles.Contains(other);

    /// <summary>Flags <paramref name="other"/> as hostile. A ship is never hostile to itself.</summary>
    public void MarkHostile(Ship other)
    {
        if (other != null && !ReferenceEquals(other, this))
            _hostiles.Add(other);
    }

    public void ForgetHostile(Ship other) => _hostiles.Remove(other);

    /// <summary>Called after this ship has been damaged by <paramref name="attacker"/> and is still alive.</summary>
    protected virtual void OnAttacked(Ship attacker) { }

    /// <summary>Longest range of this ship's primary weapons; 0 when unarmed.</summary>
    [JsonIgnore] public float WeaponRange => Weapons.MaxRange;

    /// <summary>Muzzle speed of this ship's longest-range weapon, relative to the ship; 0 when unarmed.</summary>
    [JsonIgnore] public float ProjectileSpeed => Weapons.ProjectileSpeed;

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
        StarSystem?.Universe?.Events.Publish(new ShipDamaged(this, projectile, shieldWasUp));
        if (IsDestroyed)
            StarSystem?.Universe?.Events.Publish(new ShipDestroyed(this, projectile.Owner));
        else
            OnAttacked(projectile.Owner);
    }

    /// <inheritdoc cref="ShipDurability.ApplyDamage"/>
    public void ApplyDamage(int damage, Ship source = null) => Durability.ApplyDamage(damage, source);

    /// <summary>Recharges every shield generator to full at once.</summary>
    public void RefillShield() => Durability.Shield = Durability.MaxShield;

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
