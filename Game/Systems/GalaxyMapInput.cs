using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Handles mouse hit-detection and selection logic for the Galaxy Map.
/// Supports building a multi-system route by clicking systems in sequence.
/// </summary>
public class GalaxyMapInput
{
    private const int HitRadius = 14;   // pixel radius for node click / hover detection

    private MouseState _prevMouse;

    /// <summary>The SystemId the mouse is currently hovering over (null if none).</summary>
    public string HoveredSystemId { get; private set; }

    /// <summary>
    /// Process one frame of mouse input.
    /// Updates hover state and builds a multi-system route in universe.JumpRoute on click.
    /// </summary>
    /// <param name="universe">The active universe.</param>
    /// <param name="nodeScreenPositions">Screen-space centre of each node, keyed by SystemId.</param>
    public void Update(Universe universe, Dictionary<string, Vector2> nodeScreenPositions)
    {
        var mouse = Mouse.GetState();
        var mousePos = new Vector2(mouse.X, mouse.Y);

        string currentId = universe.ActiveStarSystem.SystemId;

        // Determine which systems are reachable for the next selection
        // Start from current system if route is empty, otherwise from the last system in route
        string lastRouteSystem = universe.JumpRoute.Count > 0 
            ? universe.JumpRoute[^1] 
            : currentId;

        var lastNode = universe.StarSystemNodes.FirstOrDefault(n => n.SystemId == lastRouteSystem);
        var reachableConnections = lastNode?.SystemConnectionIds ?? new HashSet<string>();

        // Hover: find the closest reachable node within hit radius
        HoveredSystemId = null;
        float closest = float.MaxValue;
        foreach (var (id, screenPos) in nodeScreenPositions)
        {
            // Can't hover over current system or systems already in the route
            if (id == currentId) continue;
            if (universe.JumpRoute.Contains(id)) continue;

            // Must be reachable from the last system in the route (or current if route is empty)
            if (!reachableConnections.Contains(id)) continue;

            float dist = Vector2.Distance(mousePos, screenPos);
            if (dist <= HitRadius && dist < closest)
            {
                closest = dist;
                HoveredSystemId = id;
            }
        }

        // Click: left button just released over a hovered node
        bool justReleased = _prevMouse.LeftButton == ButtonState.Pressed &&
                            mouse.LeftButton     == ButtonState.Released;

        if (justReleased && HoveredSystemId != null)
        {
            // Add to route
            universe.JumpRoute.Add(HoveredSystemId);
        }

        // Right-click anywhere: clear the entire route
        bool rightClicked = _prevMouse.RightButton == ButtonState.Pressed &&
                           mouse.RightButton == ButtonState.Released;

        if (rightClicked)
        {
            universe.JumpRoute.Clear();
        }

        _prevMouse = mouse;
    }

    public void Reset()
    {
        HoveredSystemId = null;
        _prevMouse = default;
    }
}
