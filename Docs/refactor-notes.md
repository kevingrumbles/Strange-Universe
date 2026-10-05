# Refactor Notes

Running log for the logic/graphics separation refactor. Each phase appends here.

---

## Phase 0 - Baseline and inventory

### Environment / baseline

- Branch `refactor/phase-0-baseline` was created from **`Guns` (2579a6a)**, not `master` (7ff533a). `Guns` is the active development branch and already contains the helper split (`Game/Helpers/*`), the `Ship` partials, projectile collisions, and NaN guards. The analysis behind the plan was done on `master`; see the corrections below.
- `dotnet build` passes (0 errors, 2 pre-existing CS0162 warnings in `CollisionSystem.cs`, caused by `Launcher.PlanetColision` being a `const`).
- Game launch: **not verified by the agent** (no interactive display). The previous session confirmed it launches and renders on this branch.

### Non-production changes made in Phase 0

- `Strange Universe.csproj`: added `<Compile Remove="Tests\**" />` and `<None Remove="Tests\**" />`. This is required because the game project sits at the repo root and globs every `*.cs`, which would otherwise compile the test sources into the game. Build configuration only; no game code changed.
- `Strange Universe.slnx`: added `Tests/Strange-Universe.Tests.csproj`.
- New: `Tests/` (xUnit project, directly references the game project; no MonoGame content pipeline is in use, so linking files was not needed).
- New: `Tests/Data/sample-universe-settings.json` (copied from `bin/Debug/net9.0-windows/Data/universe-settings.json`, because the repo `Data/` folder is not tracked in git).

### Characterization tests (33, all passing)

| File | Covers |
|---|---|
| `HelperCharacterizationTests.cs` | `ProceduralHelpers.SeedHash` (FNV-1a known vectors, null == empty), `NameGenerator.GenerateCelestialName` determinism for all factions, `NameGenerator.GetStarSystemName` returns `"Sol"` without an active universe, `MathHelpers.WrapAngle` / `StarDeltaAngle` / `SafeNormalize` / `IsFinite` / `AsteroidInterpolatedRadius` |
| `PersistenceRoundTripTests.cs` | Sample save deserializes; deserialize then serialize is JSON deep-equal to the original; `Persistence.Persist` then `LoadExisting` round-trips through a temp file |

Observation: `WrapAngle(5?)` returns `-?` rather than `+?` because of float rounding at the boundary. The test avoids that exact boundary; behaviour is unchanged.

---

### Inventory: `Launcher.*` static usage (production code)

