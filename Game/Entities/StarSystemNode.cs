using Microsoft.Xna.Framework;
using System.Collections.Generic;
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

    public StarSystemNode() { }
    public StarSystemNode(string seed, Vector2 position, StarSystemNode backConnection = null, IReadOnlyList<StarSystemNode> existingNodes = null)
    {
        Name = NameGenerator.GetStarSystemName(seed, existingNodes);
        SystemId = $"{seed}_{Name}";
        GalaxyPosition = position;

        if (backConnection != null)
        {
            SystemConnectionIds.Add(backConnection.SystemId);
        }
    }
}
