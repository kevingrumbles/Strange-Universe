namespace Strange_Universe.Game.Components;

public class CameraSettings
{
    public float SmoothSpeed  { get; set; } = 8.0f;
    public float DefaultZoom  { get; set; } = 1.0f;
    public float MinZoom      { get; set; } = 0.04f;
    public float MaxZoom      { get; set; } = 5.0f;
    public float ZoomSpeed    { get; set; } = 0.12f;
}
