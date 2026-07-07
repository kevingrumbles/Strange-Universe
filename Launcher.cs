using Microsoft.VisualBasic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strange_Universe.Game.Entities;
using StrangeUniverse.Game.World;
using StrangeUniverse.Input;
using StrangeUniverse.Rendering.Camera;
using StrangeUniverse.Rendering.ProceduralGeneration;
using StrangeUniverse.Rendering.SpriteRenderer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Text.Json;

namespace StrangeUniverse
{
    enum GameState { Menu, Naming, Playing }

    public class Launcher : Microsoft.Xna.Framework.Game
    {
        // ── Core ──────────────────────────────────────────────────────────────
        public static GraphicsDevice GD;
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch             _spriteBatch = null!;
        private SpriteFont              _font        = null!;
        private int _screenWidth;
        private int _screenHeight;

        // ── State ─────────────────────────────────────────────────────────────
        private GameState _state = GameState.Menu;

        // ── Menu ──────────────────────────────────────────────────────────────
        // Each entry is either a Universe (existing) or null (Create New).
        private int                     _menuIndex = 0;
        private KeyboardState           _prevKeys;
        private string                  _newUniverseName = string.Empty;
        private double                  _cursorBlink     = 0;

        // ── Gameplay ──────────────────────────────────────────────────────────
        private InputHandler            _inputHandler   = null!;
        private Rendering.Camera.Camera _camera         = null!;  
        public static ProceduralTextureCache  TextureCache   = null!;
        private SpriteRenderer          _renderer       = null!;
        private Universe                _activeUniverse = null!;
        private CameraSettings          _cameraSettings = null!;
        private List<Universe>         _universes      = null!;
        private static string _universeFilePath = "Data/universe-settings.json";

        // ── Layout constants ──────────────────────────────────────────────────
        private const int RowHeight = 62;
        private const int RowPadX   = 24;
        private const int RowPadY   = 14;
        private const int MenuWidth = 520;

        private static readonly JsonSerializerOptions _readOptions = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions _writeOptions = new()
        {
            WriteIndented = true,
        };

        public Launcher()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth       = 1280,
                PreferredBackBufferHeight      = 720,
                SynchronizeWithVerticalRetrace = true,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible        = true;
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

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            if (_activeUniverse != null)
                Persist(_activeUniverse, _universeFilePath);
            base.OnExiting(sender, args);
        }

        protected override void LoadContent()
        {
            GD = GraphicsDevice;
            _inputHandler = new InputHandler();
            TextureCache = new ProceduralTextureCache();
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _renderer = new SpriteRenderer(_spriteBatch, GraphicsDevice, TextureCache);
            _font        = Content.Load<SpriteFont>("Fonts/DefaultFont");
            _cameraSettings = LoadFile<CameraSettings>("Data/camera-settings.json") ?? new CameraSettings();
            _camera = new Rendering.Camera.Camera(_cameraSettings, _screenWidth, _screenHeight);
            var shipStats = LoadFile<List<ShipStats>>("Data/ship-stats.json");
            ShipStats.Presets = LoadFile<List<ShipStats>>("Data/ship-stats.json"); 
            _universes = LoadExisting(_universeFilePath);

            _menuIndex = 0;
            _prevKeys  = Keyboard.GetState();
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

        public static List<Universe> LoadExisting(string path)
        {
            if (!File.Exists(path)) return new List<Universe>();
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<Universe>>(json, _readOptions) ?? new List<Universe>();
            }
            catch(Exception e)
            {
                System.Console.WriteLine($"[Strange Universe] Failed to load universe settings: {e.Message}");
                return new List<Universe>();
            }
        }

        // ── Update ────────────────────────────────────────────────────────────

        protected override void Update(GameTime gameTime)
        {
            if (_state == GameState.Menu)
                UpdateMenu();
            else if (_state == GameState.Naming)
                UpdateNaming(gameTime);
            else
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
            IsMouseVisible = true;
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
                var created = Universe.CreateDefault(name);
                _universes.Add(created);
                LaunchUniverse(created);
            }

