using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Screens;
using Strange_Universe.Game.Systems;

namespace Strange_Universe
{
    /// <summary>Thin MonoGame host: owns shared GPU resources and forwards Update/Draw to the active screen.</summary>
    public class Launcher : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly ScreenManager _screens = new();
        private readonly GameSettings _settings = new();

        private RenderService _render;
        private ProjectileRenderer _projectileRenderer;
        private ScreenContext _ctx;
        private MenuScreen _menu;
        private NamingScreen _naming;

        public Launcher()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                IsFullScreen = false,
                SynchronizeWithVerticalRetrace = true,
                PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height,
            };
            _graphics.ApplyChanges();
            IsMouseVisible = true;
            IsFixedTimeStep = true;
            TargetElapsedTime = System.TimeSpan.FromSeconds(1.0 / 60.0);
            Window.Title = "Strange Universe";
        }

        protected override void LoadContent() => CreateResources();

        protected override void UnloadContent()
        {
            DisposeResources();
            base.UnloadContent();
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            // Leaving the playing screen persists the active universe.
            _screens.Shutdown();
            base.OnExiting(sender, args);
        }

        private void CreateResources()
        {
            _render = new RenderService(GraphicsDevice);
            _projectileRenderer = new ProjectileRenderer(GraphicsDevice, _render);
            _ctx = new ScreenContext
            {
                GraphicsDevice = GraphicsDevice,
                Render = _render,
                Font = FontBuilder.Build(GraphicsDevice, "Arial", 16f),
                Settings = _settings,
                Universes = Persistence.LoadExisting(ScreenContext.UniverseFilePath),
                ScreenWidth = _graphics.PreferredBackBufferWidth,
                ScreenHeight = _graphics.PreferredBackBufferHeight,
                ShowMenu = () => _screens.Switch(_menu),
                ShowNaming = () => _screens.Switch(_naming),
                Play = Play,
                Quit = Exit,
                SetMouseVisible = visible => IsMouseVisible = visible,
            };
            _menu = new MenuScreen(_ctx);
            _naming = new NamingScreen(_ctx, Window);
            _screens.Switch(_menu);
        }

        private void DisposeResources()
        {
            _screens.Shutdown();
            _projectileRenderer?.Dispose();
            _ctx?.Font.Texture.Dispose();
            _render?.Dispose();
            _projectileRenderer = null;
            _render = null;
            _ctx = null;
        }

        private void Play(Universe universe) =>
            _screens.Switch(new PlayingScreen(_ctx, _projectileRenderer, universe));

        protected override void Update(GameTime gameTime)
        {
            _screens.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _screens.Draw(gameTime);
            _render?.End();
            base.Draw(gameTime);
        }
    }
}
