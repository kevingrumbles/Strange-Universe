using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Loads sprite artwork from the Art folder at runtime.
/// Files are read directly from disk (no MonoGame content pipeline required),
/// so replacing a PNG is immediately reflected on the next run.
/// </summary>
public static class ArtLoader
{
    /// <summary>
    /// Loads a PNG from <paramref name="relativePath"/> (relative to the executable directory)
    /// and returns it as a <see cref="Texture2D"/> managed by the caller.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    public static Texture2D TryLoad(GraphicsDevice gd, string relativePath)
    {
        if (!File.Exists(relativePath))
        {
            System.Console.WriteLine($"[ArtLoader] File not found: {relativePath}");
            return null;
        }

        using var stream = File.OpenRead(relativePath);
        return Texture2D.FromStream(gd, stream);
    }
}
