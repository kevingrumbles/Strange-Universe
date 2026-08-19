using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using Strange_Universe.Game.UI;
using System.Collections.Generic;

namespace StrangeUniverse
{
    enum GameState { Menu, Naming, Playing }

    public class Launcher : Microsoft.Xna.Framework.Game
    {
        public static Universe ActiveUniverse = null;
        public bool debug = false;
        public const bool PlanetColision = false;


        // -- Core --------------------------------------------------------------
        public static GraphicsDevice GD = null;
        public static RenderService RenderService = null!;
        private readonly GraphicsDeviceManager _graphics;
        private SpriteFont _font = null!;
        private int _screenWidth;
        private int _screenHeight;

        // -- State -------------------------------------------------------------
        private GameState _state = GameState.Menu;


        // -- Menu --------------------------------------------------------------
        // Each entry is either a Universe (existing) or null (Create New).
        private int _menuIndex = 0;
        private KeyboardState _prevKeys;
        private string _newUniverseName = string.Empty;
        private double _cursorBlink = 0;

        // -- Gameplay ----------------------------------------------------------
        private InputHandler _inputHandler = null!;
        public static Camera Camera { get; private set; } = null!;
        public static ProceduralTextureCache TextureCache = null!;
        private SpriteRenderer _starsystemRenderer = null!;
        private ProjectileRenderer _projectileRenderer = null!;
        private GalaxyMapOverlay _galaxyMap = null!;
        private List<Universe> _universes = null!;
        private static string _universeFilePath = "Data/universe-settings.json";

        // -- Layout constants --------------------------------------------------
        private const int RowHeight = 62;
        private const int RowPadX = 24;
        private const int RowPadY = 14;
        private const int MenuWidth = 520;

