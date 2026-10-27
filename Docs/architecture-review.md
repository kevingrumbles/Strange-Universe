# Architecture review: classes to eliminate or consolidate

Branch `refactor-architecture-review`. Method: a top-down pass over every folder of `Game/` (about 11,800 lines, 148 types: 125 classes and records, the rest enums and interfaces),
reading the small types first and checking each suspect's real usage (grep over `Game/` and `Tests/`). Nothing is changed yet; this is the list to choose from.

## The shape today

| Folder | Types | Notes |
|---|---|---|
| `Components` | 25 | A grab bag: ship stats parts, equipment, event bus, interfaces, camera, input state, physics. Three sub-folders (`ShipComponents`, `EquipmentComponents`, `PlanetComponents`) are well formed; the files left in the root are not one thing. |
| `Entities` | 27 | The world: universe, star systems, planets, ships (`ShipParts`). Ship is a facade over five part classes. |
| `Systems` | 18 | Rendering and assets. `SpriteRenderer` alone is 773 lines. |
| `UI`, `Screens` | 15 | Overlays and screens; the galaxy map is three classes, the spaceport two plus a helper. |
| `NavSystem` | 17 | One task per behavior; healthy. |
| `EventSystem` | 7 | Four event classes around one controller. |
| `Helpers`, `Simulation` | 14 | Math, procedural tables, saving, two collision systems. |

## Findings, by what they would remove

Estimated effect is in types removed. Risk: **low** = mechanical and covered by tests, **medium** = touches several call sites or tests, **higher** = changes how several classes fit together.

### 1. Dead and pass-through types (low risk, 5 types)

- **`NullEventBus`** and **`NullMessageSink`** are never used (the only mentions are their own definitions). Delete.
- **The message stack is three types for "post a string".** `IMessageSink`, `BusMessageSink` (publishes `MessageRequested`) and `HudMessageService : IMessageSink` (which `Generate` subscribes to the bus through a `messages` parameter).
  `Universe.Messages.Post(...)` already just publishes an event. Replace with `Universe.Post(text, seconds)` that publishes `MessageRequested`, have `HudMessageService` subscribe to the bus itself, and drop the interface, `BusMessageSink`, `NullMessageSink` and the `messages` parameter of `Generate`.
  Touches: `Player`, `SpaceportService` (2), `StarSystem.PostMessage`, `PlayingScreen`, and one test sink in `EventBusTests`. **Removes 3 types** (with the Null one: counted above).
- **`WeaponBonuses`** (a record struct used by one method of `WeaponSystem`): return three values from `WeaponSystem` directly. 1 type, trivial.

### 2. The asset stack (low to medium, 3 types)

`ArtLoader` (a static class with one method), `ProceduralTextureCache` (a `Dictionary<string, Texture2D>` with disposal) and `GameServices` (a holder of the cache and an `AssetService`) all serve `AssetService`.
Fold the dictionary and the PNG loading into `AssetService`, make it `IDisposable`, and let `PlayingScreen` and `SpriteRenderer` hold it directly (`SpaceportOverlay` calls it instead of `ArtLoader`). **Removes 3 types.** The only extra care: `IAssetRequests` stays (it is the test seam), and `AssetService` already implements it.

### 3. The event classes (medium, 6 types)

`SystemEvent` (abstract), `EventType` (an enum nothing reads), `NpcTestEvent`, `DefendedSystemSpawn`, `ScoutedSystemSpawn` and `MerchantMissionEvent` are four small spawn recipes with a static probability field each.
`DefendedSystemSpawn` and `ScoutedSystemSpawn` contain the same "build patrol points and queue a `PatrolTask`" loop word for word; `NpcTestEvent` and `DefendedSystemSpawn` both create guards.
Turn them into methods of `EventController` (`SpawnDefended`, `SpawnScouted`, `SpawnNpcTest`, `MerchantMission`) with one shared `SpawnPatrol(world, random, count)`, and the probabilities as constants. **Removes 6 types** and the duplicated loop.
Costs: `NavDeterminismTests`, `WorldContextTests` and `TestModeEventTests` refer to `NpcTestEvent.EventProbability` and the event classes, so they change; the random-stream order (`NextEventRandom`) must be kept exactly so seeded systems still play out the same way.

