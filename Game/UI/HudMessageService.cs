using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.UI;

/// <summary>
/// Holds the single timed message shown at the bottom of the screen.
/// Owned by <see cref="Launcher"/>; the simulation posts via <see cref="IMessageSink"/>.
/// </summary>
public sealed class HudMessageService : IMessageSink
{
    public string Message { get; private set; } = string.Empty;
    public float Remaining { get; private set; }

    public void Post(string message, int durationSeconds = 3)
    {
        Message = message;
        Remaining = durationSeconds;
    }

    public void Update(float deltaTime)
    {
        if (Remaining <= 0f) return;
        Remaining -= deltaTime;
        if (Remaining < 0f) Remaining = 0f;
    }

    public void Clear()
    {
        Message = string.Empty;
        Remaining = 0f;
    }
}
