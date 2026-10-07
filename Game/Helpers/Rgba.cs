namespace Strange_Universe.Game.Helpers;

/// <summary>
/// An 8-bit-per-channel colour for procedural pixel generation, with no graphics-library dependency.
/// <see cref="Pack"/> matches MonoGame's packed layout (R in the low byte), so pixel buffers can be
/// uploaded to a texture as they are.
/// </summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public Rgba(int r, int g, int b) : this((byte)r, (byte)g, (byte)b, 255) { }

    public uint Pack() => Pack(R, G, B, A);

    public static uint Pack(byte r, byte g, byte b, byte a)
        => (uint)(r | (g << 8) | (b << 16) | (a << 24));
}
