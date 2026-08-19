using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Systems;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using static StrangeUniverse.StaticHelpers;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Base class for all ships (player and NPCs).
/// Contains shared functionality: movement, physics, rendering, ship statistics, and resources.
/// Does not contain player input or AI logic.
/// </summary>
public abstract class Ship
{
    public string ShipName { get; set; }
    public int? CurrentHullStrength { get; set; } = null;
    public int? CurrentShieldStrength { get; set; } = null;
    public int? CurrentFuelLevel { get; set; } = null;
    [JsonConverter(typeof(EquipmentListConverter))]
    public List<Equipment> Equipment { get; set; } = new();
    [JsonIgnore] public List<Equipment> PrimaryWeapons { get => Equipment.Where(e => e.EquipmentStats.PrimaryWeapon).ToList(); }
    [JsonIgnore] public bool HasActiveNavTask => ActiveNavTask is not null;
    [JsonIgnore] private NavTask ActiveNavTask { get; set; } = null;
    [JsonIgnore] private Queue<NavTask> NavTaskQueue { get; set; } = new Queue<NavTask>();
    [JsonIgnore] public string Id { get; set; }
    [JsonIgnore] public string Name { get; set; }
    [JsonIgnore] public Ship Target { get; set; }
    [JsonIgnore] public Vector2 CurrentGravity => StarSystem == null ? Vector2.Zero : StarSystem.CalculateGravityAtLocation(Transform.Position);
    [JsonIgnore] public ShipStats ShipStats { get; set; } = new();
    [JsonIgnore] public float Radius { get => CalculateRadius() ; }
    [JsonIgnore] public StarSystem StarSystem { get { return Launcher.ActiveUniverse.ActiveStarSystem; } }
    [JsonIgnore] private PhysicsBody Physics { get; set; }
    private Transform Transform { get; set; } = new();
    public Vector2 Position
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }
    public float Rotation
    {
        get => Transform.Rotation;
        set => Transform.Rotation = value;
    }
    public float Scale
    {
        get => Transform.Scale;
        set => Transform.Scale = value;
    }
    public Vector2 Velocity 
    { 
        get => Physics.Velocity; 
        set => Physics.Velocity = value;
    }
    [JsonIgnore] public Vector2 Forward
    {
        get => Transform.Forward;
    }
    [JsonIgnore] public float Speed => Velocity.Length();
    [JsonIgnore] public float MaxSpeed => CalculateMaxSpeed();
    [JsonIgnore] public int MaxHullStrength => CalculateMaxHull();
    [JsonIgnore] public int MaxShieldStrength => CalculateMaxShield();
    [JsonIgnore] public int MaxFuelLevel => CalculateMaxFuel();
    [JsonIgnore] public float CurrentHullPercentage => CurrentHullStrength.HasValue && MaxHullStrength > 0 
        ? (float)CurrentHullStrength.Value / MaxHullStrength * 100f 
        : 0f;
    [JsonIgnore] public float CurrentShieldPercentage => CurrentShieldStrength.HasValue && MaxShieldStrength > 0 
        ? (float)CurrentShieldStrength.Value / MaxShieldStrength * 100f 
        : 0f;
    [JsonIgnore] public float CurrentFuelPercentage => CurrentFuelLevel.HasValue && MaxFuelLevel > 0 
        ? (float)CurrentFuelLevel.Value / MaxFuelLevel * 100f 
        : 0f;
    [JsonIgnore] public float MandevilleRadius => StarSystem == null ? 0f : StarSystem.MandevilleRadius;
    [JsonIgnore] public float DistanceFromSystemCenter => DistanceTo(Vector2.Zero);

    protected Ship(string shipName)
    {
        ShipStats = ShipStats.GetShipStats(shipName);
        ShipName = shipName ?? ShipStats.ShipName;

        Physics = new PhysicsBody
        {
            Mass = CalculateMass(),
        };
    }

    /// <summary>
    /// Loads the ship's sprite texture and registers it in the texture cache.
    /// Also loads the splash art (portrait) used in the HUD target panel,
    /// falling back to the standard sprite if no splash art is defined or found.
    /// </summary>
    public virtual void Generate()
    {
        var tex = ArtLoader.TryLoad(Launcher.GD, ShipStats.SpriteName);
        Launcher.TextureCache.Register(ShipStats.ShipName, tex);

        // Splash art -- fall back to the standard sprite if not provided or not found
        Texture2D splashTex = null;
        if (!string.IsNullOrEmpty(ShipStats.SplashName))
            splashTex = ArtLoader.TryLoad(Launcher.GD, ShipStats.SplashName);
        Launcher.TextureCache.Register(SplashArtKey, splashTex ?? tex);
    }

    /// <summary>Cache key used to look up this ship's splash/portrait art.</summary>
    [JsonIgnore] public string SplashArtKey => $"splash_{ShipStats.ShipName}";

    /// <summary>
    /// Applies thrust force in the ship's current facing direction.
    /// Uses a soft speed cap that gradually reduces acceleration-contributing thrust as MaxSpeed is approached.
    /// Lateral and decelerating components are never reduced.
    /// </summary>
    public void ApplyThrust(float deltaTime)
    {
        Vector2 thrustForce = Forward * ShipStats.ThrustForce;
        float currentSpeed = Velocity.Length();

        if (currentSpeed > 0f)
        {
            Vector2 velDir = Velocity / currentSpeed;
            float parallelMag = Vector2.Dot(thrustForce, velDir);

            // Only reduce thrust that would push speed higher (positive parallel component)
            if (parallelMag > 0f)
            {
                float softStart = ShipStats.MaxSpeed * ShipStats.SoftCapStart;

                if (currentSpeed >= ShipStats.MaxSpeed)
                {
                    // At or above max: strip the forward component entirely.
                    // The ship can still turn — lateral thrust is unaffected.
                    thrustForce -= velDir * parallelMag;
                }
                else if (currentSpeed > softStart)
                {
                    // Soft zone: linearly fade the forward component to zero.
                    float t = (currentSpeed - softStart) / (ShipStats.MaxSpeed - softStart);
                    thrustForce -= velDir * (parallelMag * t);
                }
                // Below softStart: full thrust, no reduction
            }
            // Negative parallel (decelerating) and lateral components: never reduced
        }

        Physics.ApplyForce(thrustForce, deltaTime);
    }

    /// <summary>
    /// Rotates the ship towards a target angle.
    /// Returns true if already aligned within the threshold.
    /// </summary>
    public bool RotateTowards(float targetAngle, float deltaTime, float threshold = float.MaxValue)
    {
        float diff = StaticHelpers.WrapAngle(targetAngle - Transform.Rotation);
        float maxDelta = ShipStats.RotationSpeed * deltaTime;

        if (Math.Abs(diff) <= Math.Min(maxDelta, threshold))
        {
            Transform.Rotation = targetAngle;
            return true;
        }

        Transform.Rotation += Math.Sign(diff) * maxDelta;
        return false;
    }

    /// <summary>
    /// Applies rotation input to the ship.
    /// </summary>
    public void ApplyRotation(Direction d, float deltaTime)
    {
        switch (d)
        {
            case Direction.Left:
                Transform.Rotation -= ShipStats.RotationSpeed * deltaTime;
                break;
            case Direction.Right:
                Transform.Rotation += ShipStats.RotationSpeed * deltaTime;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Applies gravitational forces from the star system and integrates physics.
    /// Should be called after all control inputs are applied.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Clear target if it becomes invalid
        if (Target != null && !IsTargetValid(Target))
        {
            ClearTarget();
        }

        if (ActiveNavTask is not null)
        {
            ActiveNavTask.Update(deltaTime);
            switch (ActiveNavTask.CurrentState)
            {
                case TaskState.Complete:
                    if (ActiveNavTask is JumpTask jumpTask && StarSystem.SystemId == jumpTask._targetSystemId)
                    {
                        Launcher.ActiveUniverse.JumpRoute.Remove(jumpTask._targetSystemId);
                        CurrentFuelLevel--;
                    }
                    ActiveNavTask = null;
                    break;
                case TaskState.Invalid:
                    ActiveNavTask = null;
                    break;
            }
        }
        if (ActiveNavTask is null && NavTaskQueue.Count > 0)
        {
            ActiveNavTask = NavTaskQueue.Dequeue();
        }

        // Apply gravitational forces from celestial bodies
        // Gravity is not capped by MaxSpeed - it can push ships beyond their normal limits
        Physics.ApplyForce(StarSystem.CalculateGravityAtLocation(Transform.Position), deltaTime);
        Physics.Integrate(Transform, deltaTime);

        // Advance weapon cooldowns
        foreach (Equipment weapon in PrimaryWeapons)
            weapon.EquipmentStats.UpdateCooldown(deltaTime);
    }
    public void EnqueueNavTask(NavTask task)
    {
        NavTaskQueue.Enqueue(task);
    }

    /// <summary>
    /// Fires all installed weapons that are ready. Spawned projectiles are added
    /// to the current StarSystem's Projectiles list.
    /// </summary>
    public void FireWeapons()
    {
        if (StarSystem == null)
            return;

        foreach (Equipment weapon in PrimaryWeapons)
        {
            float attackSpeedBonus   = CalculateBaseAttackSpeed();
            float attackRangeBonus   = CalculateBaseAttackRange();
            float accuracyBonus      = CalculateBaseAccuracyBonus();
            Projectile projectile = weapon.EquipmentStats.TryFire(this, attackSpeedBonus, attackRangeBonus, accuracyBonus);
            if (projectile != null)
                StarSystem.Projectiles.Add(projectile);
        }
    }

    #region Ship Stats Calculations
    private float CalculateMass()
    {
        return ShipStats.Mass;
    }

    private int CalculateMaxHull()
    {
        return ShipStats.MaxHull;
    }

    private int CalculateMaxShield()
    {
        return ShipStats.MaxShield;
    }

    private int CalculateMaxFuel()
    {
        return ShipStats.MaxFuel;
    }

    private float CalculateRadius()
    {
        return ShipStats.Radius;
    }
    
    private float CalculateMaxSpeed()
    {
        return ShipStats.MaxSpeed;
    }

    private float CalculateBaseAttackSpeed()
    {
        float baseAttackSpeed = 0f; // Additive attacks-per-second bonus from utility gear
        foreach (var equipment in Equipment)
        {
            if (equipment.EquipmentStats.EquipmentType == EquipmentType.Utility)
            {
                baseAttackSpeed += equipment.EquipmentStats.FireRate ?? 0f;
            }
        }
        return baseAttackSpeed;
    }

    private float CalculateBaseAttackRange()
    {
        float baseAttackRange = 0f; // Additive attack-range bonus from utility gear
        foreach (var equipment in Equipment)
        {
            if (equipment.EquipmentStats.EquipmentType == EquipmentType.Utility)
            {
                baseAttackRange += equipment.EquipmentStats.Range ?? 0f;
            }
        }
        return baseAttackRange;
    }

    private float CalculateBaseAccuracyBonus()
    {
        float bonus = 0f; // Additive accuracy bonus from utility gear
        foreach (var equipment in Equipment)
        {
            if (equipment.EquipmentStats.EquipmentType == EquipmentType.Utility)
                bonus += equipment.EquipmentStats.Accuracy ?? 0f;
        }
        return bonus;
    }
    #endregion
    #region Sensors
    /// <summary>
    /// Returns the distance to a target position.
    /// </summary>
    public float DistanceTo(Vector2 position)
    {
        return Vector2.Distance(Transform.Position, position);
    }

    #region Targeting
    /// <summary>
    /// Sets the current target. Does not allow targeting self.
    /// </summary>
    public void SetTarget(Ship target)
    {
        if (target == this)
            return;

        Target = target;
    }

    /// <summary>
    /// Clears the current target.
    /// </summary>
    public void ClearTarget()
    {
        Target = null;
    }

    /// <summary>
    /// Gets the nearest ship in the current system, excluding self.
    /// </summary>
    public Ship GetNearestTarget()
    {
        if (StarSystem == null)
            return null;

        var allShips = GetAllShipsInSystem();
        Ship nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var ship in allShips)
        {
            if (ship == this)
                continue;

            float distance = DistanceTo(ship.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = ship;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Checks if a target is valid (not null, not self, still in system).
    /// </summary>
    public bool IsTargetValid(Ship target)
    {
        if (target == null || target == this)
            return false;

        if (StarSystem == null)
            return false;

        var allShips = GetAllShipsInSystem();
        return allShips.Contains(target);
    }

    /// <summary>
    /// Gets all ships in the current system (NPCs and player).
    /// </summary>
    private List<Ship> GetAllShipsInSystem()
    {
        var ships = new List<Ship>();

        if (StarSystem == null)
            return ships;

        // Add all NPCs
        ships.AddRange(StarSystem.Npcs);

        // Add player
        if (StarSystem.ActivePlayer != null)
        {
            ships.Add(StarSystem.ActivePlayer);
        }

        return ships;
    }
    #endregion

    /// <summary>
    /// Gets all nearby ships within sensor range.
    /// </summary>
    public IReadOnlyList<Ship> GetNearbyShips(float range = float.MaxValue)
    {
        if (StarSystem == null)
            return Array.Empty<Ship>();

        List<Ship> nearby = new List<Ship>();

        // Add NPCs
        foreach (var npc in StarSystem.Npcs)
        {
            if (npc != this && DistanceTo(npc.Transform.Position) <= range)
            {
                nearby.Add(npc);
            }
        }

        // Add player if not self
        if (StarSystem?.ActivePlayer != this)
        {
            if (DistanceTo(StarSystem.ActivePlayer.Transform.Position) <= range)
            {
                nearby.Add(StarSystem.ActivePlayer);
            }
        }

        return nearby;
    }

    /// <summary>
    /// Gets all planets within sensor range.
    /// </summary>
    public IReadOnlyList<Planet> GetNearbyPlanets(float range = float.MaxValue)
    {
        if (StarSystem?.Planets == null)
            return Array.Empty<Planet>();

        return StarSystem.Planets
            .Where(planet => DistanceTo(planet.Position) <= range)
            .ToList();
    }

    /// <summary>
    /// Gets all stars within sensor range.
    /// </summary>
    public IReadOnlyList<Star> GetNearbyStars(float range = float.MaxValue)
    {
        if (StarSystem?.Stars == null)
            return Array.Empty<Star>();

        return StarSystem.Stars
            .Where(star => DistanceTo(star.Position) <= range)
            .ToList();
    }

    /// <summary>
    /// Gets all asteroids within sensor range.
    /// </summary>
    public IReadOnlyList<Asteroid> GetNearbyAsteroids(float range = float.MaxValue)
    {
        if (StarSystem?.Asteroids == null)
            return Array.Empty<Asteroid>();

        return StarSystem.Asteroids
            .Where(asteroid => DistanceTo(asteroid.Position) <= range)
            .ToList();
    }

    /// <summary>
    /// Gets the closest planet to the ship.
    /// </summary>
    public Planet GetClosestPlanet()
    {
        if (StarSystem?.Planets == null || StarSystem.Planets.Count == 0)
            return null;

        return StarSystem.Planets
            .OrderBy(planet => DistanceTo(planet.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the closest star to the ship.
    /// </summary>
    public Star GetClosestStar()
    {
        if (StarSystem?.Stars == null || StarSystem.Stars.Count == 0)
            return null;

        return StarSystem.Stars
            .OrderBy(star => DistanceTo(star.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the closest ship to this ship.
    /// </summary>
    public Ship GetClosestShip()
    {
        var nearby = GetNearbyShips();
        if (nearby.Count == 0)
            return null;

        return nearby
            .OrderBy(ship => DistanceTo(ship.Transform.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Relative velocity to another ship.
    /// </summary>
    public Vector2 RelativeVelocity(Ship otherShip)
    {
        if (otherShip == null)
            return Vector2.Zero;
        return Velocity - otherShip.Velocity;
    }

    /// <summary>
    /// Returns true if the ship has reached a destination (within threshold).
    /// </summary>
    public bool HasReachedDestination(Vector2 destination, float threshold = 100f)
    {
        return DistanceTo(destination) <= threshold;
    }

    /// <summary>
    /// Returns true if the ship is approaching a collision with a position.
    /// Checks if current velocity is heading toward the position and distance is decreasing.
    /// </summary>
    public bool IsApproachingCollision(Vector2 position, float dangerRadius, float lookAheadTime = 5f)
    {
        float distance = DistanceTo(position);

        // Already past danger radius
        if (distance > dangerRadius * 2f)
            return false;

        // Check if heading toward position
        Vector2 toPosition = position - Transform.Position;
        if (toPosition.LengthSquared() < 0.1f)
            return true; // Already at position

        Vector2 direction = Vector2.Normalize(toPosition);
        float velocityTowardPosition = Vector2.Dot(Velocity, direction);

        // Moving away
        if (velocityTowardPosition <= 0)
            return false;

        // Predict future distance
        float futureDistance = distance - velocityTowardPosition * lookAheadTime;
        return futureDistance <= dangerRadius;
    }

    /// <summary>
    /// Returns true if approaching a collision with a planet.
    /// </summary>
    public bool IsApproachingPlanetCollision(float lookAheadTime = 5f)
    {
        if (StarSystem?.Planets == null)
            return false;

        foreach (var planet in StarSystem.Planets)
        {
            if (IsApproachingCollision(planet.Position, planet.Radius * 1.5f, lookAheadTime))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if approaching a collision with a star.
    /// </summary>
    public bool IsApproachingStarCollision(float lookAheadTime = 5f)
    {
        if (StarSystem?.Stars == null)
            return false;

        foreach (var star in StarSystem.Stars)
        {
            if (IsApproachingCollision(star.Position, star.Radius * 1.5f, lookAheadTime))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the ship is outside the Mandeville radius (can jump).
    /// </summary>
    public bool CanJump()
    {
        return !IsInsideMandevilleRadius() && CurrentGravity == Vector2.Zero;
    }

    /// <summary>
    /// Returns true if the ship is inside the Mandeville radius.
    /// </summary>
    public bool IsInsideMandevilleRadius()
    {
        return DistanceFromSystemCenter < MandevilleRadius;
    }
    #endregion
}