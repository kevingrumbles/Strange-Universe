using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StrangeUniverse;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// The set of drawing "modes" a renderer can request. Each mode maps to a
/// specific SpriteBatch blend/sampler/transform configuration.
/// </summary>
public enum BatchMode
{
    /// <summary>Camera-transformed, alpha-blended world-space drawing (stars, planets, ships, nebula, debug rings).</summary>
    WorldAlpha,

    /// <summary>Camera-transformed, additive-blended world-space drawing (projectile glows/particles).</summary>
    WorldAdditive,

    /// <summary>Screen-space (no camera transform), alpha-blended drawing (HUD, menus, overlays).</summary>
    ScreenAlpha,
}

/// <summary>
/// Owns the single shared <see cref="SpriteBatch"/> used by every renderer in the game and
/// manages its Begin/End lifecycle and blend/transform state. Renderers call <see cref="Begin"/>
/// to declare what mode they need to draw in; the service transparently starts a new batch
/// session only when the requested mode (or camera transform) actually changes, so callers
/// never need to worry about opening or closing batches themselves.
/// </summary>
public class RenderService
{
    public SpriteBatch SpriteBatch { get; private set; }

    private BatchMode? _activeMode;
    private Matrix _activeTransform;
    private bool _batchOpen;

    /// <summary>
    /// Ensures the SpriteBatch is open in the requested mode. If a batch is already open in a
    /// different mode (or with a different world transform), it is ended and a new one begun.
    /// If the requested mode already matches the active session, this is a no-op.
    /// </summary>
    /// <param name="mode">The draw mode required by the caller.</param>
    /// <param name="transformMatrix">
    /// The camera transform to use for world-space modes. Ignored for <see cref="BatchMode.ScreenAlpha"/>.
    /// </param>
    public void Begin(BatchMode mode, Matrix? transformMatrix = null)
    {
        if (SpriteBatch == null || SpriteBatch.IsDisposed)
        {
            SpriteBatch = new SpriteBatch(Launcher.GD);
            _batchOpen = false;
            _activeMode = null;
        }

        Matrix transform = transformMatrix ?? Matrix.Identity;

        if (_batchOpen && _activeMode == mode && (mode == BatchMode.ScreenAlpha || _activeTransform.Equals(transform)))
        {
            // Already in the requested state — nothing to do.
            return;
        }
        End();

        switch (mode)
        {
            case BatchMode.WorldAlpha:
                SpriteBatch.Begin(sortMode: SpriteSortMode.Deferred,
                                   blendState: BlendState.AlphaBlend,
                                   samplerState: SamplerState.LinearClamp,
                                   transformMatrix: transform);
                break;

            case BatchMode.WorldAdditive:
                SpriteBatch.Begin(sortMode: SpriteSortMode.Deferred,
                                   blendState: BlendState.Additive,
                                   samplerState: SamplerState.LinearClamp,
                                   transformMatrix: transform);
                break;

            case BatchMode.ScreenAlpha:
                SpriteBatch.Begin(blendState: BlendState.AlphaBlend);
                break;
        }

        _activeMode = mode;
        _activeTransform = transform;
        _batchOpen = true;
    }

    /// <summary>Ends the currently open batch session, if any. Safe to call when no batch is open.</summary>
    public void End()
    {
        if (!_batchOpen) return;

        if (SpriteBatch != null && !SpriteBatch.IsDisposed)
            SpriteBatch.End();

        _batchOpen = false;
        _activeMode = null;
    }
}
