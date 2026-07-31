using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.UI;

/// <summary>
/// Handles all drawing for the Galaxy Map overlay.
/// Operates entirely in screen space — no camera transform applied.
/// </summary>
public class GalaxyMapRenderer
{
    // ── Layout ────────────────────────────────────────────────────────────
    private const int   PanelPadding   = 60;    // space between screen edge and map panel
    private const int   NodeRadius     = 8;
    private const int   CurrentRing    = 14;    // ring radius around current system
    private const int   SelectedRing   = 12;    // ring radius around selected system
    private const int   CloseBoxSize   = 32;
    private const int   CloseMargin    = 16;
    private const float LabelOffsetX   = 12f;
    private const float LabelOffsetY   = -8f;
    private const float LabelScale     = 0.65f; // render system names smaller than the base font

    // ── Colours ───────────────────────────────────────────────────────────
    private static readonly Color BgColor         = new Color(4,  8, 20)  * 0.92f;
    private static readonly Color LineColor        = new Color(60, 90, 130) * 0.7f;
    private static readonly Color ReachableLineC   = new Color(80, 160, 220) * 0.55f;
    private static readonly Color NodeColor        = new Color(160, 180, 210);
    private static readonly Color CurrentColor     = new Color(80,  220, 120);
    private static readonly Color HoverColor       = new Color(255, 220,  60);
    private static readonly Color SelectedColor    = new Color(255, 130,  40);
    private static readonly Color LabelColor       = new Color(170, 190, 215);
    private static readonly Color CurrentLabelC    = new Color(140, 255, 160);
    private static readonly Color UndiscoveredC    = new Color(50,  60,  80);
    private static readonly Color CloseBgColor     = new Color(30,  20,  20);
    private static readonly Color CloseXColor      = new Color(220, 100, 80);
    private static readonly Color PanelBorderC     = new Color(60,  80, 120) * 0.8f;

    private readonly SpriteBatch _sb;
    private readonly Texture2D   _pixel;
    private readonly SpriteFont  _font;

    // Computed each Draw call — shared between helpers
    private int    _panelX, _panelY, _panelW, _panelH;
    private float  _scaleX, _scaleY;
    private Vector2 _galaxyMin, _galaxyMax;

