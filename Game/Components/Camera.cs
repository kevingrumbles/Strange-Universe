using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Smooth-following, zoomable camera.
/// Produces a <see cref="Matrix"/> for use with the sprite batch.
/// </summary>
public class Camera
{
    public Vector2 Position     { get; private set; }
    public float   Zoom         { get; private set; }
    public Vector2 ScreenCenter { get; private set; }
    public float DefaultZoom { get; set; } = 1.0f;
    public float MinZoom { get; set; } = 0.04f;
    public float MaxZoom { get; set; } = 5.0f;
    public float ZoomSpeed { get; set; } = 0.12f;

    public Camera(int screenWidth, int screenHeight)
    {
        Zoom = DefaultZoom;
        ScreenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
    }

    public void Update(Vector2 target, float deltaTime, InputState input)
    {
        if (float.IsFinite(target.X) && float.IsFinite(target.Y))
            Position = target;

        // Zoom
        if (input.ZoomIn)
            Zoom = MathHelper.Clamp(Zoom + Zoom * ZoomSpeed, MinZoom, MaxZoom);
        if (input.ZoomOut)
            Zoom = MathHelper.Clamp(Zoom - Zoom * ZoomSpeed, MinZoom, MaxZoom);
    }

    /// <summary>Returns the sprite-batch transform matrix for world-space drawing.</summary>
    public Matrix GetTransformMatrix() =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(ScreenCenter.X, ScreenCenter.Y, 0f);

    public Vector2 WorldToScreen(Vector2 worldPos) =>
        Vector2.Transform(worldPos, GetTransformMatrix());

    public Vector2 ScreenToWorld(Vector2 screenPos) =>
        Vector2.Transform(screenPos, Matrix.Invert(GetTransformMatrix()));
}
