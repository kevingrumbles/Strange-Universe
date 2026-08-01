using Strange_Universe.Game.Entities;
using System;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Simple AI controller for NPC ships.
/// Provides basic autonomous movement: drift through space with occasional heading changes.
/// </summary>
public static class NpcController
{
    private static readonly Random _random = new Random();

    // AI tuning constants
    private const float MinDecisionInterval = 3f;   // minimum seconds between AI decisions
    private const float MaxDecisionInterval = 8f;   // maximum seconds between AI decisions
    private const float ThrustProbability = 0.6f;   // 60% chance to thrust after heading change
    private const float ThrustDuration = 2f;        // seconds to thrust when decided

    /// <summary>
    /// Updates the AI for a single NPC ship.
    /// Makes periodic decisions about heading and thrust.
    /// </summary>
    public static void UpdateAI(NPC npc, float deltaTime)
    {
        npc.AiTimer += deltaTime;

        // Periodic AI decision making
        float nextDecisionTime = MinDecisionInterval + 
            (float)(_random.NextDouble() * (MaxDecisionInterval - MinDecisionInterval));

        if (npc.AiTimer >= nextDecisionTime)
        {
            // Make a new decision
            MakeDecision(npc);
            npc.AiTimer = 0f;
        }

        // Stop thrusting after duration expires
        if (npc.IsThrusting && npc.AiTimer > ThrustDuration)
        {
            npc.IsThrusting = false;
        }
    }

    private static void MakeDecision(NPC npc)
    {
        // Choose a new random heading
        npc.CurrentTargetHeading = (float)(_random.NextDouble() * Math.PI * 2);

        // Decide whether to thrust
        npc.IsThrusting = _random.NextDouble() < ThrustProbability;
    }
}
