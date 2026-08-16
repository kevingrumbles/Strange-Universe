namespace Strange_Universe.Game.Components;

public class InputState
{
    public bool RotateLeft  { get; set; }
    public bool RotateRight { get; set; }
    public bool Thrust      { get; set; }
    public bool Retrograde  { get; set; }
    public bool ZoomIn      { get; set; }
    public bool ZoomOut     { get; set; }
    public bool OpenMap     { get; set; }
    public bool Jump        { get; set; }
    public bool Exit        { get; set; }
    public bool TargetNearest { get; set; }
    public bool CycleTarget   { get; set; }
}
