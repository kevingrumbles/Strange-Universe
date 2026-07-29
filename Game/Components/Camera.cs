using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Smooth-following, zoomable camera.
/// Produces a <see cref="Matrix"/> for use with <c>SpriteBatch.Begin</c>.
/// </summary>
public class Camera
{
    public Vector2 Position     { get; private set; }
    public float   Zoom         { get; private set; }
    public Vector2 ScreenCenter { get; private set; }

    private readonly CameraSettings _settings;

    public Camera(CameraSettings settings, int screenWidth, int screenHeight)
    {
        _settings    = settings;
        Zoom         = settings.DefaultZoom;
        ScreenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
    }

    public void Update(Vector2 target, float deltaTime, InputState input)
    {
        Position = target;

        // Zoom
        if (input.ZoomIn)
            Zoom = MathHelper.Clamp(Zoom + Zoom * _settings.ZoomSpeed, _settings.MinZoom, _settings.MaxZoom);
        if (input.ZoomOut)
            Zoom = MathHelper.Clamp(Zoom - Zoom * _settings.ZoomSpeed, _settings.MinZoom, _settings.MaxZoom);
    }

    /// <summary>Returns the SpriteBatch transform matrix for world-space drawing.</summary>
    public Matrix GetTransformMatrix() =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(ScreenCenter.X, ScreenCenter.Y, 0f);

    public Vector2 WorldToScreen(Vector2 worldPos) =>
        Vector2.Transform(worldPos, GetTransformMatrix());

    public Vector2 ScreenToWorld(Vector2 screenPos) =>
        Vector2.Transform(screenPos, Matrix.Invert(GetTransformMatrix()));
}
