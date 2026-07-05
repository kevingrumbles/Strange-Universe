using Microsoft.Xna.Framework;

namespace StrangeUniverse.Game.Entities;

/// <summary>
/// Distant background star — just a screen-space dot.
/// Position is in an arbitrary space used only for parallax scrolling; not world space.
/// </summary>
public class BackgroundStar
{
    public Vector2 Position   { get; set; }
    public float   Brightness { get; set; } = 1f;   // 0..1
    public float   Size       { get; set; } = 1f;   // pixels
    /// <summary>Render layer: 1 = in front of nebula, 3 = behind nebula.</summary>
    public int     Layer      { get; set; } = 3;
}
