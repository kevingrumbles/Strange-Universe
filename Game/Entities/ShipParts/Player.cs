using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.NavSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>The player-controlled ship. Inherits shared ship functionality from Ship base class.</summary>
public class Player : Ship
{
    public string CurrentStarSystemID { get; set; }

    private bool _located;

    /// <summary>The player's position in the system. Assigning it (the spawn does) marks the player as placed.</summary>
    [JsonIgnore]
    public override Vector2 Position
    {
        get => base.Position;
        set { base.Position = value; _located = true; }
    }

    /// <summary>
    /// Where the player is, as saved (the file calls it <c>Position</c>, as before). <b>Null until the player has been placed:</b>
    /// a new universe creates its player with no location, and entering a universe whose player has none spawns it
    /// (see <see cref="SpawnTask"/>). Saves from before this was nullable always have a position.
    /// </summary>
    [JsonPropertyName("Position")]
    public Vector2? Location
    {
        get => _located ? Position : null;
        set
        {
            if (value is Vector2 position) Position = position;
            else _located = false;
        }
    }

    public Player(string name, ShipStats shipType) : base(name, shipType)
    {

    }

    /// <summary>Used by System.Text.Json when loading a saved universe.</summary>
    [JsonConstructor]
    public Player() : base()
    {
    }
    public void Update(float deltaTime, InputState input)
    {
        HandleManualRotation(deltaTime, input);
        HandleRetrograde(deltaTime, input);
        HandleThrust(deltaTime, input);
        HandleJump(deltaTime, input);
        HandleDock(input);
        HandleTargeting(input);
        HandleFirePrimaryWeapons(input);

        Update(deltaTime);
        CameraTarget = Position;
    }

    /// <summary>
    /// Player position captured immediately after the player's own physics step, before
    /// collisions resolve. The camera follows this so framing matches the original behaviour.
    /// </summary>
    [JsonIgnore] public Vector2 CameraTarget { get; private set; }

    private void HandleFirePrimaryWeapons(InputState input)
    {
        if (input.Fire)
        {
            FireWeapons();
        }
    }

    // -- Rotation -------------------------------------------------------------
    // A/D rotate the ship facing immediately and responsively.
    // Facing is independent of the current velocity vector.

    private void HandleManualRotation(float deltaTime, InputState input)
    {
        if (HasActiveNavTask) return;
        if (input.RotateLeft)  ApplyRotation(Direction.Left, deltaTime);
        if (input.RotateRight) ApplyRotation(Direction.Right, deltaTime);
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
        if (HasActiveNavTask || StarSystem == null) return;

        if (input.Jump && StarSystem.Universe.JumpRoute?.Count > 0 && CanJump())
        {
            if (CurrentFuelLevel > 0)
            {
                EnqueueNavTask(new JumpTask(this,StarSystem.GalaxyPosition, StarSystem.Universe.JumpRoute.FirstOrDefault()));
            }
            else
            {
                StarSystem.Universe.Messages.Post("Insufficient fuel for jump!");
            }
        }
    }

    // -- Docking (L) ---------------------------------------------------------------
    // L with no spaceport targeted targets the nearest one in the system. L with one targeted starts the docking process:
    // the planet decides whether the ship can dock now (Planet.CheckDocking) and the ship docks, or is told why not.

    private Planet _dockTarget;

    /// <summary>The planet with a spaceport the player has targeted for docking; null when none (or when it is not in the player's current system). Not saved.</summary>
    [JsonIgnore]
    public Planet DockTarget =>
        _dockTarget != null && StarSystem != null && StarSystem.Planets.Contains(_dockTarget) ? _dockTarget : null;

    public void ClearDockTarget() => _dockTarget = null;

    private void HandleDock(InputState input)
    {
        if (!input.Dock || HasActiveNavTask || StarSystem == null) return;

        var messages = StarSystem.Universe.Messages;
        var target = DockTarget;

        if (target == null)
        {
            _dockTarget = StarSystem.NearestSpaceport(Position);
            messages.Post(_dockTarget != null ? $"Targeted {_dockTarget.Name}" : "No spaceport in this system");
            return;
        }

        switch (target.CheckDocking(this))
        {
            case DockingCheck.Ok:
                StarSystem.Universe.Dock(target);
                break;
            case DockingCheck.TooFast:
                messages.Post("Too fast to dock");
                break;
            default:
                messages.Post($"{target.Name} is out of docking range");
                break;
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

        // There is one target at a time: targeting a ship replaces the spaceport targeted for docking.
        if ((input.TargetNearest || input.CycleTarget) && Target != null)
            ClearDockTarget();
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

