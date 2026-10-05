using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// The galaxy-level graph of star system nodes: lookup by id/position and
/// procedural generation of jump connections.
/// Wraps the persisted node list; indices pick up nodes appended to the list directly.
/// </summary>
public class GalaxyGraph
{
    private readonly Dictionary<string, StarSystemNode> _byId = new();
    private readonly Dictionary<Vector2, StarSystemNode> _byPosition = new();
    private int _indexedCount;

    public GalaxyGraph(List<StarSystemNode> nodes)
    {
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
    }

    /// <summary>The underlying (persisted) node list.</summary>
    public List<StarSystemNode> Nodes { get; }

    public StarSystemNode FindById(string systemId)
    {
        if (systemId == null) return null;
        EnsureIndexed();
        return _byId.TryGetValue(systemId, out var node) ? node : null;
    }

    public StarSystemNode FindByPosition(Vector2 position)
    {
        EnsureIndexed();
        return _byPosition.TryGetValue(position, out var node) ? node : null;
    }

    public void Add(StarSystemNode node)
    {
        Nodes.Add(node);
        EnsureIndexed();
    }

    private void EnsureIndexed()
    {
        if (_indexedCount > Nodes.Count)
        {
            _byId.Clear();
            _byPosition.Clear();
            _indexedCount = 0;
        }

        for (; _indexedCount < Nodes.Count; _indexedCount++)
        {
            var n = Nodes[_indexedCount];
            // First occurrence wins, matching the previous FirstOrDefault scans.
            if (n.SystemId != null) _byId.TryAdd(n.SystemId, n);
            _byPosition.TryAdd(n.GalaxyPosition, n);
        }
    }

    /// <summary>
    /// Adds connections from <paramref name="node"/> until it has <paramref name="targetConnectionCount"/>,
    /// linking to nearby existing systems or creating new ones. Deterministic for a given node id.
    /// </summary>
    public void GenerateConnections(StarSystemNode node, int targetConnectionCount, string seed)
    {
        Random rng = new Random(ProceduralHelpers.SeedHash($"{node.SystemId}_Connections"));

        List<Point> directions = ProceduralHelpers.GalaxyConnectionPreferredDirections.ToList();

        // Remove directions already occupied by existing connections.
        foreach (string id in node.SystemConnectionIds)
        {
            StarSystemNode connected = FindById(id);
            if (connected == null)
                continue;

            Point dir = ProceduralHelpers.NormalizeDirection(
                (int)(connected.GalaxyPosition.X - node.GalaxyPosition.X),
                (int)(connected.GalaxyPosition.Y - node.GalaxyPosition.Y));

            directions.Remove(dir);
        }

        // Deterministic shuffle.
        for (int i = directions.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (directions[i], directions[j]) = (directions[j], directions[i]);
        }

        double existingConnectionChance = ExistingConnectionChance(node);

        while (node.SystemConnectionIds.Count < targetConnectionCount &&
               directions.Count > 0)
        {
            // Prefer connecting to an existing nearby system.
            var nearbySystems = UnconnectedNeighbors(node);

            if (nearbySystems.Count > 0 &&
                rng.NextDouble() < existingConnectionChance)
            {
                // Favor the closest few systems.
                int candidateCount = Math.Min(3, nearbySystems.Count);
                StarSystemNode existing = nearbySystems[rng.Next(candidateCount)];
                Connect(node, existing);
                continue;
            }

            // Otherwise create a new system.
            Point dir = directions[0];
            directions.RemoveAt(0);

            foreach (int distance in new[] { rng.Next(2, 4), rng.Next(4, 7), rng.Next(7, 10) })
            {
                Vector2 location = node.GalaxyPosition + new Vector2(dir.X * distance, dir.Y * distance);

                StarSystemNode target = FindByPosition(location);
                if (target == null)
                {
                    target = new StarSystemNode(seed, location, node, Nodes);
                    Add(target);
                }

                if (!node.SystemConnectionIds.Contains(target.SystemId))
                {
                    Connect(node, target);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// How "developed" the region around a node is:
    /// 0 nearby systems -> 5% chance, 40 nearby systems -> 85% chance.
    /// </summary>
    private double ExistingConnectionChance(StarSystemNode node)
    {
        const float LocalDensityRadius = 30f;

        int nearbySystemCount = Nodes.Count(n =>
            n.SystemId != node.SystemId &&
            Vector2.Distance(node.GalaxyPosition, n.GalaxyPosition) <= LocalDensityRadius);

        return Math.Clamp(0.05 + (nearbySystemCount / 40.0) * 0.80, 0.05, 0.85);
    }

    private List<StarSystemNode> UnconnectedNeighbors(StarSystemNode node)
    {
        const float MaxConnectionDistance = 8f;

        return Nodes
            .Where(n =>
                n.SystemId != node.SystemId &&
                !node.SystemConnectionIds.Contains(n.SystemId) &&
                Vector2.Distance(node.GalaxyPosition, n.GalaxyPosition) <= MaxConnectionDistance)
            .OrderBy(n => Vector2.Distance(node.GalaxyPosition, n.GalaxyPosition))
            .ToList();
    }

    private static void Connect(StarSystemNode a, StarSystemNode b)
    {
        a.SystemConnectionIds.Add(b.SystemId);
        b.SystemConnectionIds.Add(a.SystemId);
    }
}
