using Microsoft.Xna.Framework.Input;

namespace Strange_Universe.Game.Screens;

/// <summary>Edge-triggered keyboard helper. <see cref="Reset"/> on screen enter so keys held across a transition don't fire.</summary>
public sealed class KeyTracker
{
    private KeyboardState _prev, _current;

    public void Reset() => _prev = _current = Keyboard.GetState();

    public void Update()
    {
        _prev = _current;
        _current = Keyboard.GetState();
    }

    public bool Pressed(Keys key) => _current.IsKeyDown(key) && !_prev.IsKeyDown(key);
}