### 4. Position, rotation and physics boilerplate (medium, net about 2 types and about 100 lines)

`Star`, `Planet`, `Asteroid` and `Ship` (through `ShipPhysics.Transform`) each wrap a `Transform` with the same 20 lines of `Position`/`Rotation`/`Scale`/`Forward` pass-through properties.
Make one base class (a `WorldBody`) that holds position, rotation, scale and `Forward`, and derive the four from it; `Transform` goes away. `PhysicsBody` (velocity, angular velocity, mass, force, integrate; used only by `Asteroid` and `ShipPhysics`) can move into the same base,
which then also removes `Transform`/`PhysicsBody` plumbing from `ShipPhysics`. **Net: 2 types out, 1 in, about 100 duplicated lines gone.** This is the largest simplification by code size and the one that touches the most files (`Ship`, `ShipPhysics`, `Asteroid`, `Planet`, `Star`, `Steering`, tests that read `Physics.Body`).

### 5. HUD and screen collaborators (low to medium, 4 types)

- **`HudRenderer`** is one 15-line method that draws the `HudMessageService` text. Move it into `SpriteRenderer.DrawHud`, which already draws the rest of the HUD with the same font and batch. 1 type.
- **`WorldRenderer`** (53 lines) is the draw order of the world: sprites, projectiles, targeting brackets, debug. It is called from one place; make it a private `DrawWorld` in `PlayingScreen` (or a method of `SpriteRenderer`). 1 type.
- **`PlayerDeathMonitor`** (29 lines) subscribes to `ShipDestroyed` to set one flag; `PlayingScreen` already subscribes to `PlayerDocked`. Keep the flag and the subscription in `PlayingScreen`. 1 type; `PlayerDeathTests` use the class directly and would test the screen's behavior through the universe instead (or keep a tiny static helper).
- **`GalaxyMapInput`** (91 lines: hover state, hit testing, route clicking) is used only by `GalaxyMapOverlay`, which is already the controller. Merge it into the overlay. 1 type. (`GalaxyMapRenderer`, 469 lines, stays separate.)

### 6. Saving (medium, 1 type)

