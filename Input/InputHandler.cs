using Microsoft.Xna.Framework.Input;

namespace StrangeUniverse.Input;

/// <summary>Reads keyboard state and produces an <see cref="InputState"/> each frame.</summary>
public class InputHandler
{
    public InputState GetState()
    {
        var kb = Keyboard.GetState();
        return new InputState
        {
            RotateLeft  = kb.IsKeyDown(Keys.Left)  || kb.IsKeyDown(Keys.A),
            RotateRight = kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D),
            Thrust      = kb.IsKeyDown(Keys.Up)    || kb.IsKeyDown(Keys.W),
            Brake       = kb.IsKeyDown(Keys.Down)  || kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Space),
            ZoomIn      = kb.IsKeyDown(Keys.OemPlus)  || kb.IsKeyDown(Keys.PageUp),
            ZoomOut     = kb.IsKeyDown(Keys.OemMinus) || kb.IsKeyDown(Keys.PageDown),
        };
    }
}