        public Launcher()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 720,
                SynchronizeWithVerticalRetrace = true,
            };
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.ApplyChanges();
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            IsFixedTimeStep = true;
            TargetElapsedTime = System.TimeSpan.FromSeconds(1.0 / 60.0);
            Window.Title = "Strange Universe";
        }

        protected override void Initialize()
        {
            _screenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _screenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            base.Initialize();
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            base.OnExiting(sender, args);
        }

        protected override void LoadContent()
        {
            GD = _graphics.GraphicsDevice;
            _inputHandler = new InputHandler();
            TextureCache = new ProceduralTextureCache();
            RenderService = new RenderService();
            _font = Content.Load<SpriteFont>("Fonts/DefaultFont");
            _starsystemRenderer = new SpriteRenderer(_font);
            _projectileRenderer = new ProjectileRenderer();
            CameraSettings cameraSettings = StaticHelpers.LoadFile<CameraSettings>("Data/camera-settings.json") ?? new CameraSettings();
            ShipStats.Presets = StaticHelpers.LoadFile<List<ShipStats>>("Data/ship-stats.json") ?? new List<ShipStats>();
            EquipmentStats.Presets = StaticHelpers.LoadFile<List<EquipmentStats>>("Data/equipment-stats.json") ?? new List<EquipmentStats>();
            _universes = StaticHelpers.LoadExisting(_universeFilePath);
            Camera = new Camera(cameraSettings, _screenWidth, _screenHeight);

            _menuIndex = 0;
            _prevKeys = Keyboard.GetState();
            Window.TextInput += OnTextInput;
        }

        private void OnTextInput(object sender, TextInputEventArgs e)
        {
            if (_state != GameState.Naming) return;
            if (e.Character == '\b')
            {
                if (_newUniverseName.Length > 0)
                    _newUniverseName = _newUniverseName[..^1];
            }
            else if (!char.IsControl(e.Character) && _newUniverseName.Length < 40)
            {
                _newUniverseName += e.Character;
            }
        }

        // -- Update ------------------------------------------------------------

        protected override void Update(GameTime gameTime)
        {
            if (_state == GameState.Menu)
                UpdateMenu();
            else if (_state == GameState.Naming)
                UpdateNaming(gameTime);
            else if (_state == GameState.Playing)
                UpdatePlaying(gameTime);

            base.Update(gameTime);
        }

        private void UpdateMenu()
        {
            var keys = Keyboard.GetState();

            if (WasPressed(keys, Keys.Escape)) Exit();
            if (WasPressed(keys, Keys.Up))
                _menuIndex = (_menuIndex + _universes.Count) % (_universes.Count + 1);
            if (WasPressed(keys, Keys.Down))
                _menuIndex = (_menuIndex + 1) % (_universes.Count + 1);
            if (WasPressed(keys, Keys.Enter))
                LaunchSelected();
            if (WasPressed(keys, Keys.Delete) && _menuIndex < _universes.Count)
            {
                var toDelete = _universes[_menuIndex];
                _universes.RemoveAt(_menuIndex);
                StaticHelpers.Remove(toDelete, _universeFilePath);
                if (_menuIndex >= _universes.Count && _menuIndex > 0)
                    _menuIndex = _universes.Count - 1;
            }
            _prevKeys = keys;
        }

        private void LaunchSelected()
        {
            var selected = _menuIndex == _universes.Count ? null : _universes[_menuIndex];

            if (selected != null)
            {
                LaunchUniverse(selected);
                return;
            }

            // Ask the user to name the new universe before generating it.
            _newUniverseName = string.Empty;
            _cursorBlink = 0;
            _state = GameState.Naming;
            return;
        }

        private void UpdateNaming(GameTime gameTime)
        {
            _cursorBlink += gameTime.ElapsedGameTime.TotalSeconds;

            var keys = Keyboard.GetState();

            if (WasPressed(keys, Keys.Escape))
            {
                _state = GameState.Menu;
            }
            else if (WasPressed(keys, Keys.Enter))
            {
                string name = _newUniverseName.Trim();
                if (name.Length == 0) name = "New Universe";
                var created = new Universe(name);
                _universes.Add(created);
                LaunchUniverse(created);
            }

            _prevKeys = keys;
        }

        private void LaunchUniverse(Universe universe)
        {
            ClearRuntime();

            ActiveUniverse = universe;
            ActiveUniverse.Generate();

            IsMouseVisible = false;
            _state = GameState.Playing;
        }

        private void ClearRuntime()
        {
            // Dispose old resources
            ActiveUniverse?.Dispose();
            TextureCache?.Dispose();
            _galaxyMap?.Dispose();

            // Create new resources
            TextureCache = new ProceduralTextureCache();
            _inputHandler = new InputHandler();
            _starsystemRenderer = new SpriteRenderer(_font);
            _galaxyMap = new GalaxyMapOverlay(_font);
            ActiveUniverse = null!;
        }
        private void UpdatePlaying(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var input = _inputHandler.GetState();

            // Galaxy map intercepts all input while open
            if (_galaxyMap.IsOpen)
            {
                bool closed = _galaxyMap.Update(ActiveUniverse, _screenWidth, _screenHeight, deltaTime);
                if (closed) IsMouseVisible = false;
                return;
            }

            if (input.Exit)
            {
                if (ActiveUniverse != null)
                    StaticHelpers.Persist(ActiveUniverse, _universeFilePath);
                ClearRuntime();

                // Update _prevKeys to current state so Escape doesn't trigger again in menu
                _prevKeys = Keyboard.GetState();
                _state = GameState.Menu;
                return;
            }

            if (input.OpenMap)
            {
                _galaxyMap.Open();
                IsMouseVisible = true;
                return;
            }

            ActiveUniverse.Update(deltaTime, input);

            // Update projectile particles (must happen after projectiles are moved)
            if (ActiveUniverse.ActiveStarSystem != null)
                _projectileRenderer.UpdateParticles(ActiveUniverse.ActiveStarSystem.Projectiles, deltaTime);
        }

        // -- Draw --------------------------------------------------------------

        protected override void Draw(GameTime gameTime)
        {
            if (_state == GameState.Menu)
                DrawMenu();
            else if (_state == GameState.Naming)
                DrawNaming();
            else
                DrawPlaying();

            RenderService.End();

            base.Draw(gameTime);
        }

        private void DrawMenu()
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));

            RenderService.Begin(BatchMode.ScreenAlpha);

            // Title
            const string title = "STRANGE UNIVERSE";
            Vector2 titleSz = _font.MeasureString(title);
            RenderService.SpriteBatch.DrawString(_font, title,
                new Vector2((_screenWidth - titleSz.X) / 2f, 80f),
                new Color(180, 210, 255));

            const string subtitle = "Select a universe or create a new one";
            Vector2 subtitleSz = _font.MeasureString(subtitle);
            RenderService.SpriteBatch.DrawString(_font, subtitle,
                new Vector2((_screenWidth - subtitleSz.X) / 2f, 114f),
                new Color(120, 140, 160));

            // Menu rows -- vertically centred in the lower portion of the screen
            int totalH = _universes.Count * RowHeight;
            int menuLeft = (_screenWidth - MenuWidth) / 2;
            int menuTop = (_screenHeight - totalH) / 2 + 30;

            for (int i = 0; i < _universes.Count; i++)
            {
                bool isSelected = i == _menuIndex;
                DrawMenuRow(menuLeft, menuTop + i * RowHeight, MenuWidth,
                            _universes[i].Name, $"Seed: {_universes[i].Seed}", isSelected, false);
            }
            DrawMenuRow(menuLeft, menuTop + _universes.Count * RowHeight, MenuWidth,
                        "+ Create New Universe", string.Empty, _menuIndex == _universes.Count, true);

            // Footer hint
            const string hint = "Up/Down  Navigate      Enter  Select      Del  Delete      Esc  Quit";
            Vector2 hintSz = _font.MeasureString(hint);
            RenderService.SpriteBatch.DrawString(_font, hint,
                new Vector2((_screenWidth - hintSz.X) / 2f, _screenHeight - 38f),
                new Color(70, 88, 108));

        }

        private void DrawNaming()
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));

            RenderService.Begin(BatchMode.ScreenAlpha);

            const string title = "CREATE NEW UNIVERSE";
            Vector2 titleSz = _font.MeasureString(title);
            RenderService.SpriteBatch.DrawString(_font, title,
                new Vector2((_screenWidth - titleSz.X) / 2f, 80f),
                new Color(180, 210, 255));

            const string prompt = "Enter a name for your universe:";
            Vector2 promptSz = _font.MeasureString(prompt);
            RenderService.SpriteBatch.DrawString(_font, prompt,
                new Vector2((_screenWidth - promptSz.X) / 2f, 140f),
                new Color(120, 140, 160));

            // Input box
            bool showCursor = (int)(_cursorBlink / 0.5) % 2 == 0;
            string displayed = _newUniverseName + (showCursor ? "|" : " ");
            int boxW = 500;
            int boxH = RowHeight;
            int boxX = (_screenWidth - boxW) / 2;
            int boxY = (_screenHeight - boxH) / 2 - 20;

            DrawRect(boxX, boxY, boxW, boxH, new Color(20, 30, 50));
            DrawRect(boxX, boxY, boxW, 2, new Color(80, 140, 220));
            DrawRect(boxX, boxY + boxH - 2, boxW, 2, new Color(80, 140, 220));
            DrawRect(boxX, boxY, 2, boxH, new Color(80, 140, 220));
            DrawRect(boxX + boxW - 2, boxY, 2, boxH, new Color(80, 140, 220));

            RenderService.SpriteBatch.DrawString(_font, displayed,
                new Vector2(boxX + RowPadX, boxY + RowPadY),
                new Color(220, 235, 255));

            const string hint = "Enter  Confirm      Esc  Back";
            Vector2 hintSz = _font.MeasureString(hint);
            RenderService.SpriteBatch.DrawString(_font, hint,
                new Vector2((_screenWidth - hintSz.X) / 2f, _screenHeight - 38f),
                new Color(70, 88, 108));

        }

        private void DrawMenuRow(int x, int y, int width,
                                  string label, string sub,
                                  bool isSelected, bool isCreate)
        {
            // Background
            DrawRect(x, y, width, RowHeight - 4,
                isSelected ? new Color(30, 50, 80) : new Color(12, 18, 30));

            // Selection border
            if (isSelected)
            {
                var b = new Color(80, 140, 220);
                DrawRect(x, y, width, 2, b);
                DrawRect(x, y + RowHeight - 6, width, 2, b);
                DrawRect(x, y, 2, RowHeight - 4, b);
                DrawRect(x + width - 2, y, 2, RowHeight - 4, b);
            }

            // Label
            Color labelColor = isCreate
                ? (isSelected ? new Color(120, 220, 120) : new Color(80, 160, 80))
                : (isSelected ? new Color(220, 235, 255) : new Color(160, 175, 195));
            RenderService.SpriteBatch.DrawString(_font, label,
                new Vector2(x + RowPadX, y + RowPadY), labelColor);

            // Sub-label (seed)
            if (!string.IsNullOrEmpty(sub))
            {
                Color subColor = isSelected ? new Color(100, 130, 170) : new Color(70, 90, 115);
                RenderService.SpriteBatch.DrawString(_font, sub,
                    new Vector2(x + RowPadX, y + RowPadY + 20), subColor);
            }
        }

        private void DrawRect(int x, int y, int w, int h, Color color)
        {
            using var px = new Texture2D(GraphicsDevice, 1, 1);
            px.SetData(new[] { Color.White });
            RenderService.SpriteBatch.Draw(px, new Rectangle(x, y, w, h), color);
        }

        private void DrawPlaying()
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));
            DrawUniverseLayers(ActiveUniverse.ActiveStarSystem);
            DrawOverlay();

            if (_galaxyMap.IsOpen)
                _galaxyMap.Draw(ActiveUniverse, _screenWidth, _screenHeight);
        }
        private void DrawUniverseLayers(StarSystem sys)
        {
            var cameraMatrix = Camera.GetTransformMatrix();

            //Draw Layer 0
            _starsystemRenderer.DrawBackgroundStars(
                sys.BackgroundStars, _screenWidth, _screenHeight, Camera.Position, Camera.Zoom, layer: 0);

            //Draw Layer 1
            _starsystemRenderer.DrawNebula(sys.NebulaId, _screenWidth, _screenHeight, Camera.Position, Camera.Zoom, debug);

            //Draw Layer 2
            _starsystemRenderer.DrawBackgroundStars(
                sys.BackgroundStars, _screenWidth, _screenHeight, Camera.Position, Camera.Zoom, layer: 1);

            //Draw Layer 3
            foreach (var star in sys.Stars)
                _starsystemRenderer.DrawStar(star);

            //Draw Layer 4
            foreach (var planet in sys.Planets)
                _starsystemRenderer.DrawPlanet(planet);

            //Draw Layer 5
            foreach (var asteroid in sys.Asteroids)
                _starsystemRenderer.DrawAsteroid(asteroid);

            //Draw Layer 6
            _starsystemRenderer.DrawPlayer(ActiveUniverse.Player);

            // Draw NPCs
            foreach (var npc in sys.Npcs)
                _starsystemRenderer.DrawNPC(npc);

            // Draw projectiles with additive blending — RenderService transparently
            // switches batch modes, no manual End/Begin needed here.
            _projectileRenderer.SetCameraMatrix(cameraMatrix);
            _projectileRenderer.DrawAll(sys.Projectiles);

            // Debug: Draw gravity well indicators
            if (debug)
            {
                foreach (var star in sys.Stars)
                {
                    _starsystemRenderer.DrawDebugCircle(
                        star.GravityWell.Center,
                        star.GravityWell.Radius,
                        Color.Yellow * 0.3f,
                        64);
                }

                foreach (var planet in sys.Planets)
                {
                    _starsystemRenderer.DrawDebugCircle(
                        planet.GravityWell.Center,
                        planet.GravityWell.Radius,
                        Color.Cyan * 0.3f,
                        48);
                }
            }
        }

        private void DrawOverlay()
        {
            RenderService.Begin(BatchMode.ScreenAlpha);
            _starsystemRenderer.DrawSpeedBar(ActiveUniverse.Player, _screenWidth, _screenHeight, ActiveUniverse.Player.MaxSpeed);
            _starsystemRenderer.DrawHud(ActiveUniverse, _screenWidth, _screenHeight);

            // Draw timed message if active with fade out
            if (ActiveUniverse.TimedMessageRemaining > 0f && !string.IsNullOrEmpty(ActiveUniverse.TimedMessage))
            {
                // Calculate alpha based on remaining time (fade out during last second)
                float alpha = ActiveUniverse.TimedMessageRemaining < 1f 
                    ? ActiveUniverse.TimedMessageRemaining 
                    : 1f;

                Vector2 messageSize = _font.MeasureString(ActiveUniverse.TimedMessage);
                Vector2 messagePosition = new Vector2(
                    (_screenWidth - messageSize.X) / 2f,
                    _screenHeight - messageSize.Y - 40f);

                Color messageColor = new Color(255, 140, 0) * alpha;
                RenderService.SpriteBatch.DrawString(_font, ActiveUniverse.TimedMessage, messagePosition, messageColor);
            }
        }

        protected override void UnloadContent()
        {
            TextureCache?.Dispose();
            _galaxyMap?.Dispose();
            base.UnloadContent();
        }

        // -- Input helper ------------------------------------------------------

        private bool WasPressed(KeyboardState current, Keys key) =>
            current.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);
    }
}