`FileSaveService` forwards each method to `Persistence` (static: path resolution, read/write options, load, persist, remove), and `SaveServiceExtensions` adds two helpers on `ISaveService`. Move the `Persistence` bodies into `FileSaveService` (the JSON options stay reachable for tests and `ArtLoader`'s path helper moves with the assets).
`ISaveService` stays (the tests' in-memory fake). **1 type**, and one place to read for how saving works. Touches `PersistenceRoundTripTests` and `CreditsTests`, which use `Persistence._readOptions` directly.

## Looked at and kept

- **The interfaces with one real implementation** (`IAssetRequests`, `IWorldContext`, `IEventBus`, `ISaveService`, `IScreen`) each have a test fake or several implementations; they are the test seams the nav and docking tests rely on.
- **`ShipStats` parts** (`FlightStats`, `DurabilityStats`, `ShipAppearance`, `LoadoutLimits`) and **`Spaceport`/the service classes**: small by design and chosen deliberately in the earlier refactors.
- **The ship part classes** (`ShipPhysics`, `ShipDurability`, `ShipSensors`, `WeaponSystem`, `ShipNavigator`): each has a real job and tests. The only change suggested is in point 4.
- **`SystemSpatialQueries`** next to `StarSystem`, and `StarSystemGenerator` + `StarSystemLayout`: the pure-generation split is what keeps the golden tests possible.
- **`ProceduralHelpers`** (360 lines) is a grab bag (noise, planet colors, nebula tables, galaxy directions, `SeedHash`); splitting it would add classes, so it is only noted. The enums `Direction`, `PlanetType`, `OriginFaction` and `CelestialNameType` live inside unrelated helper files; moving them is a file-placement tidy, not a class change.
- **`SpriteRenderer`** (773 lines) is big but not a duplicate of anything; shrinking it means more classes, the opposite of this review. Point 5 adds a little to it on purpose.

## Suggested order

1. **Point 1** (dead types, the message stack, `WeaponBonuses`): safest, removes 5 types, shrinks `Generate`.
2. **Point 2** (assets): removes 3 types, no behavior change.
3. **Point 5** (HUD renderer, world renderer, death monitor, map input): 4 small merges, each independent.
4. **Point 3** (events): 6 types and a real duplication, with the random-stream order to preserve.
5. **Point 6** (saving).
6. **Point 4** (the shared body base class): biggest, last, on its own commit.

Done in that order: about **22 types removed** (148 to about 126) and several hundred lines, with the tests updated as each step goes. Each step is its own commit and runs the full suite.

## Done

All six points were carried out, in this order, each as its own commit with the full suite passing (401 tests):

| # | Commit | What was done | Types |
|---|---|---|---|
| 1 | Refactor 1 | `NullEventBus`, `IMessageSink`, `BusMessageSink`, `NullMessageSink` removed: `Universe.Post(text, seconds)` publishes `MessageRequested` and `PlayingScreen` forwards it to the HUD (`Generate` lost its `messages` parameter). `WeaponBonuses` became a named tuple. | -5 |
| 2 | Refactor 2 | `ArtLoader`, `ProceduralTextureCache` and `GameServices` folded into `AssetService` (it keeps the textures by id, loads art with `LoadArt`, and is `IDisposable`). | -3 |
| 3 | Refactor 3 | `HudRenderer` became `SpriteRenderer.DrawMessage`; `WorldRenderer` became `PlayingScreen.DrawWorld`; `PlayerDeathMonitor` became `Player.IsDestroyed` (the hull never recovers once it reaches zero, so the flag was the same thing); `GalaxyMapInput` merged into `GalaxyMapOverlay`. | -4 |
| 4 | Refactor 4 | `SystemEvent`, `EventType` and the four event classes became static methods of `EventController` with one shared `SpawnPatrol`. A before/after run of the controller over 5 systems, with and without test mode, gave identical ships, names, messages and random-draw counts. | -6 |
| 5 | Refactor 5 | `Persistence`, `ISaveService` and `SaveServiceExtensions` became one abstract `SaveService` (with `CreateUniverse` and `ReloadInto`) and `FileSaveService` (which now holds the file reading and writing). | -2 |
| 6 | Refactor 6 | `Star`, `Planet` and `Asteroid` derive from `Transform` instead of each wrapping one with 20 lines of pass-through properties. | 0 (about -75 lines) |

**Result:** 148 types down to 128 (20 fewer); `Game/` is about 450 lines shorter and the tests about 30. The plan expected about 22 types; two fewer came out because point 6 turned out smaller than estimated
(see below) and because `Persistence` and the save extensions became one type, not two.

**Point 4 as built, and why it is smaller than planned.** `ShipPhysics` has to keep its own position, rotation and velocity (its tests build one without a ship), so `Ship` still holds a `Transform` and a `PhysicsBody` through it. A shared base class for ships as well would have meant
changing those tests and the physics class's ownership; the stars, planets and asteroids were the duplicated part, so only they changed. `Transform` was kept as the base (rather than adding a new `WorldBody`), which is why this step removes lines but not a type.

**Not done, still open:** `ProceduralHelpers` (a 360-line grab bag), `SpriteRenderer` (773 lines) and the enums that sit in unrelated helper files are as they were; splitting them would add types, which was not the goal of this review.
