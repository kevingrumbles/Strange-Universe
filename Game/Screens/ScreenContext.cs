using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using System;
using System.Collections.Generic;

using Strange_Universe.Game.Components;
namespace Strange_Universe.Game.Screens;

/// <summary>Shared host resources and navigation callbacks handed to every screen.</summary>
public sealed class ScreenContext
{
    public static readonly Color Background = new(4, 4, 12);

    public required GraphicsDevice GraphicsDevice { get; init; }
    public required RenderService Render { get; init; }
    public required SpriteFont Font { get; init; }
    public required GameSettings Settings { get; init; }
    public required ISaveService Saves { get; init; }
    /// <summary>Shared keyboard reader for gameplay; call <see cref="InputHandler.Rebaseline"/> when a screen takes over.</summary>
    public required InputHandler Input { get; init; }
    public required List<Universe> Universes { get; init; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }

    public required Action ShowMenu { get; init; }
    public required Action ShowNaming { get; init; }
    public required Action<Universe> Play { get; init; }
    public required Action Quit { get; init; }
    public required Action<bool> SetMouseVisible { get; init; }

    public void DrawRect(int x, int y, int w, int h, Color color) =>
        Render.SpriteBatch.Draw(Render.Pixel, new Rectangle(x, y, w, h), color);

    public void DrawBorder(int x, int y, int w, int h, int t, Color color)
    {
        DrawRect(x, y, w, t, color);
        DrawRect(x, y + h - t, w, t, color);
        DrawRect(x, y, t, h, color);
        DrawRect(x + w - t, y, t, h, color);
    }

    public void DrawCentered(string text, float y, Color color)
    {
        Vector2 size = Font.MeasureString(text);
        Render.SpriteBatch.DrawString(Font, text, new Vector2((ScreenWidth - size.X) / 2f, y), color);
    }
}
