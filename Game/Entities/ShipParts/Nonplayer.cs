using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.NavSystem;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Strange_Universe.Game.Components.ShipComponents;

namespace Strange_Universe.Game.Entities.ShipParts;

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


    /// <param name="shipType">The ship type, which gives the ship its default loadout. A new Shuttle when not given.</param>
    public Nonplayer(string npcId, ShipStats shipType = null, string npcName = "NPC", bool jumpSpawn = false) : base(npcName, shipType ?? new Shuttle())
    {
        Id = npcId;
        Credits = RollCredits(npcId, ShipType.TypicalCredits);
        if (jumpSpawn)
        {
            EnqueueNavTask(new JumpTask(this, startPhase: JumpTask.Phase.SystemTranslation));
        }
        else
        {
            EnqueueNavTask(new SpawnTask(this));
        }
    }

    /// <summary>
    /// An NPC's credits: between half and one and a half times the ship type's typical amount, rolled from its id so the same NPC always has the
    /// same amount.
    /// </summary>
    public static int RollCredits(string npcId, int typicalCredits)
    {
        var rng = new Random(ProceduralHelpers.SeedHash($"{npcId}_credits"));
        return (int)Math.Round(typicalCredits * (0.5 + rng.NextDouble()));
    }

    /// <summary>Used by System.Text.Json when loading a saved universe.</summary>
    [JsonConstructor]
    public Nonplayer() : base()
    {
    }

    /// <summary>Flags the attacker as hostile and puts the navigator on alert (see <see cref="AlertTask"/>).</summary>
    protected override void OnAttacked(Ship attacker)
    {
        if (attacker == null || ReferenceEquals(attacker, this)) return;

        MarkHostile(attacker);
        Navigator.RaiseAlert();
    }

    public new void Update(float deltaTime)
    { 
        base.Update(deltaTime);
    }
}
