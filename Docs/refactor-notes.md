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

---

# Round 2

Plan: `REFACTOR_PLAN_ROUND2.md`. Baseline `56138e4`. All work is on branch `Refactor-Round-2` (no per-phase branches, at the maintainer's request).

## Round 2 / Phase 0 - Test seams and baselines

### Files read (from the plan's "not reviewed" list)

`ProceduralTextureCache`, `NavTask`/`JumpTask`, `GalaxyGraph`, `StarSystemNode`, `Nebula`, `Persistence`, `Tests/StarSystemGoldenTests.cs`, `Tests/StarSystemConstructionTests.cs`.

### Corrections to the plan

- **1c (texture leak):** `ProceduralTextureCache.Register` already disposes the texture it replaces, so there is no leak. The guard in 1c is still worth adding (it avoids regenerating planet/star textures on every system entry), but it is an optimization, not a leak fix.
- **1b (nebula ids):** confirmed. Pool ids are `$"{Seed}_nebula_pool_{i}"` in both `GenerateNebulaPool` and `GenerateRemainingNebulaPoolAsync`.
- **1a (headless path):** confirmed. With `Assets == null` the generator always draws 15 seeds, which equals the *first-visit* game path but not later visits. "Headless vs game" therefore only differs when palettes already exist, and the test compares headless against the pre-seeded fake.
- **Phase 0 step 4 (idempotent expansion):** constructing the same system twice does **not** change universe/galaxy state today. `GenerateConnections` stops once the node already has its target connection count. The test passes, so there is no known issue to carry into Phase 4. It now guards against regressions.

### Changes

Added:
- `Tests/Fakes/RecordingAssetRequests.cs`: records calls and mimics `AssetService.EnsureAsteroidTexture` (seed drawn only for new palette ids). It can be pre-seeded with all 15 palette ids.
- `Tests/GenerationCharacterizationTests.cs`:
  - `Fake_DrawsSeedOnlyForNewPalettes` (passes)
  - `AsteroidLayout_IndependentOfVisitOrder` (skipped, bug, Phase 1a)
  - `AsteroidLayout_HeadlessMatchesGamePath` (skipped, bug, Phase 1a)
  - `NebulaSelection_IndependentOfPoolFill` (skipped, bug, Phase 1b)
  - `ConstructingSameSystemTwice_DoesNotChangeUniverse` (passes)

Production (test seams only, no behavior change):
- `Nebula.CreateWithoutPixels(id)` (internal): a nebula with an id and no 4096x4096 pixel generation, used to fill the pool cheaply in tests.
- `Strange Universe.csproj`: `InternalsVisibleTo Strange-Universe.Tests`.

### Tests

69 passed, 3 skipped (72 total). The three skipped tests were temporarily un-skipped and confirmed to **fail** against the current code, so they reproduce the bugs.

### Manual smoke test

None needed. There is no production behavior change.

### Open questions

- None blocking Phase 1. Phase 1a changes asteroid appearance and layout for existing universes (approved in the plan); golden values for asteroid fields will be regenerated and listed.

## Round 2 / Phase 1 - Determinism fixes

### Changes

- `IAssetRequests.EnsureAsteroidTexture(string paletteId, int seed)` replaces the `Func<int> nextSeed` overload. `AssetService` and the test fake were updated.
- `StarSystemGenerator.GenerateAsteroids`:
  - Always draws one value per palette slot (15) from `asteroidsRng` before placement, whether or not a texture is created.
  - Palette texture seeds are now `SeedHash($"{universe.Seed}_asteroid_tex_{i}")`, so palette art is the same no matter which system creates it first. The per-system stream is used for placement only.
- `StarSystemGenerator.SelectNebula`: picks `index = rng.Next(Universe.NebulaPoolSize)` and sets `NebulaId = Universe.NebulaPoolId(seed, index)`. It no longer reads `NebulaPool`, so the choice doesn't depend on timing and `NebulaId` is never null.
- `Universe`: `NebulaPoolSize` is now `public const`. New `Universe.NebulaPoolId(seed, index)` is the single place the id format lives (format unchanged).
- `AssetService.EnsurePlanetTexture`/`EnsureStarTexture`: `TryGet` guards, matching `EnsureShipArt`. As noted in Phase 0, `Register` already disposed the replaced texture, so this only avoids regenerating textures on each system entry. No leak existed.
- 1b.3 renderer guard: `SpriteRenderer.DrawNebula` already returns early when the texture isn't in the cache, so no change was needed. A system whose nebula is still generating shows no nebula until it is uploaded. It then appears on the next frame without needing a re-entry.
- 1c.3: `AssetService` needs a real `GraphicsDevice`, so it isn't unit-tested. Its behavior is covered through the fake.

### Behavior changes

- **Asteroid layout:** for a system generated when *no* palette textures existed yet (the first system of a launch), the layout is **unchanged**. That path already drew 15 values. For systems entered later in a launch, the layout changes: it previously skipped the 15 draws and now matches the first-visit layout. Net result: each system now always has the layout it would get as the first system of a launch.
- **Asteroid appearance:** palette textures are now seeded from the universe seed, so asteroid art differs from before in every universe.
- **Nebula:** a system may show a different nebula than before (selection was `Next(pool.Count)` against a partially filled pool). It is now stable across launches.
- No serialized properties changed. Saves load as before.

### Golden data

**No golden values changed.** The golden tests run headless (`Assets == null`), and the old headless path already drew 15 values, the same as the new code. `StarSystemGoldenData.cs` is untouched. The golden fingerprint doesn't include `NebulaId`.

### Tests

74/74 pass. The three Phase 0 bug tests were un-skipped and now pass. New tests:
- `PaletteSeeds_DependOnUniverseSeedOnly`
- `NebulaId_IsAlwaysSet_EvenWithEmptyPool`

### Manual smoke test

**Not performed** (needs a display). Please verify:
1. New game: asteroids and nebula render.
2. Jump A -> B -> C, then start a new launch and go A -> C -> B. C's asteroid layout should match.
3. Immediately after launch, jump before all nebulae finish. The nebula appears once uploaded, with no crash.

### Open questions

- Should the golden fingerprint include `NebulaId` going forward?

### Follow-up (maintainer request): wait for the nebula instead of drawing without it

- `Universe` now holds one `Task<Nebula>` per pool slot. `GenerateNebulaPool()` starts any slot not already started, so calling it again on each jump/`Regenerate` no longer starts duplicate loops (this also covers Phase 2 step 2).
- Entering a system (`ActiveStarSystem` creating the system) calls `WaitForNebula(NebulaId)`. It blocks on that slot's task and uploads it on the main thread before the system is returned, so a system is never drawn without its nebula. Other finished slots are still uploaded each frame in `Update`.
- Headless (`_assets == null`), the wait is skipped.
- Removed the old "first nebula synchronous" path and the `ConcurrentQueue`.
- **Behavior change:** if a system is entered before its nebula has finished generating (mainly right after launch, or a fast jump early in a session), the game pauses until it is ready. Previously it showed no nebula until it was ready (Phase 1) or picked a different one (before Phase 1). Startup cost is about the same as before: one nebula, as before.
- Test: `EnteringSystem_WaitsForItsNebula`. This test generates real nebula pixels, so the full test run now takes about 28 s.

### Follow-up: load the current system's nebula first

- New
- `Universe.GenerateNebulaPool()` works out the player's system the same way `ActiveStarSystem` does (current id, then Sol, then the first node) without creating anything. It starts that nebula first and starts the other five only after it finishes, so they don't compete for CPU while the player waits on entry.
- `StartNebula` is now thread-safe (`Interlocked.CompareExchange`), because the remaining slots are started from a continuation.
- Test: `NebulaIndexFor_MatchesSelectedNebula`. 76/76 pass.
- Note: the editor's stale copy of `GenerationCharacterizationTests.cs` re-added the Phase 0 `Skip` attributes twice. They were removed again and checked before committing.

Test: `NewUniverse_PredictsStartingSystemNebula`. 77/77 pass.

## Round 2 / Phase 2 - Lifecycle correctness

### Changes

- `Universe.ActiveStarSystem` no longer builds systems lazily. It throws until a system has been entered.
- `Universe.EnterSystem(node)` is now the only way to enter a system. `Generate` and `JumpTask` both use it, and `JumpTask` no longer calls `Regenerate()`.
- `ResolveStartNode()` picks the starting node before entry, so its nebula is requested first.
- Nebula generation uses one task per pool slot, so no loop is ever started twice. Added a `NebulaFactory` test seam and `WaitForAllNebulae()`.
- `NullAssetRequests.Instance` replaces the `null` asset checks. Removed the parameterless `StarSystem()` constructor.
- Ships and players not in a system (`StarSystem == null`) no longer throw from `Update`, `FireWeapons` or jump input (`Player.HandleJump`).
- Housekeeping: checked that there are no duplicate `using` lines in `StarSystem.cs` or `Universe.cs`. `Universe.cs` uses only the XNA `Vector2`.

### Tests

- New `UniverseLifecycleTests`.
- Removed `ActiveStarSystem_AttachesPlayer`, which tested the old lazy getter. `Generate_EntersStartingSystem` now covers that behavior.
- 83 pass, 3 skipped.

### Manual smoke test

**Not performed.** Please verify: new game, load a save, and several jumps in a row.

## Round 2 / Phase 3 - Task completion belongs to the task

### Changes

- `NavTask.OnCompleted()` (virtual, no-op by default). `ShipNavigator.Update` calls it once when the active task reaches `Complete`, then clears the task.
- `JumpTask.OnCompleted()` now holds the route removal and fuel decrement, with the same condition as before (owner is in the target system).
- `JumpTask._targetSystemId` is now private; `TargetSystemId` is a public read-only property.
- `ShipNavigator.cs` no longer names any task type.

### Behavior changes

None.

### Tests

New `NavTaskCompletionTests`: a completed jump in the target system removes the route entry and uses one fuel; a completed jump elsewhere and an invalid jump change nothing. 90/90 pass, 0 skipped.

Note: the three Phase 0 bug tests in `GenerationCharacterizationTests.cs` were still marked `Skip` in the committed code, although the Phase 1 notes say they were un-skipped (the stale editor copy mentioned in Phase 1 appears to have re-added them). They are now un-skipped in their own commit and pass.

### Manual smoke test

**Not performed** (needs a display). Please verify: set a route on the galaxy map, jump. The route entry disappears and fuel drops by one on arrival; a merchant NPC still jumps out.

## Round 2 / Phase 4 - Pure generation

### Changes

- New `StarSystemLayout` (immutable record): derived parameters, the initial stars, planets, asteroids and background stars, and `NebulaIndex`. The entity instances are handed to the `StarSystem` built from the layout, so a layout is consumed by one system.
- `StarSystemGenerator.Generate(node, nebulaPoolSize)` returns a layout. It has no reference to `Universe`, `Galaxy` or `IAssetRequests`, and the order of Random draws is unchanged. `NebulaIndexFor` now takes the pool size.
- `StarSystem` takes a layout (`new StarSystem(node, universe, layout, assets, random)`). Its generated properties are read-only and the constructor has no side effects. Internal `StarSystem.Create(...)` generates a layout and builds the system, and `AttachAssets()` requests textures.
- `Universe.EnterSystem` now does, in order: create the system, expand the galaxy (only when `!node.Discovered`, which it then sets), attach assets, make it active, attach the player, wait for the nebula.
- `StarSystemNode.IsHome` (`[JsonIgnore]`, derived from `HomeName = "Sol"`) replaces the string checks.
- Deviation from the plan: `Generate` has no `universeSeed` parameter. The only seed-dependent parts (palette texture seeds, nebula id) are now built outside the generator.

### Behavior changes

None. Golden data is untouched. The golden fingerprint helper and a few tests now enter the system through `Universe.EnterSystem` (which does the galaxy expansion) instead of constructing a `StarSystem` directly.

The order of asset requests changed: star textures, then planet textures, then the 15 asteroid palette textures, instead of being interleaved. Seeds and results are the same.

### Tests

New `PureGenerationTests`: equal layouts for the same node, no node/universe mutation during `Generate`, Sol overrides via `IsHome`, galaxy expanded only on first entry, `IsHome` not serialized. The idempotence test now enters the same system twice. 95/95 pass.

### Manual smoke test

**Not performed** (needs a display). Please verify: new game, jump through a few systems (new neighbours appear on the galaxy map), load a save and revisit a system.

### Open questions

- Saves where a node is `Discovered` but has fewer connections than its target will no longer be topped up on re-entry. New saves can't reach that state; say if you want an always-top-up instead.

## Round 2 / Phase 5 - Simulation/presentation boundary leftovers

### Changes

1. **Projectile visuals.** `Projectile` no longer has `Visual`, `BurstAge`, `IsBursting`, `BurstProgress` or `TryConsumeBurstEmission`. It keeps `HasHit`, `HitAge` (renamed from `BurstAge`) and a plain `ImpactLingerSeconds`, and `IsExpired` depends only on those. `Equipment.ImpactLingerSeconds` (not serialized) replaces `Equipment.ProjectileVisual`; each preset's value equals its old burst duration (0.16, 0.26, 0.22, 0.10, 0.34, 0.20; default 0.18). `ProjectileVisual` and `ProjectileVisualStyle` moved to `Game.Systems`, with a new `ProjectileVisuals.For(weaponName)` lookup. `ProjectileRenderer` derives burst progress from `HitAge / BurstDuration` and tracks the one-shot spark emission itself (a pruned `HashSet<Projectile>`).
2. **Nebula without `Color`.** New `Game.Helpers.Rgba` struct; `ProceduralHelpers.NebulaColorPool` and `Nebula` use it. Pixels are byte-identical (new `NebulaPixelTests` hashes the full buffer of a fixed nebula, recorded before the change).
3. **Namespaces.** `CollisionSystem` and `ProjectileCollisionSystem` moved to `Game.Simulation`; `GameSettings` moved to `Game.Components`. `StarSystem` and `Universe` no longer import `Game.Systems`.
4. **`SimulationContracts.cs` split** into `IAssetRequests.cs` (with `NullAssetRequests`), `IMessageSink.cs` (with `NullMessageSink`) and `BackgroundTile.cs`.
5. **Architecture tests** (`ArchitectureTests`): a source scan of `Game/Entities`, `Components`, `EventSystem`, `NavSystem` and `Simulation` for `Texture2D`, `GraphicsDevice`, `SpriteBatch`, `SpriteFont` and `Color` (comments stripped), and for `using` of graphics, `Systems`, `UI` or `Screens` namespaces. A grep-style check was used instead of NetArchTest to avoid a new dependency. The plan listed `ProjectileVisual`/`ProjectileVisualStyle` and `Nebula` implicitly; they were the only violations.

### Behavior changes

None intended. Impact linger times equal the old burst durations, and projectiles for weapons without a dedicated visual (e.g. missiles) keep the default look.

### Tests

100/100 pass (new: nebula hash, architecture, weapon linger equals burst duration, projectile expiry).

### Manual smoke test

**Not performed** (needs a display). Please fire each weapon and compare impact bursts (flash, sparks, duration) and hit effects on ships against before.

### Open questions

- `Light Missile` has no dedicated `ProjectileVisual`, so it uses the default look as before; say if it should get its own.