            _prevKeys = keys;
        }

        private void LaunchUniverse(Universe universe)
        {
            var generator = new UniverseGenerator(universe);
            _activeUniverse = generator.Generate();
            _camera.Update(_activeUniverse.ActiveStarSystem.Player.Transform.Position, 1f, new InputState());

            IsMouseVisible = false;
            _state = GameState.Playing;
        }

        private void UpdatePlaying(GameTime gameTime)
        {
            var keys = Keyboard.GetState();
            if (WasPressed(keys, Keys.Escape))
            {
                _state = GameState.Menu;
                _prevKeys = keys;
            }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var   input     = _inputHandler.GetState();

            _activeUniverse.Update(deltaTime, input);
            _camera.Update(_activeUniverse.ActiveStarSystem.Player.Transform.Position, deltaTime, input);
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        protected override void Draw(GameTime gameTime)
        {
            if (_state == GameState.Menu)
                DrawMenu();
            else if (_state == GameState.Naming)
                DrawNaming();
            else
                DrawPlaying();

            base.Draw(gameTime);
        }

        private void DrawMenu()
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));

            _spriteBatch.Begin(blendState: BlendState.AlphaBlend);

            // Title
            const string title   = "STRANGE UNIVERSE";
            Vector2      titleSz = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title,
                new Vector2((_screenWidth - titleSz.X) / 2f, 80f),
                new Color(180, 210, 255));

            const string subtitle   = "Select a universe or create a new one";
            Vector2      subtitleSz = _font.MeasureString(subtitle);
            _spriteBatch.DrawString(_font, subtitle,
                new Vector2((_screenWidth - subtitleSz.X) / 2f, 114f),
                new Color(120, 140, 160));

            // Menu rows -- vertically centred in the lower portion of the screen
            int totalH   = _universes.Count * RowHeight;
            int menuLeft = (_screenWidth  - MenuWidth) / 2;
            int menuTop  = (_screenHeight - totalH)    / 2 + 30;

            for (int i = 0; i < _universes.Count; i++)
            {
                bool   isSelected = i == _menuIndex;
                DrawMenuRow(menuLeft, menuTop + i * RowHeight, MenuWidth,
                            _universes[i].Name, $"Seed: {_universes[i].Seed}", isSelected, false);
            }
            DrawMenuRow(menuLeft, menuTop + _universes.Count * RowHeight, MenuWidth,
                        "+ Create New Universe", string.Empty, _menuIndex == _universes.Count, true);

            // Footer hint
            const string hint   = "Up/Down  Navigate      Enter  Select      Esc  Quit";
            Vector2      hintSz = _font.MeasureString(hint);
            _spriteBatch.DrawString(_font, hint,
                new Vector2((_screenWidth - hintSz.X) / 2f, _screenHeight - 38f),
                new Color(70, 88, 108));

            _spriteBatch.End();
        }

        private void DrawNaming()
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));

            _spriteBatch.Begin(blendState: BlendState.AlphaBlend);

            const string title   = "CREATE NEW UNIVERSE";
            Vector2      titleSz = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title,
                new Vector2((_screenWidth - titleSz.X) / 2f, 80f),
                new Color(180, 210, 255));

            const string prompt   = "Enter a name for your universe:";
            Vector2      promptSz = _font.MeasureString(prompt);
            _spriteBatch.DrawString(_font, prompt,
                new Vector2((_screenWidth - promptSz.X) / 2f, 140f),
                new Color(120, 140, 160));

            // Input box
            bool   showCursor = (int)(_cursorBlink / 0.5) % 2 == 0;
            string displayed  = _newUniverseName + (showCursor ? "|" : " ");
            int    boxW       = 500;
            int    boxH       = RowHeight;
            int    boxX       = (_screenWidth  - boxW) / 2;
            int    boxY       = (_screenHeight - boxH) / 2 - 20;

            DrawRect(boxX, boxY, boxW, boxH, new Color(20, 30, 50));
            DrawRect(boxX,           boxY,           boxW, 2, new Color(80, 140, 220));
            DrawRect(boxX,           boxY + boxH - 2, boxW, 2, new Color(80, 140, 220));
            DrawRect(boxX,           boxY,           2,    boxH, new Color(80, 140, 220));
            DrawRect(boxX + boxW - 2, boxY,           2,    boxH, new Color(80, 140, 220));

            _spriteBatch.DrawString(_font, displayed,
                new Vector2(boxX + RowPadX, boxY + RowPadY),
                new Color(220, 235, 255));

            const string hint   = "Enter  Confirm      Esc  Back";
            Vector2      hintSz = _font.MeasureString(hint);
            _spriteBatch.DrawString(_font, hint,
                new Vector2((_screenWidth - hintSz.X) / 2f, _screenHeight - 38f),
                new Color(70, 88, 108));

            _spriteBatch.End();
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
                DrawRect(x,             y,                  width,     2,           b);
                DrawRect(x,             y + RowHeight - 6,  width,     2,           b);
                DrawRect(x,             y,                  2,         RowHeight - 4, b);
                DrawRect(x + width - 2, y,                  2,         RowHeight - 4, b);
            }

            // Label
            Color labelColor = isCreate
                ? (isSelected ? new Color(120, 220, 120) : new Color(80, 160, 80))
                : (isSelected ? new Color(220, 235, 255) : new Color(160, 175, 195));
            _spriteBatch.DrawString(_font, label,
                new Vector2(x + RowPadX, y + RowPadY), labelColor);

            // Sub-label (seed)
            if (!string.IsNullOrEmpty(sub))
            {
                Color subColor = isSelected ? new Color(100, 130, 170) : new Color(70, 90, 115);
                _spriteBatch.DrawString(_font, sub,
                    new Vector2(x + RowPadX, y + RowPadY + 20), subColor);
            }
        }

        private void DrawRect(int x, int y, int w, int h, Color color)
        {
            using var px = new Texture2D(GraphicsDevice, 1, 1);
            px.SetData(new[] { Color.White });
            _spriteBatch.Draw(px, new Rectangle(x, y, w, h), color);
        }

        private void DrawPlaying()
        {
            GraphicsDevice.Clear(new Color(4, 4, 12));

            var cameraMatrix = _camera.GetTransformMatrix();

            // ── World pass (camera transform applied) ──────────────────────────
            _spriteBatch.Begin(
                sortMode:        SpriteSortMode.Deferred,
                blendState:      BlendState.AlphaBlend,
                samplerState:    SamplerState.LinearClamp,
                transformMatrix: cameraMatrix);

            // Draw order (back to front):
            //   Layer 3 -- stars behind the nebula
            //   Layer 2 -- system star + nebula
            //   Layer 1 -- stars in front of the nebula
            //   Layer 0 -- planets, asteroids, player
            var sys = _activeUniverse.ActiveStarSystem;

            _renderer.DrawBackgroundStars(
                sys.BackgroundStars, _screenWidth, _screenHeight, _camera.Position, layer: 3);

            _renderer.DrawStar(sys.Star);
            _renderer.DrawNebula(sys.NebulaTextureId, sys.NebulaWorldSize);

            _renderer.DrawBackgroundStars(
                sys.BackgroundStars, _screenWidth, _screenHeight, _camera.Position, layer: 1);

            foreach (var planet in sys.Planets)
                _renderer.DrawPlanet(planet);

            foreach (var asteroid in sys.Asteroids)
                _renderer.DrawAsteroid(asteroid);

            _renderer.DrawPlayer(sys.Player);

            _spriteBatch.End();

            // ── HUD pass (no transform) ────────────────────────────────────────
            _spriteBatch.Begin(blendState: BlendState.AlphaBlend);
            _renderer.DrawHud(sys.Player, _screenWidth, _screenHeight, sys.Player.Ship.MaxSpeed);
            _renderer.DrawMinimap(sys, sys.SystemRadius, _screenWidth, _screenHeight);
            _spriteBatch.End();
        }

        protected override void UnloadContent()
        {
            TextureCache?.Dispose();
            base.UnloadContent();
        }

        // ── Input helper ──────────────────────────────────────────────────────

        private bool WasPressed(KeyboardState current, Keys key) =>
            current.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);

        private static T? LoadFile<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                var deserialized = JsonSerializer.Deserialize<T>(json, _readOptions);
                return deserialized;
            }
            catch
            {
                return null;
            }
        }
        private void Persist(Universe activeUniverse, string path)
        {
            var all = LoadExisting(path);
            int idx = all.FindIndex(u => u.Id == activeUniverse.Id);
            if (idx >= 0)
                all[idx] = activeUniverse;
            else
                all.Add(activeUniverse);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(all, _writeOptions));
        }
    }
}
