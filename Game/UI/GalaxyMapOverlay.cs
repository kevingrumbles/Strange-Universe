using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using System.Collections.Generic;

namespace Strange_Universe.Game.UI;

/// <summary>
/// Top-level controller for the Galaxy Map overlay.
/// Owns visibility state, reads the mouse and keyboard (hover, building a route by clicking systems in sequence)
/// and hands the drawing to GalaxyMapRenderer. Exposes a simple Update / Draw interface to the playing screen.
/// </summary>
public class GalaxyMapOverlay
{
    private const int HitRadius = 14;   // pixel radius for node click / hover detection

    private readonly GalaxyMapRenderer _renderer;

    /// <summary>The SystemId the mouse is currently hovering over (null if none).</summary>
    private string _hoveredSystemId;

    /// <summary>Whether the map is currently visible.  While true, gameplay is paused.</summary>
    public bool IsOpen { get; private set; }

    // Last computed node screen positions - shared between Update and Draw each frame.
    private Dictionary<string, Vector2> _nodePositions = new();

    private KeyboardState _prevKeys;
    private MouseState    _prevMouse;

    // Accumulated time for animation (pulsing ring)
    private double _totalSeconds;

    public GalaxyMapOverlay(SpriteFont font, Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, Strange_Universe.Game.Systems.RenderService renderService)
    {
        _renderer = new GalaxyMapRenderer(font, graphicsDevice, renderService);
    }

    // -- Lifecycle ---------------------------------------------------------

    public void Open()
    {
        IsOpen = true;
        _hoveredSystemId = null;
        _prevKeys  = Keyboard.GetState();
        _prevMouse = Mouse.GetState();
    }

    public void Close()
    {
        IsOpen = false;
    }

    // -- Per-frame API -----------------------------------------------------

    /// <summary>
    /// Process UI input.  Call only while IsOpen == true.
    /// Returns true when the map should close.
    /// </summary>
    public bool Update(Universe universe, int screenW, int screenH, double deltaSeconds)
    {
        _totalSeconds += deltaSeconds;

        var keys  = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // Escape or M → close
        bool escPressed = keys.IsKeyDown(Keys.Escape) && !_prevKeys.IsKeyDown(Keys.Escape);
        bool mPressed   = keys.IsKeyDown(Keys.M)      && !_prevKeys.IsKeyDown(Keys.M);

        // Close button click
        bool closeClicked = false;
        var closeRect = _renderer.GetCloseButtonRect(screenW, screenH);
        if (_prevMouse.LeftButton == ButtonState.Pressed &&
            mouse.LeftButton      == ButtonState.Released &&
            closeRect.Contains(mouse.X, mouse.Y))
        {
            closeClicked = true;
        }

        // Clear route button click
        if (universe.JumpRoute.Count > 0)
        {
            var clearRouteRect = _renderer.GetClearRouteButtonRect(screenW, screenH);
            if (_prevMouse.LeftButton == ButtonState.Pressed &&
                mouse.LeftButton      == ButtonState.Released &&
                clearRouteRect.Contains(mouse.X, mouse.Y))
            {
                universe.JumpRoute.Clear();
            }
        }

        bool closing = escPressed || mPressed || closeClicked;
        if (!closing)
            UpdateRoute(universe, mouse); // reads the previous frame's mouse state, so it runs before that is replaced

        _prevKeys  = keys;
        _prevMouse = mouse;

        if (closing)
        {
            Close();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Updates the hover state and builds a multi-system route in <c>universe.JumpRoute</c>: a click on a reachable system adds it, a right click clears the route.
    /// Uses last frame's node positions (<see cref="_nodePositions"/>) for hit testing.
    /// </summary>
    private void UpdateRoute(Universe universe, MouseState mouse)
    {
        var mousePos = new Vector2(mouse.X, mouse.Y);
        string currentId = universe.ActiveStarSystem.SystemId;

        // The next system must be reachable from the last one in the route (or from the current system when the route is empty)
        string lastRouteSystem = universe.JumpRoute.Count > 0 ? universe.JumpRoute[^1] : currentId;
        var reachable = universe.Galaxy.FindById(lastRouteSystem)?.SystemConnectionIds ?? new HashSet<string>();

        // Hover: the closest reachable node within the hit radius (not the current system, not one already in the route)
        _hoveredSystemId = null;
        float closest = float.MaxValue;
        foreach (var (id, screenPos) in _nodePositions)
        {
            if (id == currentId || universe.JumpRoute.Contains(id) || !reachable.Contains(id)) continue;

            float dist = Vector2.Distance(mousePos, screenPos);
            if (dist <= HitRadius && dist < closest)
            {
                closest = dist;
                _hoveredSystemId = id;
            }
        }

        // Left button released over a hovered node: add it to the route
        if (_prevMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released && _hoveredSystemId != null)
            universe.JumpRoute.Add(_hoveredSystemId);

        // Right click anywhere: clear the whole route
        if (_prevMouse.RightButton == ButtonState.Pressed && mouse.RightButton == ButtonState.Released)
            universe.JumpRoute.Clear();
    }

    /// <summary>
    /// Render the overlay.  Call after the game world has been drawn.
    /// Stores node positions for the next Update call's hit-testing.
    /// </summary>
    public void Draw(Universe universe, int screenW, int screenH)
    {
        _nodePositions = _renderer.Draw(universe, screenW, screenH,
                                        _hoveredSystemId, _totalSeconds);
    }

    public void Dispose() 
    { 
        _renderer.Dispose();
    }
}