    public GalaxyMapRenderer(SpriteBatch spriteBatch, GraphicsDevice gd, SpriteFont font)
    {
        _sb   = spriteBatch;
        _font = font;

        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // ── Public entry point ────────────────────────────────────────────────

    /// <summary>
    /// Draw the full overlay.  SpriteBatch must NOT have been begun — this method
    /// begins and ends its own batch (no transform, alpha blend).
    /// Returns the screen-space centre of each node so GalaxyMapInput can do hit tests.
    /// </summary>
    public Dictionary<string, Vector2> Draw(
        Universe universe,
        int screenW, int screenH,
        string hoveredId,
        double totalSeconds)
    {
        ComputeLayout(universe, screenW, screenH);

        _sb.Begin(blendState: BlendState.AlphaBlend);

        DrawBackground(screenW, screenH);
        var positions = ComputeNodeScreenPositions(universe);
        DrawConnections(universe, positions);
        DrawRouteLines(universe, positions);  // Draw route before nodes so it appears behind
        DrawNodes(universe, positions, hoveredId, totalSeconds);
        DrawLabels(universe, positions);
        DrawJumpTargetHint(universe, screenW, screenH);
        DrawCloseButton(screenW, screenH);

        _sb.End();

        return positions;
    }

    /// <summary>Returns the screen rect of the close (X) button.</summary>
    public Rectangle GetCloseButtonRect(int screenW, int screenH) =>
        new Rectangle(screenW - CloseMargin - CloseBoxSize,
                      CloseMargin,
                      CloseBoxSize, CloseBoxSize);

    /// <summary>Returns the screen rect of the clear route button.</summary>
    public Rectangle GetClearRouteButtonRect(int screenW, int screenH)
    {
        const int buttonWidth = 100;
        const int buttonHeight = 28;
        int x = screenW - PanelPadding - buttonWidth - 16;
        int y = screenH - PanelPadding - buttonHeight - 12;
        return new Rectangle(x, y, buttonWidth, buttonHeight);
    }

    // ── Layout helpers ────────────────────────────────────────────────────

    private void ComputeLayout(Universe universe, int screenW, int screenH)
    {
        _panelX = PanelPadding;
        _panelY = PanelPadding;
        _panelW = screenW - PanelPadding * 2;
        _panelH = screenH - PanelPadding * 2;

        var nodes = universe.StarSystemNodes;
        if (nodes.Count == 0) { _scaleX = _scaleY = 1f; return; }

        // Scale is driven by the node furthest from the origin (0,0) so that
        // galaxy position (0,0) always maps to the panel centre and all other
        // nodes spiderweb outward from there.
        const int innerPad = 20;
        float maxAbsX = nodes.Max(n => Math.Abs(n.GalaxyPosition.X));
        float maxAbsY = nodes.Max(n => Math.Abs(n.GalaxyPosition.Y));

        // Treat the extents symmetrically: the visible half-size on each axis
        // is the half-panel minus padding.
        float halfW = (_panelW / 2f) - innerPad;
        float halfH = (_panelH / 2f) - innerPad;

        _scaleX = maxAbsX > 0 ? halfW / maxAbsX : 1f;
        _scaleY = maxAbsY > 0 ? halfH / maxAbsY : 1f;

        // Uniform scale so layout isn't distorted
        float uniformScale = Math.Min(_scaleX, _scaleY);
        _scaleX = _scaleY = uniformScale;

        // _galaxyMin / _galaxyMax are no longer used for the transform;
        // keep them zeroed so GalaxyToScreen stays simple.
        _galaxyMin = Vector2.Zero;
        _galaxyMax = Vector2.Zero;
    }

    private Vector2 GalaxyToScreen(Vector2 galaxy)
    {
        // Panel centre is the origin of galaxy space.
        float centreX = _panelX + _panelW / 2f;
        float centreY = _panelY + _panelH / 2f;

        return new Vector2(
            centreX + galaxy.X * _scaleX,
            centreY + galaxy.Y * _scaleY);
    }

    private Dictionary<string, Vector2> ComputeNodeScreenPositions(Universe universe)
    {
        var result = new Dictionary<string, Vector2>();
        foreach (var node in universe.StarSystemNodes)
            result[node.SystemId] = GalaxyToScreen(node.GalaxyPosition);
        return result;
    }

    // ── Draw helpers ──────────────────────────────────────────────────────

    private void DrawBackground(int screenW, int screenH)
    {
        // Full-screen dark tint
        _sb.Draw(_pixel, new Rectangle(0, 0, screenW, screenH), new Color(0, 0, 0) * 0.70f);

        // Panel background
        _sb.Draw(_pixel, new Rectangle(_panelX, _panelY, _panelW, _panelH), BgColor);

        // Panel border
        const int b = 1;
        _sb.Draw(_pixel, new Rectangle(_panelX,              _panelY,               _panelW, b),      PanelBorderC);
        _sb.Draw(_pixel, new Rectangle(_panelX,              _panelY + _panelH - b, _panelW, b),      PanelBorderC);
        _sb.Draw(_pixel, new Rectangle(_panelX,              _panelY,               b, _panelH),      PanelBorderC);
        _sb.Draw(_pixel, new Rectangle(_panelX + _panelW - b, _panelY,              b, _panelH),      PanelBorderC);
    }

    private void DrawConnections(Universe universe, Dictionary<string, Vector2> positions)
    {
        string currentId  = universe.ActiveStarSystem.Node.SystemId;
        var    connections = universe.ActiveStarSystem.Node.SystemConnectionIds;
        var    drawn       = new HashSet<string>();

        foreach (var node in universe.StarSystemNodes)
        {
            if (!positions.TryGetValue(node.SystemId, out var fromPos)) continue;

            foreach (var connId in node.SystemConnectionIds)
            {
                // Deduplicate: only draw A→B, not also B→A
                string key = string.CompareOrdinal(node.SystemId, connId) < 0
                    ? node.SystemId + connId
                    : connId + node.SystemId;
                if (!drawn.Add(key)) continue;

                if (!positions.TryGetValue(connId, out var toPos)) continue;

                // Reachable lines from current system are brighter
                bool reachable = node.SystemId == currentId || connId == currentId;
                DrawLine(fromPos, toPos, reachable ? ReachableLineC : LineColor);
            }
        }
    }

    private void DrawRouteLines(Universe universe, Dictionary<string, Vector2> positions)
    {
        if (universe.JumpRoute.Count == 0) return;

        // Draw lines connecting the route systems in sequence
        string currentId = universe.ActiveStarSystem.Node.SystemId;
        var routeColor = new Color(255, 200, 80); // Bright yellow/orange for the route

        // First line: from current system to first route system
        if (positions.TryGetValue(currentId, out var currentPos) &&
            positions.TryGetValue(universe.JumpRoute[0], out var firstRoutePos))
        {
            DrawLine(currentPos, firstRoutePos, routeColor, 3f);
        }

        // Subsequent lines: between consecutive route systems
        for (int i = 0; i < universe.JumpRoute.Count - 1; i++)
        {
            if (positions.TryGetValue(universe.JumpRoute[i], out var fromPos) &&
                positions.TryGetValue(universe.JumpRoute[i + 1], out var toPos))
            {
                DrawLine(fromPos, toPos, routeColor, 3f);
            }
        }
    }

    private void DrawNodes(Universe universe,
                           Dictionary<string, Vector2> positions,
                           string hoveredId,
                           double totalSeconds)
    {
        string currentId  = universe.Player.CurrentStarSystemID;
        string selectedId = universe.SelectedJumpTargetSystemId;
        var    connections = universe.ActiveStarSystem.Node.SystemConnectionIds;

        // Get the last system in the route to determine which systems are reachable next
        string lastRouteSystem = universe.JumpRoute.Count > 0 
            ? universe.JumpRoute[^1] 
            : currentId;
        var lastNode = universe.StarSystemNodes.FirstOrDefault(n => n.SystemId == lastRouteSystem);
        var nextReachable = lastNode?.SystemConnectionIds ?? new HashSet<string>();

        foreach (var node in universe.StarSystemNodes)
        {
            if (!positions.TryGetValue(node.SystemId, out var pos)) continue;

            bool isCurrent  = node.SystemId == currentId;
            bool isSelected = node.SystemId == selectedId;
            bool isHovered  = node.SystemId == hoveredId;
            bool isInRoute = universe.JumpRoute.Contains(node.SystemId);
            bool isReachable = connections.Contains(node.SystemId) || 
                             (isInRoute ? false : nextReachable.Contains(node.SystemId));

            // Rings (drawn before the filled dot)
            if (isCurrent)
            {
                // Pulsing ring
                float pulse = 0.6f + 0.4f * MathF.Sin((float)totalSeconds * 2.5f);
                DrawRing(pos, CurrentRing, 2, CurrentColor * pulse);
            }

            if (isSelected)
                DrawRing(pos, SelectedRing, 2, SelectedColor);

            if (isHovered && !isCurrent)
                DrawRing(pos, NodeRadius + 4, 1, HoverColor * 0.9f);

            // Filled node circle
            Color nodeCol = isCurrent  ? CurrentColor
                          : isSelected ? SelectedColor
                          : isHovered  ? HoverColor
                          : isReachable ? NodeColor
                          : node.Discovered ? NodeColor * 0.6f
                          : UndiscoveredC;

            int r = isCurrent ? NodeRadius + 3 : NodeRadius;
            DrawCircle(pos, r, nodeCol);
        }
    }

    private void DrawLabels(Universe universe, Dictionary<string, Vector2> positions)
    {
        string currentId = universe.Player.CurrentStarSystemID;

        foreach (var node in universe.StarSystemNodes)
        {
            if (!node.Discovered && node.SystemId != currentId) continue;
            if (!positions.TryGetValue(node.SystemId, out var pos)) continue;

            bool isCurrent = node.SystemId == currentId;
            Color col      = isCurrent ? CurrentLabelC : LabelColor;

            _sb.DrawString(_font, node.DisplayName,
                new Vector2(pos.X + LabelOffsetX, pos.Y + LabelOffsetY),
                col, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);
        }
    }

    private void DrawJumpTargetHint(Universe universe, int screenW, int screenH)
    {
        if (universe.JumpRoute.Count == 0)
        {
            // No route selected
            string line1 = "JUMP TARGET";
            string line2 = "None";

            Vector2 sz1 = _font.MeasureString(line1);
            Vector2 sz2 = _font.MeasureString(line2);
            float totalH = sz1.Y + 4 + sz2.Y;
            float y = screenH - PanelPadding - totalH - 12;
            float x = _panelX + 16;

            _sb.DrawString(_font, line1, new Vector2(x, y), new Color(100, 130, 160));
            _sb.DrawString(_font, line2, new Vector2(x, y + sz1.Y + 4), new Color(80, 90, 100));
        }
        else
        {
            // Show route information
            string line1 = $"JUMP ROUTE ({universe.JumpRoute.Count} systems)";

            // Show first system name
            var firstNode = universe.StarSystemNodes.FirstOrDefault(n => n.SystemId == universe.JumpRoute[0]);



            string line2 = $"Next: {firstNode.DisplayName}";

            Vector2 sz1 = _font.MeasureString(line1);
            Vector2 sz2 = _font.MeasureString(line2);
            float totalH = sz1.Y + 4 + sz2.Y;
            float y = screenH - PanelPadding - totalH - 12;
            float x = _panelX + 16;

            _sb.DrawString(_font, line1, new Vector2(x, y), new Color(100, 130, 160));
            _sb.DrawString(_font, line2, new Vector2(x, y + sz1.Y + 4), SelectedColor);

            // Draw Clear Route button
            DrawClearRouteButton(screenW, screenH);
        }
    }

    private void DrawClearRouteButton(int screenW, int screenH)
    {
        var rect = GetClearRouteButtonRect(screenW, screenH);

        // Button background
        _sb.Draw(_pixel, rect, new Color(40, 30, 30));

        // Border
        const int b = 1;
        Color borderColor = new Color(180, 80, 60);
        _sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, b), borderColor);
        _sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - b, rect.Width, b), borderColor);
        _sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, b, rect.Height), borderColor);
        _sb.Draw(_pixel, new Rectangle(rect.Right - b, rect.Y, b, rect.Height), borderColor);

        // Button text
        const string text = "Clear Route";
        Vector2 textSize = _font.MeasureString(text);
        float scale = 0.6f;
        Vector2 scaledSize = textSize * scale;
        _sb.DrawString(_font, text,
            new Vector2(rect.X + (rect.Width - scaledSize.X) / 2f,
                        rect.Y + (rect.Height - scaledSize.Y) / 2f),
            new Color(220, 120, 100),
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawCloseButton(int screenW, int screenH)
    {
        var rect = GetCloseButtonRect(screenW, screenH);

        _sb.Draw(_pixel, rect, CloseBgColor);

        // Border
        const int b = 1;
        _sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, b),           CloseXColor * 0.6f);
        _sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - b, rect.Width, b),  CloseXColor * 0.6f);
        _sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, b, rect.Height),          CloseXColor * 0.6f);
        _sb.Draw(_pixel, new Rectangle(rect.Right - b, rect.Y, b, rect.Height),  CloseXColor * 0.6f);

        // "X" label centred
        const string x = "X";
        Vector2 xSz = _font.MeasureString(x);
        _sb.DrawString(_font, x,
            new Vector2(rect.X + (rect.Width  - xSz.X) / 2f,
                        rect.Y + (rect.Height - xSz.Y) / 2f),
            CloseXColor);
    }

    // ── Pixel-art primitives ──────────────────────────────────────────────

    /// <summary>Bresenham line using 1×1 pixel draws.</summary>
    private void DrawLine(Vector2 a, Vector2 b, Color color)
    {
        int x0 = (int)a.X, y0 = (int)a.Y;
        int x1 = (int)b.X, y1 = (int)b.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            _sb.Draw(_pixel, new Rectangle(x0, y0, 1, 1), color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
    {
        Vector2 edge = b - a;
        float length = edge.Length();
        if (length < 0.1f) return;

        float angle = MathF.Atan2(edge.Y, edge.X);

        _sb.Draw(_pixel,
            new Rectangle((int)a.X, (int)a.Y, (int)length, (int)thickness),
            null,
            color,
            angle,
            new Vector2(0, 0.5f),
            Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
            0);
    }

    /// <summary>Filled square standing in for a circle node.</summary>
    private void DrawCircle(Vector2 centre, int radius, Color color)
    {
        // Approximate filled circle with pixel-level scanlines
        int r = radius;
        for (int y = -r; y <= r; y++)
        {
            int hw = (int)MathF.Sqrt(r * r - y * y);
            _sb.Draw(_pixel,
                new Rectangle((int)centre.X - hw, (int)centre.Y + y, hw * 2 + 1, 1),
                color);
        }
    }

    /// <summary>Hollow ring (1-pixel-thick approximated circle outline).</summary>
    private void DrawRing(Vector2 centre, int radius, int thickness, Color color)
    {
        for (int t = 0; t < thickness; t++)
        {
            int r = radius + t;
            // Sample the circle at enough points to avoid gaps
            int steps = Math.Max(32, r * 4);
            for (int i = 0; i < steps; i++)
            {
                float angle = MathF.Tau * i / steps;
                int px = (int)(centre.X + MathF.Cos(angle) * r);
                int py = (int)(centre.Y + MathF.Sin(angle) * r);
                _sb.Draw(_pixel, new Rectangle(px, py, 1, 1), color);
            }
        }
    }

    public void Dispose()
    {
        _pixel?.Dispose();
    }
}
