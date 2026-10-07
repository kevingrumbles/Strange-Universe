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

/// <summary>Publishes posted messages as <see cref="MessageRequested"/> events.</summary>
public sealed class BusMessageSink : IMessageSink
{
    private readonly IEventBus _bus;
    public BusMessageSink(IEventBus bus) => _bus = bus;
    public void Post(string message, int durationSeconds = 3) => _bus.Publish(new MessageRequested(message, durationSeconds));
}
