# Nav system review

Review and rework of the nav system (`Game/NavSystem`, `ShipNavigator`), done as a series of small steps. Every step has tests; the plan, findings and measurements are in `Docs/nav-system-review.md`, `Docs/nav-jump-split.md` and `Docs/nav-alert.md`.

## Bugs fixed

- **Dock teleported ships to the system origin.** `DockTask` divided by `ThrustForce * GravityThrustRatio` (0, because gravity is off), producing NaN, which the physics guard then "fixed" by resetting the ship to (0, 0).
- **Guard never held its orbit** (settled at about 3x the radius, at 676 speed against a 580 limit) and approached the planet's centre head-on at full speed.
- **Patrol never slowed down** for waypoints (two `RotateTowards` calls in one frame), and could circle a waypoint for 80 s without reaching it.
- **A jump followed leftover drift instead of the ship's facing**, so a ship drifting inward at 45 jumped backwards through the system.
- **Events shared one constant random seed** (`new Guid().GetHashCode()` is always the same), and patrol-point loops re-rolled their bound on every iteration.
- **NPCs had no weapons and no hull or shield at all** (unarmed and invulnerable).

## What changed

- **`Steering`**: shared flight primitives (seek, brake, arrive with a physically derived speed limit, velocity matching, pursue, lead aiming, obstacle avoidance). Patrol, Guard, Dock and the pre-jump phases are rebuilt on it.
- **Per-task phases and a read-only `NavTask.Status`** (`Running`, `Complete`, `Invalid`) replace the shared 15-value `TaskState` enum. Navigator: ownership check, `CancelActive`, `Clear`, `OnFailed`.
- **`JumpTask` split** into `PrepareJumpTask` (flown), `JumpDriveTask` and `ArrivalTask` (scripted), with the same API and identical timings. Arrival is now faster (25x top speed) and tapers to normal speed halfway to the centre, and faces inward on arrival.
- **Seeded randomness** for events and NPC placement (the player keeps varied spawn and arrival spots).
- **Alert**: an NPC that is shot goes on alert. The shooter is flagged hostile, the previous task is stored and resumed, and its disposition decides the response: aggressive tasks (patrol, guard) hunt and attack; defensive tasks (dock, jump, none) fight only if a hostile is in range, otherwise leave the system. NPCs now inherit weapons and shields from their ship type (`ShipStats.DefaultEquipment`).
- **Player destroyed**: returns to the main menu without saving; the menu offers the last saved state.
- **Test mode**: `Launcher.TestMode` makes the NPC test event always trigger on system spawn; otherwise `NpcTestEvent.EventProbability` applies (now 0 by default, so a normal game is unchanged).
- **Tests follow the ship stats.** Nav tests derive speeds and time limits from `ShipStats`, and `TuningOverride` (`SU_TEST_MAXSPEED`, `SU_TEST_THRUST`, `SU_TEST_ROTATION`) lets a run change the tuning. The suite passes across 14 of 15 combinations tried, which exposed and fixed three behaviors that depended on top speed (guard orbit tolerance, pursuit tolerance, a pursuit turn-around bug).

## Behavior changes (please read)

- **NPCs can be destroyed and can shoot**, including at the player. The player being destroyed now ends the session (no save).
- Arrival speed and heading after a jump (see above).
- Event sequences in a system differ from before (each event now takes one extra random draw).
- Patrols slow to 40% of top speed at each waypoint.
- Without friend-or-foe (no factions yet), any shot that hits an NPC flags its shooter hostile, including another NPC's. Aggressive ships never give up.

## Not verified

Nothing here has been run in the game (no display). The tests simulate real tasks headlessly (241 tests, 0 skipped), but please play it: jump between systems, watch a patrol, a guard and a docking merchant, shoot an NPC, and let an NPC shoot you.

`Game/Components/ShipStats.cs` retuning (hull and shield) is not part of this branch.
