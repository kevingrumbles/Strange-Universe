using Strange_Universe.Game.Components;
using StrangeUniverse;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// A generated universe. Owns all <see cref="StarSystem"/>s and drives the
/// update of whichever one is currently active.
/// Persisted to <c>universe-settings.json</c> with only identity fields.
/// </summary>
public class Universe
{
    public Guid   Id   { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Universe";
    public Player Player { get; set; }
    public string Seed { get; set; }

    private StarSystem _activeStarSystem;
    [JsonIgnore]
    public StarSystem ActiveStarSystem
    {
        get
        {
            if (_activeStarSystem == null)
            {
                if (StarSystemNodes.Count == 0)
                {
                    // If there are no star systems nodes, create a default one and add it to the universe.
                    StarSystemNode defaultNode = new StarSystemNode(this);
                    StarSystemNodes.Add(defaultNode);
                }
                StarSystemNode currentSystemNode = StarSystemNodes.FirstOrDefault(s => s.SystemId == Player.CurrentStarSystemID);
                if (currentSystemNode == null)
                {
                    // If the player's current star system ID is not found, return the first star system as a fallback.
                    currentSystemNode = StarSystemNodes.FirstOrDefault();
                    Player.CurrentStarSystemID = currentSystemNode.SystemId; // Update player's current star system ID
                }
                _activeStarSystem = new StarSystem(currentSystemNode);
            }
            return _activeStarSystem;
        }
    }

    public List<StarSystemNode> StarSystemNodes { get; set; } = new();

    public Universe() { }
    public Universe(string name, string seed = null)
    {
        Guid id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(name)) name = "New Universe";

        Id = id;
        Name = name;
        Seed = seed ?? id.ToString();
        Player = new Player();
    }

    public void Update(float deltaTime, InputState input) =>
        ActiveStarSystem.Update(Player, deltaTime, input);

    public void Generate()
    {
        ClearRuntime();
        Player.Generate();
    }

    public void ClearRuntime()
    {
        _activeStarSystem = null;
    }

    public string GetStarSystemName()
    {
        Random universeRng = new Random(StaticHelpers.SeedHash(Seed));
        string name = "Sol";
        while (name is null || StarSystemNodes.Contains(StarSystemNodes.Find(s => s.Name == name)))
        {
            name = StaticHelpers.StarSystemNames[universeRng.Next(StaticHelpers.StarSystemNames.Length)];
        }
        return name;
    }
}
