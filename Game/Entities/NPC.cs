using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// NPC-controlled ship. Inherits shared ship functionality from Ship base class.
/// AI behavior is managed externally by NpcController.
/// </summary>
public class NPC : Ship
{
    public string NpcId { get; set; }
    public string NpcName { get; set; }

    // AI state (managed by NpcController)
    [JsonIgnore] public float AiTimer { get; set; }
    [JsonIgnore] public float CurrentTargetHeading { get; set; }
    [JsonIgnore] public bool IsThrusting { get; set; }

    public NPC(string npcId, string npcName = "NPC", string shipName = "Shuttle") : base(shipName)
    {
        NpcId = npcId;
        NpcName = npcName;
        AiTimer = 0f;
        CurrentTargetHeading = 0f;
        IsThrusting = false;
    }

    /// <summary>
    /// Updates the NPC ship. AI decisions are made by NpcController before calling this.
    /// </summary>
    public void Update(float deltaTime, StarSystem system)
    {
        // AI controller will set IsThrusting and CurrentTargetHeading
        // before this is called

        // Rotate toward target heading
        if (CurrentTargetHeading != Transform.Rotation)
        {
            RotateTowards(CurrentTargetHeading, deltaTime);
        }

        // Apply thrust if AI decided to
        if (IsThrusting)
        {
            ApplyThrust(deltaTime);
        }

        // Apply physics (gravity + integration)
        UpdatePhysics(deltaTime, system);
    }
}
