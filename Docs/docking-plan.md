# Docking feature plan (branch `Docking`)

Goal: some planets have a spaceport. A player who is nearly stopped and close enough to a planet with a spaceport can dock. Docking
clears the active star system, saves the player's location in that system, and shows an interactive UI for the spaceport.

## What the code does today (the parts this touches)

- `Planet` is generated from a seeded `Random` (`SeedHash(planetId)`); the order of its draws is load-bearing for the golden tests,
  so anything new must not change those draws. Planets are not saved; only the player is (`Player.CurrentStarSystemID`, `Player.Location`, velocity, equipment).
- `Universe` owns the one `ActiveStarSystem` (built by `EnterSystem(node)`, a fresh system every time); `ActiveStarSystem` throws when none is set.
  `Universe.Update` and `PlayingScreen` (update, draw, projectile particles) all go through it.
- `PlayingScreen` is the session owner (`OnExit` saves and disposes the universe). Overlays such as `GalaxyMapOverlay` take over input and drawing
  while open. A second screen would dispose the universe on the switch, so docked mode has to live inside `PlayingScreen`.
- Input is `InputState` (built by `InputHandler`); one-press actions use `WasPressed`. The player acts on it in `Player.HandleJump`, `HandleTargeting` etc.
- `DockTask` is the NPC "fly to a planet and hold" behavior. It is unrelated to this feature.

## Design

