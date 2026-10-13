namespace Strange_Universe.Game.Components;

/// <summary>Runtime toggles that used to be launcher fields/constants.</summary>
public sealed class GameSettings
{
    /// <summary>Draws gravity wells, hitboxes and nebula debug info.</summary>
    public bool ShowDebug { get; set; }

    /// <summary>
    /// Test mode: the system spawn event is always the NPC test event (see <c>NpcTestEvent</c>), so every system
    /// has test NPCs to try things on. Set from <c>Launcher.TestMode</c>.
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>When true, ships collide with planets (formerly <c>Launcher.PlanetColision</c>).</summary>
    public bool PlanetCollision { get; set; }
}
