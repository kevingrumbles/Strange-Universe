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
