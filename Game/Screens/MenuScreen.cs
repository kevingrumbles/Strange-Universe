using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Systems;

namespace Strange_Universe.Game.Screens;

/// <summary>Universe list: select, create, delete, quit.</summary>
public sealed class MenuScreen : IScreen
{
    private const int RowHeight = 62;
    private const int RowPadX = 24;
    private const int RowPadY = 14;
    private const int MenuWidth = 520;

    private readonly ScreenContext _ctx;
    private readonly KeyTracker _keys = new();
    private int _index;

    public MenuScreen(ScreenContext ctx) => _ctx = ctx;

    public void OnEnter()
    {
        _keys.Reset();
        _ctx.SetMouseVisible(true);
        if (_index > _ctx.Universes.Count) _index = _ctx.Universes.Count;
    }

    public void OnExit() { }

    public void Update(GameTime gameTime)
    {
        _keys.Update();
        var universes = _ctx.Universes;
        int rows = universes.Count + 1;

        if (_keys.Pressed(Keys.Escape)) { _ctx.Quit(); return; }
        if (_keys.Pressed(Keys.Up)) _index = (_index + universes.Count) % rows;
        if (_keys.Pressed(Keys.Down)) _index = (_index + 1) % rows;

        if (_keys.Pressed(Keys.Enter))
        {
            if (_index == universes.Count) _ctx.ShowNaming();
            else _ctx.Play(universes[_index]);
            return;
        }

        if (_keys.Pressed(Keys.Delete) && _index < universes.Count)
        {
            var toDelete = universes[_index];
            universes.RemoveAt(_index);
            _ctx.Saves.Delete(toDelete);
            if (_index >= universes.Count && _index > 0)
                _index = universes.Count - 1;
        }
    }

    public void Draw(GameTime gameTime)
    {
        _ctx.GraphicsDevice.Clear(ScreenContext.Background);
        _ctx.Render.Begin(BatchMode.ScreenAlpha);

        _ctx.DrawCentered("STRANGE UNIVERSE", 80f, new Color(180, 210, 255));
        _ctx.DrawCentered("Select a universe or create a new one", 114f, new Color(120, 140, 160));

        var universes = _ctx.Universes;
        int menuLeft = (_ctx.ScreenWidth - MenuWidth) / 2;
        int menuTop = (_ctx.ScreenHeight - universes.Count * RowHeight) / 2 + 30;

        for (int i = 0; i < universes.Count; i++)
            DrawRow(menuLeft, menuTop + i * RowHeight, universes[i].Name, $"Seed: {universes[i].Seed}", i == _index, false);
        DrawRow(menuLeft, menuTop + universes.Count * RowHeight, "+ Create New Universe", string.Empty, _index == universes.Count, true);

        _ctx.DrawCentered("Up/Down  Navigate      Enter  Select      Del  Delete      Esc  Quit",
            _ctx.ScreenHeight - 38f, new Color(70, 88, 108));
    }

    private void DrawRow(int x, int y, string label, string sub, bool selected, bool isCreate)
    {
        _ctx.DrawRect(x, y, MenuWidth, RowHeight - 4, selected ? new Color(30, 50, 80) : new Color(12, 18, 30));
        if (selected)
            _ctx.DrawBorder(x, y, MenuWidth, RowHeight - 4, 2, new Color(80, 140, 220));

        Color labelColor = isCreate
            ? (selected ? new Color(120, 220, 120) : new Color(80, 160, 80))
            : (selected ? new Color(220, 235, 255) : new Color(160, 175, 195));
        var sb = _ctx.Render.SpriteBatch;
        sb.DrawString(_ctx.Font, label, new Vector2(x + RowPadX, y + RowPadY), labelColor);

        if (!string.IsNullOrEmpty(sub))
            sb.DrawString(_ctx.Font, sub, new Vector2(x + RowPadX, y + RowPadY + 20),
                selected ? new Color(100, 130, 170) : new Color(70, 90, 115));
    }
}
