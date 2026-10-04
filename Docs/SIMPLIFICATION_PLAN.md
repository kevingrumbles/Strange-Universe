# Project Simplification Plan

Goal: reduce structural noise and dead code in `Strange Universe` without changing gameplay behavior.

**Ground rules**
- One phase per commit so any phase can be reverted independently.
- Build after every phase, then run the game (menu -> new universe -> fly -> fire -> open map -> exit).
- Do not mix mechanical changes (namespaces, moves) with logic changes.

---

## Phase 1 - Remove dead code (zero risk)

| Item | Action |
|---|---|
| `Game\Components\InputAction.cs` | Delete. No references; superseded by `InputState`/`InputHandler`. |
| `Camera.SmoothSpeed` | Delete (never read). |
| `StaticHelpers.LoadFile<T>` | Delete (no callers). |
| `Content\` folder, `Content.mgcb` | Delete. No `Content.Load` calls; `FontBuilder` replaced the pipeline. |
| `Launcher`: `Content.RootDirectory = "Content";` | Delete. |
| `csproj`: `MonoGame.Content.Builder.Task` | Remove package reference. |
| `csproj`: `<Folder Include=...>` entries | Remove (redundant). |

## Phase 2 - Inline `PhysicsSystem`
- Replace `_physics.Update(Asteroids, deltaTime)` in `StarSystem.Update` with a `foreach` calling `a.Update(deltaTime)`.
- Remove the `_physics` field and delete `Game\Systems\PhysicsSystem.cs`.

## Phase 3 - Unify the root namespace
- Two roots exist: `Strange_Universe.*` (~34 files) and `StrangeUniverse.*` (~12 files).
- Standardize on `Strange_Universe`; update declarations, `using`, and `using static`.
- Verify no `StrangeUniverse` references remain (excluding `obj`/`bin`).

## Phase 4 - Align folders and namespaces
- `Equipment`, `ShipStats`, `ProjectileVisual`, `ProjectileVisualStyle`, `ShipImpact` -> namespace `Game.Components`.
- Move `GalaxyMapOverlay`, `GalaxyMapInput`, `GalaxyMapRenderer` to `Game\UI\` (namespace `Game.UI`).
- Rule: namespace = folder path.

## Phase 5 - Split the root helper files
Move under `Game\Helpers\` and split by concern:

| New file | Contents |
|---|---|
| `Persistence.cs` | `ResolveDataPath`, `Persist`, `Remove`, `LoadExisting`, JSON options |
| `MathHelpers.cs` | `WrapAngle`, `StarDeltaAngle`, `AsteroidInterpolatedRadius`, `Direction` |
| `ProceduralHelpers.cs` | Noise, nebula density, colour tables, `PlanetType`, `SeedHash` |
| `NameGenerator.cs` | `OriginFaction`, `CelestialNameType`, name generation |

This removes the circular dependency between `StaticHelpers` and `ProceduralHelpers`.

## Phase 6 - Decompose `Ship.cs` (optional)
1. Inline the six one-line `Calculate*` forwarders and remove the `#region`.
2. Extract hit-visual state into `Ship.Effects.cs` (partial class).
3. Optionally extract nav-task handling into `Ship.Navigation.cs` (partial class).

## Phase 7 - Drop `-windows` TFM (optional)
- `net9.0-windows` exists only for `System.Drawing.Common` in `FontBuilder`.
- Replace with a cross-platform font approach, then return to `net9.0`.
- Only pursue if cross-platform support is a goal.

---

## Order and risk

| Phase | Risk | Notes |
|---|---|---|
| 1 Dead code | None | Do first |
| 2 Inline PhysicsSystem | Very low | |
| 3 Single namespace | Low (mechanical) | Largest diff |
| 4 Folder/namespace alignment | Low | After 3 |
| 5 Split helpers | Medium | Behavior must stay identical |
| 6 Decompose Ship | Low-medium | Optional |
| 7 Drop `-windows` | Medium | Optional |

## Done criteria
- Single root namespace; namespace matches folder everywhere.
- No unreferenced types or members from the dead-code list.
- No circular dependency between helper classes.
- Game plays identically (menu, new/load universe, flight, combat, galaxy map, save on exit).