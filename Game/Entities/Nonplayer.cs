using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Nonplayer-controlled ship. Inherits shared ship functionality from Ship base class.
/// AI behavior is managed internally through a behavior strategy and activities.
/// </summary>
public class Nonplayer : Ship
{
    /// <summary>
    /// Marks this NPC for removal from the system (e.g., after jumping out).
    /// </summary>
    [JsonIgnore] public bool Remove { get; set; }

    public Nonplayer(string npcId, string shipName, string npcName = "NPC") : base(shipName)
    {
        Id = npcId;
        Name = npcName;
    }

    /// <summary>
    /// Updates the NPC ship using the AI pipeline:
    /// Behavior logic -> Ship autopilot -> Ship physics
    /// </summary>
    public new void Update(float deltaTime)
    { 
        base.Update(deltaTime);
    }
}
