using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Strange_Universe.Game.Systems;

/// <summary>Owns all procedurally generated <see cref="Texture2D"/> objects keyed by ID string.</summary>
public sealed class ProceduralTextureCache : IDisposable
{
    private readonly Dictionary<string, Texture2D> _textures = new();

    public void Register(string id, Texture2D texture)
    {
        if (_textures.TryGetValue(id, out var existing))
        {
            existing.Dispose();
        }
        _textures[id] = texture;
    }

    public Texture2D Get(string id) =>
        _textures.TryGetValue(id, out var tex)
            ? tex
            : throw new KeyNotFoundException($"Texture '{id}' not found in cache.");

    public bool TryGet(string id, out Texture2D texture) =>
        _textures.TryGetValue(id, out texture);

    public void Dispose()
    {
        foreach (var tex in _textures.Values)
            tex?.Dispose();
        _textures.Clear();
    }
}