**The planet owns the docking check.** `Planet.CheckDocking(Ship ship)` returns `Ok`, `NoSpaceport`, `OutOfRange` or `TooFast`. Two hard-coded constants on `Planet`:
`DockRange` (distance from the planet's *surface*, so it works with planet collision on or off) and `MaxDockSpeed` (a flat speed in units per second: stopped or almost stopped,
not a fraction of the ship's top speed). `StarSystem.FindDockingPlanet(ship)` returns the nearest planet the ship can dock at (or the reason it cannot, for the message).
The check lives on `Planet` only; nothing about it is in `Spaceport`.

**Spaceport.** A plain data class, `Spaceport` (in `Game/Components`, no graphics types), held as a nullable property `Planet.Spaceport` (null = no spaceport). It holds a short
`Description` and the list of services the planet offers. It has no name of its own: the planet's name is the name everywhere (HUD hint, UI title). It does not look at ships or decide anything about docking. A service is its own small class derived from
`SpaceportService` (a `Name` and what happens when the player picks it, acting on the player), so the spaceport's contents grow by adding classes, not by changing the UI.
First services: **Refuel** (fuel to full), **Repair** (hull to full; the shield is not its job, see below) and **Undock**. Later: equipment store, ship store, quests.

**Which planets have a spaceport.** Never a gas giant. Every other planet type has a chance, rolled from its own seeded `Random` (`SeedHash($"{planetId}_spaceport")`, not the planet's
`Random`), so existing generation, the golden tests and every saved universe are untouched.

**Docked state.** `Universe.Dock(planet)`: records the planet, ends the player's nav tasks, zeroes velocity, publishes `PlayerDocked`, sets the active star system to null (and
`Player.StarSystem` to null), and **saves immediately**. `Player.Location` and `CurrentStarSystemID` are already part of the save, so the position in the system is what gets stored.
`Universe.IsDocked` and `Universe.DockedAt` expose the runtime state. `Universe.Undock()`: re-enters the saved system (`EnterSystem` with the player's node), puts the player back at the
saved location at rest, clears the dock, publishes `PlayerUndocked`. Nothing in the old system survives a dock (NPCs, projectiles, asteroid debris); it is rebuilt on undock, like a jump arrival.

**Shields on entering a system.** The player's shield is always recharged to full whenever they load into a system: `Universe.EnterSystem` refills every shield generator (a small
`Player.RechargeShields()` / shield refill on the ship's durability). That covers loading a save, undocking, and arriving by jump, so shield damage never needs a repair and Repair only deals with the hull.

**Persistence.** Nothing new is saved. Docked is runtime-only, so **loading a save starts the player in the ship**, in the saved system at the saved location (nearly stopped, since that is how they docked),
not in the spaceport UI. Quitting while docked saves like any other exit and loads the same way.

**Input and prompts.** `InputState.Dock` (one-press, key **`L`**), `Player.HandleDock`: dock if the planet's check says Ok, otherwise a HUD message ("Too fast to dock", "Nothing to dock with in range").
While eligible, a HUD hint ("L: dock at <planet name>") shows.

**Spaceport UI.** A `SpaceportOverlay` in `Game/UI` (same pattern as `GalaxyMapOverlay`): it is open exactly while `Universe.IsDocked`, intercepts all input, and draws over a plain background:
the planet's name as the title, the spaceport's description and a keyboard menu (Up/Down, Enter) built from its services. `PlayingScreen` skips the star-system update/draw and the projectile particles while docked,
and ignores the map and flight keys. Escape behaves as it does in flight (back to the menu, which saves).

## Steps

1. **Spaceport data.** `Spaceport`, `SpaceportService` with Refuel, Repair and Undock, `Planet.Spaceport`, the seeded roll in generation (no gas giants). Tests: same seed gives the same planets with spaceports; golden tests unchanged; gas giants never; not every other planet has one (and some do).
2. **The check.** The constants, `Planet.CheckDocking`, `StarSystem.FindDockingPlanet`. Tests: nearly stopped and in range docks; too fast, too far, no spaceport refused; range measured from the surface; boundaries use the constants, not copied numbers.
3. **Input and player.** `InputState.Dock` (`L`), `InputHandler`, `Player.HandleDock`, messages, HUD hint. Tests for `HandleDock` outcomes (messages posted, no dock when refused).
4. **Universe docked state and shields.** `Dock`/`Undock`, `IsDocked`, events, immediate save, and the shield refill in `EnterSystem`. Tests: shields are full after entering a system (load, undock, jump arrival); (existing tests that set a shield to 0 and then enter a system, or rely on a damaged shield surviving a system entry, are adjusted); the active system is cleared and `ActiveStarSystem` is guarded; location and system id kept and saved at the moment of docking; a load after docking starts in the system, not docked; undock restores the system and position at rest; docking twice and undocking while not docked are safe.
5. **Spaceport UI.** `SpaceportOverlay`, `PlayingScreen` docked mode, guards around every `ActiveStarSystem` use. Not unit tested (graphics); the menu's selection and activation logic and the three services are plain code with tests (Refuel fills fuel, Repair fills hull, Undock leaves the dock).
6. **Cleanup.** Architecture test still passes (no graphics types in `Spaceport`), docs updated, a manual check list (dock, refuel, repair, undock, quit while docked, reload).

## Risks and notes

- `ActiveStarSystem` is used in `PlayingScreen` and `Universe.Update`; the docked path must avoid it everywhere or it throws. The step 4 tests and a search for its uses cover this.
- Rebuilding the system on undock restarts event spawns (NPCs). That is intended ("clear active star system").
- Planet gravity could make "nearly stopped" hard near a well. Gravity is off in the nav work for now; the two constants are the tuning points.
- Refuel and Repair are free for now; there are no credits yet. A price would be added to the service when an economy exists.
- Both `DockTask` (NPCs) and the new docking use the word "dock"; the new types are named `Spaceport` / `Docked...` to keep them apart.

## Decided

- Flat dock speed threshold (stopped or almost stopped); hard-coded range and speed on `Planet`.
- The planet owns the docking check; `Spaceport` is just a property of the planet and does not talk to ships.
- Gas giants never have a spaceport; every other planet type has a chance.
- Dock key is `L`.
- Docking saves immediately; a loaded save starts in the ship, not in the spaceport UI.
- First services: Refuel, Repair, Undock. Repair restores the hull.
- Shields are always recharged to full when the player loads into a system.
- The spaceport has no name; the planet's name is used.

## Still open (defaults in bold)

1. The numbers: **range 300 from the planet's surface, max dock speed 5 units/s**, and **a 50% chance** that a non-gas-giant planet has a spaceport.
