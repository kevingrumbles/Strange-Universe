using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Systems;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>The player-controlled ship. Inherits shared ship functionality from Ship base class.</summary>
public class Player : Ship
{
    public string CurrentStarSystemID { get; set; }
    [JsonIgnore] public bool UseSafeEntryLocation { get; set; } = false;

    public Player(string shipName) : base(shipName)
    {
    }
    public void Update(float deltaTime, InputState input)
    {
        if (UseSafeEntryLocation)
        {
            Transform transform = StarSystem.GetSafeEntryTransform();
            this.Position = transform.Position;

            UseSafeEntryLocation = false;
        }

        HandleManualRotation(deltaTime, input);
        HandleRetrograde(deltaTime, input);
        HandleThrust(deltaTime, input);
        HandleJump(deltaTime, input);

        base.Update(deltaTime);
        Launcher.Camera.Update(Position, deltaTime, input);
    }

    // ── Rotation ─────────────────────────────────────────────────────────────
    // A/D rotate the ship facing immediately and responsively.
    // Facing is independent of the current velocity vector.

    private void HandleManualRotation(float deltaTime, InputState input)
    {
        if (ActiveNavTask != null) return;
        if (input.RotateLeft)  ApplyRotation(StaticHelpers.Direction.Left, deltaTime);
        if (input.RotateRight) ApplyRotation(StaticHelpers.Direction.Right, deltaTime);
    }

    // ── Maneuvering thrusters (S) ─────────────────────────────────────────────
    // Rotates the ship to face directly opposite the current velocity vector
    // so that W thrust will decelerate the ship.  A/D can combine with this.

    private void HandleRetrograde(float deltaTime, InputState input)
    {
        if (ActiveNavTask != null) return;
        if (!input.Retrograde || Velocity.LengthSquared() < 1f)
            return;

        RotateTowards((float)Math.Atan2(-Velocity.Y, -Velocity.X), deltaTime);
    }

    // ── Forward thrust (W) ────────────────────────────────────────────────────
    // Adds acceleration in the current facing direction using base Ship.ApplyThrust.

    private void HandleThrust(float deltaTime, InputState input)
    {
        if (ActiveNavTask != null) return;
        if (!input.Thrust) return;
        ApplyThrust(deltaTime);
    }

    // ── Forward thrust (J) ────────────────────────────────────────────────────
    // Adds acceleration in the current facing direction using base Ship.ApplyThrust.

    private void HandleJump(float deltaTime, InputState input)
    {
        if (ActiveNavTask != null) return;

        if (input.Jump && StarSystem.Universe.JumpRoute?.Count > 0 && CanJump())
        {
            if (CurrentFuelLevel > 0)
            {
                ActiveNavTask = new JumpTask(this, StarSystem.Universe.JumpRoute.FirstOrDefault());
            }
            else
            {
                Launcher.ActiveUniverse.ShowTimedMessage("Insufficient fuel for jump!");
            }
        }
    }
}

