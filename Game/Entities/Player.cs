using Strange_Universe.Game.Components;
using Strange_Universe.Game.NavSystem;
using StrangeUniverse;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.Entities;

/// <summary>The player-controlled ship. Inherits shared ship functionality from Ship base class.</summary>
public class Player : Ship
{
    public string CurrentStarSystemID { get; set; }

    public Player(string shipName) : base(shipName)
    {
    }
    public void Update(float deltaTime, InputState input)
    {
        HandleManualRotation(deltaTime, input);
        HandleRetrograde(deltaTime, input);
        HandleThrust(deltaTime, input);
        HandleJump(deltaTime, input);
        HandleTargeting(input);

        base.Update(deltaTime);
        Launcher.Camera.Update(Position, deltaTime, input);
    }

    // -- Rotation -------------------------------------------------------------
    // A/D rotate the ship facing immediately and responsively.
    // Facing is independent of the current velocity vector.

    private void HandleManualRotation(float deltaTime, InputState input)
    {
        if (HasActiveNavTask) return;
        if (input.RotateLeft)  ApplyRotation(StaticHelpers.Direction.Left, deltaTime);
        if (input.RotateRight) ApplyRotation(StaticHelpers.Direction.Right, deltaTime);
    }

    // -- Maneuvering thrusters (S) ---------------------------------------------
    // Rotates the ship to face directly opposite the current velocity vector
    // so that W thrust will decelerate the ship.  A/D can combine with this.

    private void HandleRetrograde(float deltaTime, InputState input)
    {
        if (HasActiveNavTask) return;
        if (!input.Retrograde || Velocity.LengthSquared() < 1f)
            return;

        RotateTowards((float)Math.Atan2(-Velocity.Y, -Velocity.X), deltaTime);
    }

    // -- Forward thrust (W) ----------------------------------------------------
    // Adds acceleration in the current facing direction using base Ship.ApplyThrust.

    private void HandleThrust(float deltaTime, InputState input)
    {
        if (HasActiveNavTask) return;
        if (!input.Thrust) return;
        ApplyThrust(deltaTime);
    }

    // -- Forward thrust (J) ----------------------------------------------------
    // Adds acceleration in the current facing direction using base Ship.ApplyThrust.

    private void HandleJump(float deltaTime, InputState input)
    {
        if (HasActiveNavTask) return;

        if (input.Jump && StarSystem.Universe.JumpRoute?.Count > 0 && CanJump())
        {
            if (CurrentFuelLevel > 0)
            {
                EnqueueNavTask(new JumpTask(this,StarSystem.GalaxyPosition, StarSystem.Universe.JumpRoute.FirstOrDefault()));
            }
            else
            {
                Launcher.ActiveUniverse.ShowTimedMessage("Insufficient fuel for jump!");
            }
        }
    }

    // -- Targeting (R / ~) ---------------------------------------------------------
    // R: Select nearest ship
    // ~: Cycle through ships in system

    private void HandleTargeting(InputState input)
    {
        if (input.TargetNearest)
        {
            SetTarget(GetNearestTarget());
        }

        if (input.CycleTarget)
        {
            CycleToNextTarget();
        }
    }

    private void CycleToNextTarget()
    {
        if (StarSystem == null)
            return;

        // Get all ships except self
        var allShips = new List<Ship>();
        allShips.AddRange(StarSystem.Npcs);
        if (StarSystem.ActivePlayer != null && StarSystem.ActivePlayer != this)
        {
            allShips.Add(StarSystem.ActivePlayer);
        }

        if (allShips.Count == 0)
        {
            ClearTarget();
            return;
        }

        // Sort by stable ID for consistent ordering
        allShips = allShips.OrderBy(s => s.Id).ToList();

        // Find current target in list
        int currentIndex = -1;
        if (Target != null)
        {
            currentIndex = allShips.IndexOf(Target);
        }

        // Select next ship (wrap around)
        int nextIndex = (currentIndex + 1) % allShips.Count;
        SetTarget(allShips[nextIndex]);
    }
}

