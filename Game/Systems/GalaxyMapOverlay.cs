using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using System.Collections.Generic;

namespace Strange_Universe.Game.UI;

/// <summary>
/// Top-level controller for the Galaxy Map overlay.
/// Owns visibility state, coordinates GalaxyMapInput and GalaxyMapRenderer,
/// and exposes a simple Update / Draw interface to Launcher.
/// </summary>
public class GalaxyMapOverlay
{
    private readonly GalaxyMapRenderer _renderer;
    private readonly GalaxyMapInput    _input;

    /// <summary>Whether the map is currently visible.  While true, gameplay is paused.</summary>
    public bool IsOpen { get; private set; }

    // Last computed node screen positions - shared between Update and Draw each frame.
    private Dictionary<string, Vector2> _nodePositions = new();

    private KeyboardState _prevKeys;
    private MouseState    _prevMouse;

    // Accumulated time for animation (pulsing ring)
    private double _totalSeconds;

    public GalaxyMapOverlay(SpriteBatch spriteBatch, GraphicsDevice gd, SpriteFont font)
    {
        _renderer = new GalaxyMapRenderer(spriteBatch, gd, font);
        _input    = new GalaxyMapInput();
    }

    // -- Lifecycle ---------------------------------------------------------

    public void Open()
    {
        IsOpen = true;
        _input.Reset();
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

        _prevKeys  = keys;
        _prevMouse = mouse;

        if (escPressed || mPressed || closeClicked)
        {
            Close();
            return true;
        }

        // Delegate selection input to GalaxyMapInput using last frame's positions
        _input.Update(universe, _nodePositions);

        return false;
    }

    /// <summary>
    /// Render the overlay.  Call after the game world has been drawn.
    /// Stores node positions for the next Update call's hit-testing.
    /// </summary>
    public void Draw(Universe universe, int screenW, int screenH)
    {
        _nodePositions = _renderer.Draw(universe, screenW, screenH,
                                        _input.HoveredSystemId, _totalSeconds);
    }

    public void Dispose() => _renderer.Dispose();
}
