using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.UI;

/// <summary>
/// Handles mouse hit-detection and selection logic for the Galaxy Map.
/// All methods are pure (no side effects beyond updating SelectedJumpTargetSystemId).
/// </summary>
public class GalaxyMapInput
{
    private const int HitRadius = 14;   // pixel radius for node click / hover detection

    private MouseState _prevMouse;

    /// <summary>The SystemId the mouse is currently hovering over (null if none).</summary>
    public string HoveredSystemId { get; private set; }

    /// <summary>
    /// Process one frame of mouse input.
    /// Updates hover state and writes to universe.SelectedJumpTargetSystemId on click.
    /// </summary>
    /// <param name="universe">The active universe.</param>
    /// <param name="nodeScreenPositions">Screen-space centre of each node, keyed by SystemId.</param>
    public void Update(Universe universe, Dictionary<string, Vector2> nodeScreenPositions)
    {
        var mouse = Mouse.GetState();
        var mousePos = new Vector2(mouse.X, mouse.Y);

        string currentId  = universe.ActiveStarSystem.Node.SystemId;
        var    connections = universe.ActiveStarSystem.Node.SystemConnectionIds;

        // Hover: find the closest reachable node within hit radius
        HoveredSystemId = null;
        float closest = float.MaxValue;
        foreach (var (id, screenPos) in nodeScreenPositions)
        {
            if (id == currentId) continue;
            if (!connections.Contains(id)) continue;

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
            // Toggle off if the same node is clicked again
            universe.SelectedJumpTargetSystemId =
                universe.SelectedJumpTargetSystemId == HoveredSystemId
                    ? null
                    : HoveredSystemId;
        }

        _prevMouse = mouse;
    }

    public void Reset()
    {
        HoveredSystemId = null;
        _prevMouse = default;
    }
}
