namespace Strange_Universe.Game.Components;

/// <summary>Runtime toggles that used to be launcher fields/constants.</summary>
public sealed class GameSettings
{
    /// <summary>Draws gravity wells, hitboxes and nebula debug info.</summary>
    public bool ShowDebug { get; set; }

    /// <summary>
    /// Test mode: the system spawn event is always the NPC test event (see <c>EventController.SpawnNpcTest</c>), so every system
    /// has test NPCs to try things on. Set from <c>Launcher.TestMode</c>.
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>
    /// Debug: no nebula is generated, so performance can be seen without it (systems have no nebula background). Set from
    /// <c>Launcher.PauseNebulaGeneration</c>; read when a universe is started.
    /// </summary>
    public bool PauseNebulaGeneration { get; set; }

    /// <summary>When true, ships collide with planets (formerly <c>Launcher.PlanetColision</c>).</summary>
    public bool PlanetCollision { get; set; }
}
