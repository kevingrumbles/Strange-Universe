using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Systems;

/// <summary>Reads keyboard state and produces an <see cref="InputState"/> each frame.</summary>
public class InputHandler
{
    private KeyboardState _prevKeys;

    public InputHandler()
    {
        _prevKeys = Keyboard.GetState();
    }

    public InputState GetState()
    {
        var kb = Keyboard.GetState();

        var input = new InputState
        {
            RotateLeft  = kb.IsKeyDown(Keys.Left)  || kb.IsKeyDown(Keys.A),
            RotateRight = kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D),
            Thrust      = kb.IsKeyDown(Keys.Up)    || kb.IsKeyDown(Keys.W),
            Retrograde  = kb.IsKeyDown(Keys.Down)  || kb.IsKeyDown(Keys.S),
            ZoomIn      = kb.IsKeyDown(Keys.OemPlus)  || kb.IsKeyDown(Keys.PageUp),
            ZoomOut     = kb.IsKeyDown(Keys.OemMinus) || kb.IsKeyDown(Keys.PageDown),
            OpenMap     = WasPressed(kb, Keys.M),
            Jump        = WasPressed(kb, Keys.J),
            Exit        = WasPressed(kb, Keys.Escape),
            TargetNearest = WasPressed(kb, Keys.R),
            CycleTarget   = WasPressed(kb, Keys.Tab),
        };

        _prevKeys = kb;
        return input;
    }

    private bool WasPressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);
}
