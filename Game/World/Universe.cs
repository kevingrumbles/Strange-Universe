using Strange_Universe.Game.World;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace StrangeUniverse.Game.World;

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


    [JsonIgnore]
    public StarSystem ActiveStarSystem
    {
        get
        {
            StarSystem currentSystem = StarSystems.FirstOrDefault(s => s.SystemId == Player.CurrentStarSystemID);
            if (currentSystem == null)
            {
                if (StarSystems.Count == 0)
                {
                    // If there are no star systems, create a default one and add it to the universe.
                    StarSystems.Add(new StarSystem(parentUniverse: this));
                }

                // If the player's current star system ID is not found, return the first star system as a fallback.
                currentSystem = StarSystems.FirstOrDefault();
                Player.CurrentStarSystemID = currentSystem?.SystemId ?? String.Empty; // Update player's current star system ID
            }
            return currentSystem;
        }
    }

    public List<StarSystem> StarSystems { get; set; } = new();

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
        Player.Generate();
        ActiveStarSystem.Generate(this);
    }

    public string GetStarSystemName()
    {
        Random universeRng = new Random(StaticHelpers.SeedHash(Seed));
        string name = "Sol";
        while (name is null || StarSystems.Contains(StarSystems.Find(s => s.Name == name)))
        {
            name = StaticHelpers.StarSystemNames[universeRng.Next(StaticHelpers.StarSystemNames.Length)];
        }
        return name;
    }
}
