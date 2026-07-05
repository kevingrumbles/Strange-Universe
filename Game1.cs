using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StrangeUniverse.Game.World;
using StrangeUniverse.Input;
using StrangeUniverse.Rendering.ProceduralGeneration;
using StrangeUniverse.Rendering.SpriteRenderer;

namespace StrangeUniverse
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch           _spriteBatch = null!;

        private InputHandler           _inputHandler  = null!;
        private Rendering.Camera.Camera _camera       = null!;
        private ProceduralTextureCache  _textureCache  = null!;
        private SpriteRenderer          _renderer      = null!;
        private StarSystem              _starSystem    = null!;

        private int _screenWidth;
        private int _screenHeight;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = 1280,
                PreferredBackBufferHeight = 720,
                SynchronizeWithVerticalRetrace = true,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible        = false;
            IsFixedTimeStep       = true;
            TargetElapsedTime     = System.TimeSpan.FromSeconds(1.0 / 60.0);
            Window.Title          = "Strange Universe";
        }

        protected override void Initialize()
        {
            _screenWidth  = GraphicsDevice.Viewport.Width;
            _screenHeight = GraphicsDevice.Viewport.Height;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load JSON settings (falls back to code defaults if files missing)
            GameSettings.Load();

            _inputHandler = new InputHandler();
            _textureCache = new ProceduralTextureCache();

            _camera = new Rendering.Camera.Camera(
                GameSettings.Camera, _screenWidth, _screenHeight);

            // Generate the universe (this creates all procedural textures)
            System.Console.WriteLine("[Strange Universe] Generating universe…");
            var generator = new UniverseGenerator(
                GraphicsDevice, _textureCache,
                GameSettings.Universe, GameSettings.Ship);
            _starSystem = generator.Generate();
            System.Console.WriteLine("[Strange Universe] Universe ready.");

            _renderer = new SpriteRenderer(_spriteBatch, GraphicsDevice, _textureCache);

            // Snap camera to player immediately (no lag on first frame)
            _camera.Update(_starSystem.Player.Transform.Position, 1f, new InputState());
        }

        protected override void Update(GameTime gameTime)
        {
            var kb = Keyboard.GetState();
            if (kb.IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var input = _inputHandler.GetState();

            _starSystem.Update(deltaTime, input);
            _camera.Update(_starSystem.Player.Transform.Position, deltaTime, input);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));   // deep space black

            var cameraMatrix = _camera.GetTransformMatrix();

            // ── World pass (camera transform applied) ─────────────────────────
            _spriteBatch.Begin(
                sortMode:        SpriteSortMode.Deferred,
                blendState:      BlendState.AlphaBlend,
                samplerState:    SamplerState.LinearClamp,
                transformMatrix: cameraMatrix);

            // Draw order (back to front):
            //   Layer 3 – stars behind the nebula
            //   Layer 2 – system star + nebula
            //   Layer 1 – stars in front of the nebula
            //   Layer 0 – planets, asteroids, player
            _renderer.DrawBackgroundStars(
                _starSystem.BackgroundStars,
                _screenWidth, _screenHeight, _camera.Position,
                layer: 3);

            _renderer.DrawStar(_starSystem.Star);

            _renderer.DrawNebula(_starSystem.NebulaTextureId, _starSystem.NebulaWorldSize);

            _renderer.DrawBackgroundStars(
                _starSystem.BackgroundStars,
                _screenWidth, _screenHeight, _camera.Position,
                layer: 1);

            foreach (var planet in _starSystem.Planets)
                _renderer.DrawPlanet(planet);

            foreach (var asteroid in _starSystem.Asteroids)
                _renderer.DrawAsteroid(asteroid);

            _renderer.DrawPlayer(_starSystem.Player);

            _spriteBatch.End();

            // ── HUD pass (no transform) ───────────────────────────────────────
            _spriteBatch.Begin(blendState: BlendState.AlphaBlend);
            _renderer.DrawHud(_starSystem.Player, _screenWidth, _screenHeight, GameSettings.Ship.MaxSpeed);
            _renderer.DrawMinimap(_starSystem, GameSettings.Universe.SystemRadius, _screenWidth, _screenHeight);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            _textureCache?.Dispose();
            base.UnloadContent();
        }
    }
}

