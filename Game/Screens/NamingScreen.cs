using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;

namespace Strange_Universe.Game.Screens;

/// <summary>Text entry for a new universe name.</summary>
public sealed class NamingScreen : IScreen
{
    private const int BoxWidth = 500;
    private const int BoxHeight = 62;

    private readonly ScreenContext _ctx;
    private readonly GameWindow _window;
    private readonly KeyTracker _keys = new();
    private string _name = string.Empty;
    private double _cursorBlink;

    public NamingScreen(ScreenContext ctx, GameWindow window)
    {
        _ctx = ctx;
        _window = window;
    }

    public void OnEnter()
    {
        _name = string.Empty;
        _cursorBlink = 0;
        _keys.Reset();
        _window.TextInput += OnTextInput;
    }

    public void OnExit() => _window.TextInput -= OnTextInput;

    private void OnTextInput(object sender, TextInputEventArgs e)
    {
        if (e.Character == '\b')
        {
            if (_name.Length > 0) _name = _name[..^1];
        }
        else if (!char.IsControl(e.Character) && _name.Length < 40)
        {
            _name += e.Character;
        }
    }

    public void Update(GameTime gameTime)
    {
        _cursorBlink += gameTime.ElapsedGameTime.TotalSeconds;
        _keys.Update();

        if (_keys.Pressed(Keys.Escape))
        {
            _ctx.ShowMenu();
        }
        else if (_keys.Pressed(Keys.Enter))
        {
            var created = new Universe(string.IsNullOrWhiteSpace(_name) ? "New Universe" : _name.Trim());
            _ctx.Universes.Add(created);
            _ctx.Play(created);
        }
    }

    public void Draw(GameTime gameTime)
    {
        _ctx.GraphicsDevice.Clear(ScreenContext.Background);
        _ctx.Render.Begin(BatchMode.ScreenAlpha);

        _ctx.DrawCentered("CREATE NEW UNIVERSE", 80f, new Color(180, 210, 255));
        _ctx.DrawCentered("Enter a name for your universe:", 140f, new Color(120, 140, 160));

        int boxX = (_ctx.ScreenWidth - BoxWidth) / 2;
        int boxY = (_ctx.ScreenHeight - BoxHeight) / 2 - 20;
        _ctx.DrawRect(boxX, boxY, BoxWidth, BoxHeight, new Color(20, 30, 50));
        _ctx.DrawBorder(boxX, boxY, BoxWidth, BoxHeight, 2, new Color(80, 140, 220));

        bool showCursor = (int)(_cursorBlink / 0.5) % 2 == 0;
        _ctx.Render.SpriteBatch.DrawString(_ctx.Font, _name + (showCursor ? "|" : " "),
            new Vector2(boxX + 24, boxY + 14), new Color(220, 235, 255));

        _ctx.DrawCentered("Enter  Confirm      Esc  Back", _ctx.ScreenHeight - 38f, new Color(70, 88, 108));
    }
}