`ActiveUniverse`
- `Game/Entities/GravityWell.cs:37` reads `Player.ShipType.ThrustForce` to scale gravity (**logic depends on the player's ship**).
- `Game/Entities/Player.cs:96` calls `ShowTimedMessage`.
- `Game/Entities/Ship.cs:45` is the `StarSystem` getter, which always returns the *active* system for every ship.
- `Game/Entities/Ship.Navigation.cs:30` calls `JumpRoute.Remove`.
- `Game/Entities/StarSystem.cs:37, 62, 63` are the `Universe` and `ActivePlayer` getters.
- `Game/EventSystem/EventController.cs:65, 70` call `ShowTimedMessage` (line 61 is commented out).
- `Game/EventSystem/ScoutedSystemSpawn.cs:33` calls `ShowTimedMessage`.
- `Game/Helpers/NameGenerator.cs:30` reads `StarSystemNodes` for duplicate-name checks.
- `Game/NavSystem/JumpTask.cs:23, 173` validate the target and call `Generate()` (**a nav task regenerates the universe**).

`TextureCache`
- `Game/Entities/Planet.cs:63`, `Star.cs:72`, `Ship.cs:121, 127`, `StarSystem.cs:416, 419`, `Universe.cs:150, 188`
- `Game/Systems/SpriteRenderer.cs:37, 168, 198, 623`

`GD` (GraphicsDevice)
- `Game/Entities/Nebula.cs:52`, `Planet.cs:62`, `Star.cs:134`, `Ship.cs:120, 126`, `StarSystem.cs:418`
- `Game/Systems/ProjectileRenderer.cs:359, 388`, `RenderService.cs:51`, `SpriteRenderer.cs:24`
- `Game/UI/GalaxyMapRenderer.cs:57`

`Camera`
- `Game/Entities/Player.cs:36` (**the player entity drives the camera**)
- `Game/Systems/SpriteRenderer.cs:39, 95, 170, 200, 235, 315`

Other statics
- `Launcher.RenderService`: `ProjectileRenderer.cs:57, 149`, `SpriteRenderer.cs:17, 39, 95, 170, 200, 235, 315`
- `Launcher.PlanetColision` (const): `CollisionSystem.cs:17, 35`

### Inventory: `Random`

Seeded from `ProceduralHelpers.SeedHash` (deterministic):
- `Planet.cs:55` uses `SeedHash(Id)`.
- `Star.cs:62` uses `SeedHash($"{Id}_{Name}")`.
- `StarSystem.cs:107` (`SystemId`), `:239` (`_Connections`), `:360` (`starId`), `:410` (`_Asteroids`), `:434` (`_BackgroundStars`), `:458` (`_Nebula`).
- `EventController.cs:27` uses `"{SystemId}:Events"`.
- `NameGenerator.cs:28` uses `SeedHash(seed)`.

Seeded from an int argument (deterministic if the caller is):
- `Asteroid.cs:298` uses `new Random(seed)`.
- `Nebula.cs:61` uses `new Random(baseSeed)`.

Unseeded (non-deterministic):
- `StarSystem.cs:500` (`GetSystemEdgeEntryPosition`), `:540`, `:593`, `:623`, `:650` (safe-location helpers).
- `DockTask.cs:26, 48`, `GuardTask.cs:75`.
- `NameGenerator.cs:43` falls back to `Random.Shared` when no `Random` is passed.
- `SystemEvent.cs:12` uses `new Random(new Guid().GetHashCode())`. **Bug:** `new Guid()` is `Guid.Empty`, so this is a *constant* seed, the same for every event instance. Probably `Guid.NewGuid()` was intended. Not fixed here because that would change behaviour.

### Inventory: graphics leaks (outside `Game/Systems` and `Game/UI`)

| File | Types |
|---|---|
| `Launcher.cs` | Color, GraphicsDevice, SpriteBatch, SpriteFont, Texture2D (expected: composition root) |
| `Game/Components/Camera.cs` | SpriteBatch (doc comment only) |
| `Game/Components/ProjectileVisual.cs` | Color |
| `Game/Components/ProjectileVisualStyle.cs` | Color |
| `Game/Components/ShipImpact.cs` | Color |
| `Game/Entities/Asteroid.cs` | Color, GraphicsDevice, Texture2D |
| `Game/Entities/Nebula.cs` | Color, Texture2D |
| `Game/Entities/Planet.cs` | Color, GraphicsDevice, Texture2D |
| `Game/Entities/Ship.cs` | Texture2D |
| `Game/Entities/Ship.Effects.cs` | Color |
| `Game/Entities/Star.cs` | Color, Texture2D |
| `Game/Entities/StarSystem.cs` | Color, Texture2D |
| `Game/Helpers/ProceduralHelpers.cs` | Color (surface/star/nebula palettes) |

`Vector2`, `MathHelper`, `Point` and `Matrix` from `Microsoft.Xna.Framework` are also used throughout the logic layer. These are math types, not graphics, but the logic layer still depends on the MonoGame assembly.

---

### Corrections to the "Read this first" section

1. **The base branch is `Guns`, not `master`.** Several files the plan lists as unread or hypothetical differ on `Guns`: `Ship.Effects.cs` and `Ship.Navigation.cs` exist only on `Guns`; `Game/Helpers/{Persistence,MathHelpers,ProceduralHelpers,NameGenerator}.cs` replaced the single root helper file; `PhysicsSystem` was inlined; the root namespace is unified to `Strange_Universe`. Later phases should be planned against `Guns`.
2. The global-state dependency is broader than `ActiveUniverse/TextureCache/GD/Camera`. `Launcher.RenderService` and `Launcher.PlanetColision` are also static dependencies.
3. **`Ship.StarSystem` always returns the active system**, not the ship's own system. Every ship (including NPCs) implicitly lives in the player's current system. Any later phase that injects a `StarSystem` must preserve this.
4. **Gravity depends on the player's ship.** `GravityWell.cs:37` scales force by `ActiveUniverse.Player.ShipType.ThrustForce` for *all* ships. Removing `Launcher.ActiveUniverse` here needs an explicit gravity scale passed in, or NPC physics will change.
5. `JumpTask` calls `Launcher.ActiveUniverse.Generate()`, so system translation is a side effect inside a nav task, not a Universe-level operation.
6. `Data/` is not tracked in git (`git ls-files Data` is empty); only `Art/` is tracked. The sample save for the tests therefore came from `bin/`.
7. `StarSystem` is not unit-testable yet (confirmed): its constructor path touches `Launcher.TextureCache` and `Launcher.GD`.

### Deferred observations (do not fix in Phase 0)

- `SystemEvent.random` constant seed (see above).
- `Persistence._writeOptions` allows named floating-point literals (`NaN`), so a corrupted state can be saved. `_readOptions` does **not** set `AllowNamedFloatingPointLiterals`, so a save that contains `"NaN"` fails to load and `LoadExisting` silently returns an empty list.
- `Persistence` read and write options differ in enum handling: read accepts camelCase strings, write emits integers.
- `Launcher.PlanetColision` is misspelled and is a `const`, which produces the CS0162 warnings.
- `JumpTask._targetSystemId` is a public field with an underscore prefix.

---

## Phase 1 - Remove global static access

Branch `refactor/phase-1-remove-globals` (from `refactor/phase-0-baseline`).

### Result

`Launcher.ActiveUniverse`, `TextureCache`, `GD`, `Camera` and `RenderService` are no longer public statics; they are now private instance fields. The search for `Launcher\.(ActiveUniverse|TextureCache|GD)` returns no hits outside `Launcher.cs`. The only remaining external `Launcher.` reference is the `const` `Launcher.PlanetColision` in `CollisionSystem`. It is a compile-time constant, not state, and is deferred.

### Changes

- **`GameServices`** (`Game/Systems/GameServices.cs`) holds `GraphicsDevice` and `ProceduralTextureCache`. `Launcher` creates it in `LoadContent` and recreates it in `ClearRuntime`. A `null` `GraphicsDevice` means "no graphics": texture creation is skipped, but all RNG draws still happen, so generation stays deterministic.
- **`StarSystemNode`** has a new constructor, `(seed, position, backConnection, existingNodes)`. The `Universe` property is removed, and the parameterless JSON constructor is kept. `NameGenerator.GetStarSystemName(seed, existingNodes)` replaces the global lookup for duplicate names.
- **`StarSystem`** has a new constructor, `(StarSystemNode, Universe, GameServices)`, and exposes `Universe` and `Services`. `ActivePlayer` is now `Universe?.Player`. `Star` and `Planet` take an optional `GameServices`.
- **`Ship.StarSystem`** is now a settable property that is `null` until attached:
  - Player: attached inside `Universe.ActiveStarSystem` when the system is built. This covers load, `Generate`, and jumps, because `JumpTask` calls `universe.Regenerate()` and then reads `ActiveStarSystem`.
  - NPCs: the new `StarSystem.AddNpc(npc)` sets the system. All four spawn events use it.
- **`Universe.Generate(GameServices)`** stores the services, and **`Universe.Regenerate()`** reuses them. Nebula upload goes through `Nebula.CreateTexture(GraphicsDevice)`. A deserialized `Universe` has no services until `Generate` is called.
- **`GravityWell`** (not in the plan's list, but it read the global): `CalculateForce(position, referenceThrustForce)`. `StarSystem.CalculateGravityAtLocation` passes the player's thrust, which keeps the previous rule that every ship's gravity is scaled by the player's ship.
- **Camera**: `Player.Update` records `CameraTarget` at the exact point where it used to call `Launcher.Camera.Update` (after its physics step, before collisions). `Launcher` then calls `Camera.Update(Player.CameraTarget, ...)` after `Universe.Update`, so framing is identical.
- **Renderers** (`SpriteRenderer`, `ProjectileRenderer`, `RenderService`, `GalaxyMapOverlay`, `GalaxyMapRenderer`) receive `GraphicsDevice`, `GameServices`, `RenderService` and `Camera` through their constructors.

### Tests

38 tests pass, including the new `StarSystemConstructionTests`:
- The first node is `Sol`.
- A `StarSystem` can be built without graphics.
- Generation is deterministic for the same seed, including generated connections.
- `ActiveStarSystem` attaches the player.
- `AddNpc` attaches the system.

The test was not blocked by Phase 2, because a `null` `GraphicsDevice` is supported.

### Deviations

- Steps 2-7 are one commit, because the constructor and signature changes ripple through `Launcher` and do not compile independently. Step 1 is its own commit.
- Step 5 (Universe) was done together with step 3, because the new `StarSystem` constructor depends on it.
- The IDE auto-added `Tests\StarSystemConstructionTests.cs` to the game csproj. That change was reverted; the `Tests\**` exclusion remains.

### Manual smoke test

**Not performed by the agent** (no interactive display). Please verify:
1. Load an existing save. The world renders and flight works.
2. Create a new universe. The same seed gives the same Sol layout.
3. Use the galaxy map to pick a route, then jump. You arrive in the new system, fuel goes down, and the route entry is removed.
4. On entering a system, NPCs spawn (Defended/Scouted messages appear and patrollers move).
5. A merchant arrives, docks, and jumps out.
6. Projectiles hit NPCs and asteroids.
7. Exit to the menu and re-enter. Textures are recreated.

### Deferred

- `Launcher.PlanetColision` const is still referenced from `CollisionSystem`.
- A ship loaded from JSON has a `null` `StarSystem` until the first `Universe.ActiveStarSystem` access. `Universe.Update` does this first.

## Phase 5 - Launcher cleanup and screens

### Result

`Launcher.cs` went from 522 to 104 lines. It now only creates and disposes the shared resources and forwards `Update`/`Draw` to a `ScreenManager`.

### Changes

Added:
- `Game/Screens/ScreenManager.cs` (`IScreen`, `ScreenManager`)
- `Game/Screens/ScreenContext.cs` (shared resources, navigation callbacks, rect/text helpers)
- `Game/Screens/KeyTracker.cs` (detects new key presses; reset in `OnEnter`)
- `Game/Screens/MenuScreen.cs`, `NamingScreen.cs`, `PlayingScreen.cs`
- `Game/Systems/WorldRenderer.cs` (layer order)
- `Game/Systems/DebugRenderer.cs` (gravity wells; new: asteroid/player/NPC hitbox circles)
- `Game/Systems/GameSettings.cs` (`ShowDebug`, `PlanetCollision`)

Modified:
- `Launcher.cs`: rewritten. `GameState`, `debug` and `PlanetColision` were removed.
- `RenderService`: owns a shared 1x1 `Pixel` and is now `IDisposable` (SpriteBatch + pixel).
- `SpriteRenderer`, `GalaxyMapRenderer`: use `RenderService.Pixel`. `DrawDebugCircle` was moved to `DebugRenderer`. `GalaxyMapRenderer.Dispose` no longer disposes the shared SpriteBatch.
- `CollisionSystem`: reads `GameSettings` (planet collision still off by default).
- `StarSystem`, `Universe`: settings are passed through `[JsonIgnore] Universe.Settings`, so nothing serialized changed.
- `Persistence`: parse failures are logged (stderr + `Trace`) and the bad file is copied to `<file>.corrupt-<timestamp>.bak`. Save and remove refuse to overwrite a file that can't be parsed. Writes go to a `.tmp` file that then replaces the original. The file format is unchanged.

Resource lifetime: `LoadContent` calls `CreateResources()`, and `UnloadContent` calls `DisposeResources()`. Each play session's resources are created in `PlayingScreen.OnEnter`. `PlayingScreen.OnExit` saves the universe and disposes them. Both exit-to-menu and closing the window go through `OnExit`.

### Tests

65/65 pass. One test was added: `CorruptFile_IsBackedUp_AndNotOverwrittenBySave`.

### Manual smoke test

**Not performed by the agent** (no interactive display). Please verify:
1. Menu: Up/Down/Enter/Delete work, and Esc quits.
2. Naming: typing and backspace work, Esc goes back to the menu, and Enter creates the universe and starts it.
3. Play: Esc returns to the menu, and the same Esc press does **not** also quit from the menu.
4. Galaxy map: M opens it, and Esc, M or the close button closes it. Closing it with Esc does **not** also exit to the menu.
5. Leave play and re-enter (same or a different universe). Textures are recreated and nothing crashes on dispose.
6. Close the window during play. The universe is saved.

### Open questions

- Every save and delete still rewrites the whole file. Avoiding that would mean changing the file format (for example, one file per universe). Do you want that?
- `ShowDebug` has no key binding. Should one be added (for example F3)?
- Hitbox circles in debug mode are new (they only show in debug mode).
- This file has no Phase 2-4 sections; those phases were only reported in chat.
- Resolves the Phase 1 deferred item: `Launcher.PlanetColision` is gone.
