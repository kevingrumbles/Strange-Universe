namespace Strange_Universe.Game.Components;

/// <summary>Destination for short on-screen notifications.</summary>
public interface IMessageSink
{
    void Post(string message, int durationSeconds = 3);
}

/// <summary>Discards messages. Used when no HUD is attached (tests, before Generate).</summary>
public sealed class NullMessageSink : IMessageSink
{
    public static readonly NullMessageSink Instance = new();
    public void Post(string message, int durationSeconds = 3) { }
}
