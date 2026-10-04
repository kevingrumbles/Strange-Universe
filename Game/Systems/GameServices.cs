using System;
using Microsoft.Xna.Framework.Graphics;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Runtime services shared by entities and renderers. Created by <see cref="Launcher"/>
/// and recreated whenever a universe is launched.
/// <see cref="GraphicsDevice"/> may be <c>null</c> (e.g. in tests); texture creation is then skipped.
/// </summary>
public sealed class GameServices : IDisposable
{
    public GraphicsDevice GraphicsDevice { get; }
    public ProceduralTextureCache TextureCache { get; }
    public AssetService Assets { get; }

    public GameServices(GraphicsDevice graphicsDevice, ProceduralTextureCache textureCache = null)
    {
        GraphicsDevice = graphicsDevice;
        TextureCache = textureCache ?? new ProceduralTextureCache();
        Assets = new AssetService(graphicsDevice, TextureCache);
    }

    public void Dispose() => TextureCache.Dispose();
}
