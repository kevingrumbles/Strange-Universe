using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Screens;

public interface IScreen
{
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime);
    void OnEnter();
    void OnExit();
}

/// <summary>Holds the active screen and runs exit/enter on transitions.</summary>
public sealed class ScreenManager
{
    public IScreen Current { get; private set; }

    public void Switch(IScreen next)
    {
        Current?.OnExit();
        Current = next;
        Current?.OnEnter();
    }

    public void Update(GameTime gameTime) => Current?.Update(gameTime);
    public void Draw(GameTime gameTime) => Current?.Draw(gameTime);

    /// <summary>Exits the current screen without entering another. Safe to call more than once.</summary>
    public void Shutdown() => Switch(null);
}
