using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.EventSystem;
using Strange_Universe.Game.NavSystem;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

public class StarSystemNode
{
    public string SystemId { get; set; }
    public HashSet<string> SystemConnectionIds { get; set; } = new();
    public string Name { get; set; }
    public Vector2 GalaxyPosition { get; set; }
    public bool Discovered { get; set; } = false;
    [JsonIgnore] public string DisplayName
    {
        get
        {
            return (Name is null || !Discovered) ? "Undiscovered" : Name;
        }
    }

    [JsonIgnore]
    public Universe Universe
    {
        get
        {
            return Launcher.ActiveUniverse;
        }
    }
    public StarSystemNode() { }
    public StarSystemNode(Vector2 position, StarSystemNode backConnection = null)
    {
        Name = StaticHelpers.GetStarSystemName(Universe.Seed);
        SystemId = $"{Universe.Seed}_{Name}";
        GalaxyPosition = position;

        if (backConnection != null)
        {
            SystemConnectionIds.Add(backConnection.SystemId);
        }
    }
}

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    [JsonIgnore] public string SystemId { get { return Node.SystemId;  }  }
    [JsonIgnore] public HashSet<string> SystemConnectionIds { get { return Node.SystemConnectionIds; } }
    [JsonIgnore] public string Name { get { return Node.Name; } }
    [JsonIgnore] public Vector2 GalaxyPosition { get { return Node.GalaxyPosition; } }
    [JsonIgnore] public StarSystemNode Node { get; set; }
    [JsonIgnore] public Player ActivePlayer { get { return Launcher.ActiveUniverse.Player; } }
    [JsonIgnore] public Universe Universe { get { return Launcher.ActiveUniverse; } }
    [JsonIgnore] public string DisplayName { get { return Node.DisplayName; } }
    [JsonIgnore] public bool Discovered { get { return Node.Discovered; } }
    [JsonIgnore] public int    PlanetCount             { get; set; }
    [JsonIgnore] public int    AsteroidCount           { get; set; }
    [JsonIgnore] public int    StarCount               { get; set; }
    [JsonIgnore] public float  SystemRadius            { get; set; }
    [JsonIgnore] public float  MandevilleRadius        { get; set; }
    [JsonIgnore] public float  StarOrbitRadius         { get; set; }
    [JsonIgnore] public float  StarOrbitSpeed          { get; set; }
    [JsonIgnore] public float  AsteroidBeltInnerRadius { get; set; }
    [JsonIgnore] public float  AsteroidBeltOuterRadius { get; set; }
    [JsonIgnore] public int    BackgroundStarCount     { get; set; }
    [JsonIgnore] public float  StarRadius              { get; set; }
    [JsonIgnore] public float  MinPlanetRadius         { get; set; }
    [JsonIgnore] public float  MaxPlanetRadius         { get; set; }
    [JsonIgnore] public float  MinAsteroidRadius       { get; set; }
    [JsonIgnore] public float  MaxAsteroidRadius       { get; set; }
    [JsonIgnore] public int SystemConnectionCount { get; set; } = 1;
    [JsonIgnore] public int InnerPlanetCount { get; set; }
    [JsonIgnore] public List<Star>           Stars            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public List<Nonplayer> Npcs            { get; }      = new();
    [JsonIgnore] public List<Projectile> Projectiles     { get; }      = new();
    [JsonIgnore] public string        NebulaId        { get; private set; }

    private readonly EventController _eventController;
    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public StarSystem() 
    {

    }
    public StarSystem(StarSystemNode node)
    {
        var sw = Stopwatch.StartNew();
        Debug.WriteLine($"StarSystem initialization started: {sw.ElapsedMilliseconds} ms");
        Node = node;
        Node.Discovered = true;

        _eventController = new EventController(this);
        Random _systemRandom = new Random(StaticHelpers.SeedHash(Node.SystemId));
        PlanetCount = _systemRandom.Next(0, 7);
        AsteroidCount = _systemRandom.Next(0, 120);
        StarCount = _systemRandom.Next(1, 3);
        StarRadius = _systemRandom.NextWeightedFloat(250f, 500f);
        switch (StarCount)
        {
            case 1: 
                StarOrbitRadius = 0f;  
                StarOrbitSpeed = 0f;
                break;
            case 2: 
                StarOrbitRadius = _systemRandom.NextWeightedFloat(2000f, 4000f);
                StarOrbitSpeed  = _systemRandom.NextWeightedFloat(0.0001f, 0.0003f);
                break;
            case 3: 
                StarOrbitRadius = _systemRandom.NextWeightedFloat(3000f, 7000f);
                StarOrbitSpeed  = _systemRandom.NextWeightedFloat(0.00005f, 0.0002f);
                break;
        }
        
        MinPlanetRadius = _systemRandom.NextWeightedFloat(90f, 120f);
        MaxPlanetRadius = _systemRandom.NextWeightedFloat(150, 300f);
        MinAsteroidRadius = _systemRandom.NextWeightedFloat(15f, 30f);
        MaxAsteroidRadius = _systemRandom.NextWeightedFloat(40f, 50f);
        SystemConnectionCount = _systemRandom.Next(1, 4);
        if (SystemConnectionCount == 1) SystemConnectionCount = _systemRandom.Next(1, 4);
        InnerPlanetCount =_systemRandom.Next(PlanetCount);
        BackgroundStarCount = _systemRandom.Next(400, 800);

        if (Node.Name == "Sol")
        {
            InnerPlanetCount = 4;
            PlanetCount = 8;
            SystemConnectionCount = 4;
            StarOrbitRadius = 0f;
            StarOrbitSpeed = 0f;
            StarCount = 1;
            AsteroidCount = 100;
        }

        // The star core is the region occupied by all orbiting stars.
        // For a single star StarOrbitRadius == 0, so starCoreRadius == StarRadius.
        float starCoreRadius = StarOrbitRadius + StarRadius;
        // SystemRadius is always large enough to contain the star core plus a meaningful planetary region.
        float starCoreFootprint = StarOrbitRadius * 2f;
        SystemRadius = _systemRandom.NextWeightedFloat(12000f + starCoreFootprint, 25000f + starCoreFootprint);
        MandevilleRadius = SystemRadius * 0.75f;

        AsteroidBeltInnerRadius = Math.Max(
            _systemRandom.NextWeightedFloat(SystemRadius * 0.2f, SystemRadius * 0.4f),
            starCoreRadius * 2.5f);
        AsteroidBeltOuterRadius = _systemRandom.NextWeightedFloat(AsteroidBeltInnerRadius * 1.2f, SystemRadius * 0.6f);
        Debug.WriteLine($"properties loaded: {sw.ElapsedMilliseconds} ms");
        Debug.WriteLine($"Generating Stars: {sw.ElapsedMilliseconds} ms");
        GenerateStars();
        Debug.WriteLine($"Generating Planets: {sw.ElapsedMilliseconds} ms");
        GeneratePlanets();
        Debug.WriteLine($"Generating Asteroids: {sw.ElapsedMilliseconds} ms");
        GenerateAsteroids();
        Debug.WriteLine($"Generating Background Stars: {sw.ElapsedMilliseconds} ms");
        GenerateBackgroundStars();
        Debug.WriteLine($"Generating Nebula: {sw.ElapsedMilliseconds} ms");
        GenerateNebula();
        Debug.WriteLine($"Generating Connections: {sw.ElapsedMilliseconds} ms");
        GenerateConnections();
        Debug.WriteLine($"StarSystem initialization completed: {sw.ElapsedMilliseconds} ms");
    }

    public void Update(float deltaTime, InputState input)
    {
        ActivePlayer.Update(deltaTime, input);

        // Update NPCs using AI pipeline (Behavior -> Ship autopilot -> Ship physics)
        foreach (var npc in Npcs)
        {
            npc.Update(deltaTime);
        }

        // Remove NPCs that have left the system (e.g., merchants after jumping)
        Npcs.RemoveAll(npc => npc.Remove);

        _eventController.Update(deltaTime);

        UpdateStarOrbits(deltaTime);
        _physics.Update(Asteroids, deltaTime);

        // Player collisions with static objects
        _collision.Resolve(ActivePlayer, Planets, Asteroids);

        // NPC collisions with static objects
        foreach (var npc in Npcs)
        {
            _collision.ResolveNPC(npc, Planets, Asteroids);
        }

        // Ship-to-ship collisions (player vs NPCs and NPC vs NPC)
        _collision.ResolveShipToShip(ActivePlayer, Npcs);

        // Update and remove expired projectiles
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            Projectiles[i].Update(deltaTime);
            if (Projectiles[i].IsExpired)
                Projectiles.RemoveAt(i);
        }
    }

    private void UpdateStarOrbits(float deltaTime)
    {
        if (Stars.Count <= 1) return;
        foreach (var star in Stars)
        {
            star.OrbitAngle += star.OrbitSpeed * deltaTime;
            star.Position = new Vector2(
                MathF.Cos(star.OrbitAngle) * star.OrbitRadius,
                MathF.Sin(star.OrbitAngle) * star.OrbitRadius);

            // Update gravity well center as star moves
            star.GravityWell.Center = star.Position;
        }
    }

    private void GenerateConnections()
    {
        Random rng = new Random(StaticHelpers.SeedHash($"{Node.SystemId}_Connections"));

        List<Point> directions = ProceduralHelpers.GalaxyConnectionPreferredDirections.ToList();

        // Remove directions already occupied by existing connections.
        foreach (string id in Node.SystemConnectionIds)
        {
            StarSystemNode connected =
                Node.Universe.StarSystemNodes.FirstOrDefault(n => n.SystemId == id);

            if (connected == null)
                continue;

            Point dir = ProceduralHelpers.NormalizeDirection(
                (int)(connected.GalaxyPosition.X - Node.GalaxyPosition.X),
                (int)(connected.GalaxyPosition.Y - Node.GalaxyPosition.Y));

            directions.Remove(dir);
        }

        // Deterministic shuffle.
        for (int i = directions.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (directions[i], directions[j]) = (directions[j], directions[i]);
        }

        //
        // Determine how "developed" this region of space is.
        //
        const float LocalDensityRadius = 30f;

        int nearbySystemCount = Node.Universe.StarSystemNodes.Count(n =>
            n.SystemId != Node.SystemId &&
            Vector2.Distance(Node.GalaxyPosition, n.GalaxyPosition) <= LocalDensityRadius);

        // 0 nearby systems -> 5% chance
        // 40 nearby systems -> 85% chance
        double existingConnectionChance =
            Math.Clamp(
                0.05 + (nearbySystemCount / 40.0) * 0.80,
                0.05,
                0.85);

        while (Node.SystemConnectionIds.Count < SystemConnectionCount &&
               directions.Count > 0)
        {
            //
            // Prefer connecting to an existing nearby system.
            //
            const float MaxConnectionDistance = 8f;

            var nearbySystems = Node.Universe.StarSystemNodes
                .Where(n =>
                    n.SystemId != Node.SystemId &&
                    !Node.SystemConnectionIds.Contains(n.SystemId) &&
                    Vector2.Distance(Node.GalaxyPosition, n.GalaxyPosition) <= MaxConnectionDistance)
                .OrderBy(n => Vector2.Distance(Node.GalaxyPosition, n.GalaxyPosition))
                .ToList();

            if (nearbySystems.Count > 0 &&
                rng.NextDouble() < existingConnectionChance)
            {
                // Favor the closest few systems.
                int candidateCount = Math.Min(3, nearbySystems.Count);

                StarSystemNode existing =
                    nearbySystems[rng.Next(candidateCount)];

                Node.SystemConnectionIds.Add(existing.SystemId);

                if (!existing.SystemConnectionIds.Contains(Node.SystemId))
                    existing.SystemConnectionIds.Add(Node.SystemId);

                continue;
            }

            //
            // Otherwise create a new system.
            //
            Point dir = directions[0];
            directions.RemoveAt(0);

            foreach (int distance in new[]
            {
            rng.Next(2,4),
            rng.Next(4,7),
            rng.Next(7,10)
        })
            {
                Vector2 location = Node.GalaxyPosition +
                                   new Vector2(dir.X * distance,
                                               dir.Y * distance);

                StarSystemNode node = Node.Universe.StarSystemNodes
                    .FirstOrDefault(n => n.GalaxyPosition == location);

                if (node == null)
                {
                    node = new StarSystemNode(location, Node);
                    Node.Universe.StarSystemNodes.Add(node);
                }

                if (!Node.SystemConnectionIds.Contains(node.SystemId))
                {
                    Node.SystemConnectionIds.Add(node.SystemId);

                    if (!node.SystemConnectionIds.Contains(Node.SystemId))
                        node.SystemConnectionIds.Add(Node.SystemId);

                    break;
                }
            }
        }
    }

    private void GenerateStars()
    {
        for (int i = 0; i < StarCount; i++)
        {
            string starId = $"{Node.SystemId}_Star_{i}";
            Random starRandom = new Random(StaticHelpers.SeedHash(starId));
            Color starColor = StaticHelpers.StarColors[starRandom.Next(StaticHelpers.StarColors.Length)];
            string name = null;
            while (name is null || Stars.Contains(Stars.Find(s => s.Name == name)))
            {
                name = StaticHelpers.GenerateCelestialName(StaticHelpers.CelestialNameType.Star, random: starRandom);
            }
            
            float initialAngle = MathHelper.TwoPi * i / StarCount;
            Star newStar = new Star(starId, StarRadius, name, starColor, StarOrbitRadius, initialAngle, StarOrbitSpeed);
            newStar.Position = StarCount == 1
                ? Vector2.Zero
                : new Vector2(
                    MathF.Cos(initialAngle) * StarOrbitRadius,
                    MathF.Sin(initialAngle) * StarOrbitRadius);
            Stars.Add(newStar);
        }
    }

    private void GeneratePlanets()
    {
        for (int i = 1; i <= PlanetCount; i++)
        {
            switch (i) 
            {
                case var _ when i <= InnerPlanetCount:
                    Planets.Add(new Planet(planetId: $"{Node.SystemId}_Planet_{i}",
                                            minPlanetRadius: MinPlanetRadius,
                                            maxPlanetRadius: MaxPlanetRadius,
                                            minOrbit: Math.Max(StarRadius * 3.5f, StarOrbitRadius * 2f),
                                            maxOrbit: AsteroidBeltInnerRadius * 0.9f,
                                            planetNumber: i,
                                            totalPlanets: PlanetCount));
                    break;
                default:
                    Planets.Add(new Planet(planetId: $"{Node.SystemId}_Planet_{i}",
                                          minPlanetRadius: MinPlanetRadius,
                                          maxPlanetRadius: MaxPlanetRadius,
                                          minOrbit: AsteroidBeltOuterRadius * 1.1f,
                                          maxOrbit: SystemRadius * 0.75f,
                                          planetNumber: i,
                                          totalPlanets: PlanetCount));
                    break;
            }
        }
    }

    private void GenerateAsteroids()
    {
        // Pre-generate a small palette of asteroid textures and reuse them
        Random asteroidsRng = new Random(StaticHelpers.SeedHash($"{Node.SystemId}_Asteroids"));
        const int PaletteSize = 15;
        var paletteIds = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            paletteIds[i] = $"asteroid_tex_{i}";
            if (Launcher.TextureCache.TryGet(paletteIds[i], out Texture2D texture)) continue;

            var tex = Asteroid.Generate(Launcher.GD, asteroidsRng.Next());
            Launcher.TextureCache.Register(paletteIds[i], tex);
        }

        for (int i = 0; i < AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(AsteroidBeltInnerRadius, AsteroidBeltOuterRadius, (float)asteroidsRng.NextDouble());
            float angle = (float)(asteroidsRng.NextDouble() * MathHelper.TwoPi);
            float radius = MathHelper.Lerp(MinAsteroidRadius, MaxAsteroidRadius, (float)asteroidsRng.NextDouble());
            string textureId = paletteIds[asteroidsRng.Next(PaletteSize)];
            Asteroids.Add(new Asteroid(angle, orbit, radius, textureId, asteroidsRng));
        }
    }

    private void GenerateBackgroundStars()
    {
        Random backgroundStarsRng = new Random(StaticHelpers.SeedHash($"{Node.SystemId}_BackgroundStars"));

        // Positions are in virtual space that matches the tile size used for parallax scrolling
        // Must match tileWidth and tileHeight in SpriteRenderer.DrawBackgroundStars
        const float VirtualWidth = 1920f;
        const float VirtualHeight = 1080f;

        for (int i = 0; i < BackgroundStarCount; i++)
        {
            BackgroundStars.Add(new BackgroundStar
            {
                Position   = new Vector2(
                    (float)backgroundStarsRng.NextDouble() * VirtualWidth,
                    (float)backgroundStarsRng.NextDouble() * VirtualHeight),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)backgroundStarsRng.NextDouble()),
                Size       = backgroundStarsRng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 0) the nebula
                Layer      = backgroundStarsRng.NextDouble() < 0.5 ? 1 : 0,
            });
        }
    }

    private void GenerateNebula()
    {
        Random nebulaRandom = new Random(StaticHelpers.SeedHash($"{Node.SystemId}_Nebula"));

        // Select a nebula from the pool deterministically
        var pool = Node.Universe.NebulaPool;
        int poolIndex = nebulaRandom.Next(pool.Count);
        Nebula chosen = pool[poolIndex];
        NebulaId = chosen.Id;
    }

    /// <summary>
    /// Calculates the total gravitational force at a given location by summing
    /// all overlapping gravity wells from stars and planets in this system.
    /// </summary>
    /// <param name="position">World position to calculate gravity at</param>
    /// <returns>The combined gravitational force vector</returns>
    public Vector2 CalculateGravityAtLocation(Vector2 position)
    {
        Vector2 totalForce = Vector2.Zero;

        // Sum forces from all star gravity wells
        foreach (var star in Stars)
        {
            totalForce += star.GravityWell.CalculateForce(position);
        }

        // Sum forces from all planet gravity wells
        foreach (var planet in Planets)
        {
            totalForce += planet.GravityWell.CalculateForce(position);
        }

        return totalForce;
    }

    /// <summary>
    /// Calculates an entry position at the edge of the system radius based on incoming direction from a galaxy position.
    /// Adds random variance to the angle and distance for more natural-looking arrivals.
    /// </summary>
    /// <param name="fromGalaxyPosition">The galaxy position the ship is arriving from (typically the previous system)</param>
    /// <returns>A position at the system edge in the direction of arrival with variance applied</returns>
    public Vector2 GetSystemEdgeEntryPosition(Vector2? fromGalaxyPosition = null)
    {
        Random rand = new Random();

        float baseAngle;
        if (fromGalaxyPosition.HasValue)
        {
            // Calculate the direction vector from the source to this system
            Vector2 galaxyVector = GalaxyPosition - fromGalaxyPosition.Value;
            baseAngle = (float)Math.Atan2(galaxyVector.Y, galaxyVector.X);
        }
        else
        {
            // Use a random angle when no source position is provided
            baseAngle = (float)(rand.NextDouble() * Math.PI * 2); // 0 to 2π radians
        }

        // Add angular variance (±15 degrees = ±0.26 radians)
        float angleVariance = (float)((rand.NextDouble() - 0.5) * 0.52); // -0.26 to +0.26 radians
        float entryAngle = baseAngle + angleVariance;

        // Calculate entry direction
        Vector2 entryDirection = new Vector2((float)Math.Cos(entryAngle), (float)Math.Sin(entryAngle));

        // Add distance variance (±5% of system radius)
        float distanceVariance = (float)((rand.NextDouble() - 0.5) * 0.1); // -0.05 to +0.05
        float entryDistance = SystemRadius * (1f + distanceVariance);

        // Calculate and return entry position
        return entryDirection * entryDistance;
    }

    /// <summary>
    /// Generates a safe entry position and orientation just outside the Mandeville radius.
    /// The returned transform is positioned at the edge of the safe zone, facing toward
    /// the system center, suitable for a slow entry approach.
    /// </summary>
    /// <param name="arrivalAngle">Optional angle in radians for arrival direction. If null, uses a random angle.</param>
    /// <returns>A Transform configured for system entry with Position facing inward and Rotation aimed at center</returns>
    public Transform GetSafeEntryTransform(float? arrivalAngle = null)
    {
        // Use provided angle or generate a random one
        float angle = arrivalAngle ?? (float)(new Random().NextDouble() * Math.PI * 2);

        // Calculate position just outside Mandeville radius (5% margin for safety)
        Vector2 outwardDirection = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        Vector2 entryPosition = outwardDirection * (MandevilleRadius * 0.9f);

        // Calculate rotation to face system center (opposite of outward direction)
        Vector2 inwardDirection = -outwardDirection;
        float rotation = (float)Math.Atan2(inwardDirection.Y, inwardDirection.X);

        return new Transform
        {
            Position = entryPosition,
            Rotation = rotation,
            Scale = 1f
        };
    }

    /// <summary>
    /// Checks if the given location conflicts with any stars or planets and returns
    /// a safe location that is at least a safe distance away from all celestial bodies.
    /// If the location is already safe, returns it unchanged.
    /// </summary>
    /// <param name="location">The desired location to check</param>
    /// <returns>A safe location with adequate clearance from stars and planets</returns>
    public Vector2 GetSafeLocation(Vector2 location)
    {
        const float MinimumClearance = 1000f; // Minimum safe distance from any celestial body
        Vector2 safeLocation = location;
        bool needsAdjustment = true;
        int maxAttempts = 10;
        int attempts = 0;

        while (needsAdjustment && attempts < maxAttempts)
        {
            needsAdjustment = false;
            attempts++;

            // Check against all stars
            if (Stars != null)
            {
                foreach (var star in Stars)
                {
                    float distance = Vector2.Distance(safeLocation, star.Position);
                    float requiredDistance = star.Radius + MinimumClearance;

                    if (distance < requiredDistance)
                    {
                        // Move the location away from the star
                        Vector2 awayFromStar = safeLocation - star.Position;
                        if (awayFromStar.LengthSquared() < 0.1f)
                        {
                            // If at the exact star position, pick a random direction
                            Random rand = new Random();
                            float randomAngle = (float)(rand.NextDouble() * Math.PI * 2);
                            awayFromStar = new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle));
                        }
                        else
                        {
                            awayFromStar = Vector2.Normalize(awayFromStar);
                        }

                        safeLocation = star.Position + awayFromStar * requiredDistance;
                        needsAdjustment = true;
                    }
                }
            }

            // Check against all planets
            if (Planets != null)
            {
                foreach (var planet in Planets)
                {
                    float distance = Vector2.Distance(safeLocation, planet.Position);
                    float requiredDistance = planet.Radius + MinimumClearance;

                    if (distance < requiredDistance)
                    {
                        // Move the location away from the planet
                        Vector2 awayFromPlanet = safeLocation - planet.Position;
                        if (awayFromPlanet.LengthSquared() < 0.1f)
                        {
                            // If at the exact planet position, pick a random direction
                            Random rand = new Random();
                            float randomAngle = (float)(rand.NextDouble() * Math.PI * 2);
                            awayFromPlanet = new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle));
                        }
                        else
                        {
                            awayFromPlanet = Vector2.Normalize(awayFromPlanet);
                        }

                        safeLocation = planet.Position + awayFromPlanet * requiredDistance;
                        needsAdjustment = true;
                    }
                }
            }
        }

        return safeLocation;
    }

    /// <summary>
    /// Returns a random safe location outside the asteroid belt that is clear of stars and planets.
    /// The location will be between the asteroid belt outer radius and the system radius.
    /// </summary>
    /// <returns>A safe position outside the asteroid belt</returns>
    public Vector2 GetRandomSafeLocationOutsideAsteroidBelt()
    {
        // Try multiple times to find a safe location
        Random random = new Random();
        int maxAttempts = 20;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Generate random angle and distance
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float distance = AsteroidBeltOuterRadius + (float)(random.NextDouble() * (SystemRadius - AsteroidBeltOuterRadius));

            // Calculate position
            Vector2 candidatePosition = new Vector2(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance
            );

            // Use GetSafeLocation to ensure it's clear of celestial bodies
            Vector2 safePosition = GetSafeLocation(candidatePosition);

            // Check if the safe position is still in our desired range
            float safeDistance = safePosition.Length();
            if (safeDistance >= AsteroidBeltOuterRadius && safeDistance <= SystemRadius)
            {
                return safePosition;
            }
        }

        // Fallback: return a position at mid-range on a random angle
        float fallbackAngle = (float)(random.NextDouble() * Math.PI * 2);
        float fallbackDistance = (AsteroidBeltOuterRadius + SystemRadius) / 2f;
        return new Vector2(
            (float)Math.Cos(fallbackAngle) * fallbackDistance,
            (float)Math.Sin(fallbackAngle) * fallbackDistance
        );
    }
}

