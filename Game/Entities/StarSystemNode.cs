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

    /// <summary>Name of the starting system; its layout is hand-tuned.</summary>
    public const string HomeName = "Sol";

    /// <summary>True for the starting system. Derived from the name, so it is not serialized.</summary>
    [JsonIgnore] public bool IsHome => Name == HomeName;
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
