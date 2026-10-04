using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
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


    public Nonplayer(string npcId, string shipType = "Shuttle", string npcName = "NPC", bool jumpSpawn = false) : base(npcName, shipType)
    {
        Id = npcId;
        if (jumpSpawn)
        {
            EnqueueNavTask(new JumpTask(this, currentState: TaskState.SystemTranslation));
        }
        else
        {
            EnqueueNavTask(new SpawnTask(this));
        }
    }

    /// <summary>Used by System.Text.Json when loading a saved universe.</summary>
    [JsonConstructor]
    public Nonplayer() : base()
    {
    }

    public new void Update(float deltaTime)
    { 
        base.Update(deltaTime);
    }
}
