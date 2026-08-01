using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
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
    // ── Identity / seed ──────────────────────────────────────────────────────
    /// <summary>Deterministic seed derived from the parent Universe seed.</summary>
    public StarSystemNode Node { get; set; }

    // ── Generation configuration ─────────────────────────────────────────────
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

    // ── Entities ─────────────────────────────────────────────────────────────
    [JsonIgnore] public List<Star>           Stars            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public List<NPC>            NPCs            { get; }      = new();
    [JsonIgnore] public Player ActivePlayer { get { return Launcher.ActiveUniverse.Player;  }  }
    [JsonIgnore] public string        NebulaId        { get; private set; }

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public StarSystem() { }
    public StarSystem(StarSystemNode node)
    {
        var sw = Stopwatch.StartNew();
        Debug.WriteLine($"StarSystem initialization started: {sw.ElapsedMilliseconds} ms");
        Node = node;
        Node.Discovered = true;

        Random systemRng = new Random(StaticHelpers.SeedHash(Node.SystemId));
        PlanetCount = systemRng.Next(0, 7);
        AsteroidCount = systemRng.Next(0, 120);
        StarCount = systemRng.Next(1, 3);
        StarRadius = systemRng.NextWeightedFloat(250f, 500f);
        switch (StarCount)
        {
            case 1: 
                StarOrbitRadius = 0f;  
                StarOrbitSpeed = 0f;
                break;
            case 2: 
                StarOrbitRadius = systemRng.NextWeightedFloat(2000f, 4000f);
                StarOrbitSpeed  = systemRng.NextWeightedFloat(0.0001f, 0.0003f);
                break;
            case 3: 
                StarOrbitRadius = systemRng.NextWeightedFloat(3000f, 7000f);
                StarOrbitSpeed  = systemRng.NextWeightedFloat(0.00005f, 0.0002f);
                break;
        }
        
        MinPlanetRadius = systemRng.NextWeightedFloat(90f, 120f);
        MaxPlanetRadius = systemRng.NextWeightedFloat(150, 300f);
        MinAsteroidRadius = systemRng.NextWeightedFloat(15f, 30f);
        MaxAsteroidRadius = systemRng.NextWeightedFloat(40f, 50f);
        SystemConnectionCount = systemRng.Next(1, 4);
        if (SystemConnectionCount == 1) SystemConnectionCount = systemRng.Next(1, 4);
        InnerPlanetCount =systemRng.Next(PlanetCount);
        BackgroundStarCount = systemRng.Next(400, 800);

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
        SystemRadius = systemRng.NextWeightedFloat(12000f + starCoreFootprint, 25000f + starCoreFootprint);
        MandevilleRadius = SystemRadius * 0.75f;

        AsteroidBeltInnerRadius = Math.Max(
            systemRng.NextWeightedFloat(SystemRadius * 0.2f, SystemRadius * 0.4f),
            starCoreRadius * 2.5f);
        AsteroidBeltOuterRadius = systemRng.NextWeightedFloat(AsteroidBeltInnerRadius * 1.2f, SystemRadius * 0.6f);
        Debug.WriteLine($"properties loaded: {sw.ElapsedMilliseconds} ms");
        Debug.WriteLine($"Generating Stars: {sw.ElapsedMilliseconds} ms");
        GenerateStars();
        Debug.WriteLine($"Generating Planets: {sw.ElapsedMilliseconds} ms");
        GeneratePlanets();
        Debug.WriteLine($"Generating Asteroids: {sw.ElapsedMilliseconds} ms");
        GenerateAsteroids();
        Debug.WriteLine($"Generating Background Stars: {sw.ElapsedMilliseconds} ms");
        GenerateBackgroundStars();
        Debug.WriteLine($"Generating NPCs: {sw.ElapsedMilliseconds} ms");
        GenerateNPCs();
        Debug.WriteLine($"Generating Nebula: {sw.ElapsedMilliseconds} ms");
        GenerateNebula();
        Debug.WriteLine($"Generating Connections: {sw.ElapsedMilliseconds} ms");
        GenerateConnections();
        Debug.WriteLine($"StarSystem initialization completed: {sw.ElapsedMilliseconds} ms");
    }

    public void Update(float deltaTime, InputState input)
    {
        ActivePlayer.Update(deltaTime, input);

        // Update NPCs
        foreach (var npc in NPCs)
        {
            NpcController.UpdateAI(npc, deltaTime);
            npc.Update(deltaTime, this);
        }

        UpdateStarOrbits(deltaTime);
        _physics.Update(Asteroids, deltaTime);

        // Collision detection (skip while player is jumping)
        if (ActivePlayer.JumpPhase == JumpPhase.Normal)
        {
            // Player collisions with static objects
            _collision.Resolve(ActivePlayer, Planets, Asteroids);

            // NPC collisions with static objects
            foreach (var npc in NPCs)
            {
                _collision.ResolveNPC(npc, Planets, Asteroids);
            }

            // Ship-to-ship collisions (player vs NPCs and NPC vs NPC)
            _collision.ResolveShipToShip(ActivePlayer, NPCs);
        }
    }

    private void UpdateStarOrbits(float deltaTime)
    {
        if (Stars.Count <= 1) return;
        foreach (var star in Stars)
        {
            star.OrbitAngle += star.OrbitSpeed * deltaTime;
            star.Transform.Position = new Vector2(
                MathF.Cos(star.OrbitAngle) * star.OrbitRadius,
                MathF.Sin(star.OrbitAngle) * star.OrbitRadius);

            // Update gravity well center as star moves
            star.GravityWell.Center = star.Transform.Position;
        }
    }

    private void GenerateConnections()
    {
        Random rng = new Random(StaticHelpers.SeedHash($"{Node.SystemId}_Connections"));

        List<Point> directions = ProceduralHelpers.GalaxyConnectionPreferredDirections.ToList();

        // Remove directions already occupied by existing connections.
        foreach (string id in Node.SystemConnectionIds)
        {
            StarSystemNode? connected =
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

                StarSystemNode? node = Node.Universe.StarSystemNodes
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
            newStar.Transform.Position = StarCount == 1
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

        // Positions are in a virtual 2048×2048 space used only for parallax scrolling
        const float VirtualSize = 2048f;
        for (int i = 0; i < BackgroundStarCount; i++)
        {
            BackgroundStars.Add(new BackgroundStar
            {
                Position   = new Vector2(
                    (float)backgroundStarsRng.NextDouble() * VirtualSize,
                    (float)backgroundStarsRng.NextDouble() * VirtualSize),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)backgroundStarsRng.NextDouble()),
                Size       = backgroundStarsRng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 0) the nebula
                Layer      = backgroundStarsRng.NextDouble() < 0.5 ? 1 : 0,
            });
        }
    }

    private void GenerateNPCs()
    {
        Random npcRng = new Random(StaticHelpers.SeedHash($"{Node.SystemId}_NPCs"));

        // Generate a small number of NPCs (2-5 per system)
        int npcCount = npcRng.Next(25, 35);

        for (int i = 0; i < npcCount; i++)
        {
            string npcId = $"{Node.SystemId}_NPC_{i}";
            string npcName = $"Trader-{i + 1}";

            var npc = new NPC(npcId, npcName, "Shuttle");

            // Spawn at random position within inner system (not too far out)
            float spawnDistance = npcRng.NextWeightedFloat(MandevilleRadius * 1.2f, SystemRadius * 0.5f);
            float spawnAngle = (float)(npcRng.NextDouble() * Math.PI * 2);

            npc.Transform.Position = new Vector2(
                (float)Math.Cos(spawnAngle) * spawnDistance,
                (float)Math.Sin(spawnAngle) * spawnDistance);

            // Random initial heading
            npc.Transform.Rotation = (float)(npcRng.NextDouble() * Math.PI * 2);

            // Random initial velocity (drifting)
            float initialSpeed = npcRng.NextWeightedFloat(50f, 200f);
            float velocityAngle = (float)(npcRng.NextDouble() * Math.PI * 2);
            npc.Physics.Velocity = new Vector2(
                (float)Math.Cos(velocityAngle) * initialSpeed,
                (float)Math.Sin(velocityAngle) * initialSpeed);

            // Random AI timer offset so they don't all make decisions simultaneously
            npc.AiTimer = (float)(npcRng.NextDouble() * 5f);

            npc.Generate();
            NPCs.Add(npc);
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
}

