# Nav System Review

Branch `Nav-System-Review`, from `master` after Round 2. This is analysis only: no production code has been changed yet.

Scope: `Game/NavSystem/*` (`NavTask`, `JumpTask`, `PatrolTask`, `GuardTask`, `DockTask`, `SpawnTask`), `ShipNavigator`, and how `Ship`, `Player` and `Nonplayer` feed them.

## How it works today

- A ship owns a `ShipNavigator`: a FIFO queue plus one `ActiveTask`. `Ship.Update` calls `Navigator.Update` (only when the ship is in a system), then applies gravity and integrates physics.
- A task is a state machine: `Update(dt)` steers by calling `Owner.RotateTowards` and `Owner.ApplyThrust`, or by writing velocity and position directly (`JumpTask`). When `CurrentState` reaches `Complete` the navigator calls `OnCompleted()`; `Invalid` just drops the task.
- Tasks reach the world through `NavTask.World` (`IWorldContext`).
- Who queues what: `Universe` queues a `SpawnTask` for a new player. `Nonplayer` queues `SpawnTask`, or a `JumpTask` starting at `SystemTranslation` when it jump-spawns. Events queue `Dock`, `Jump`, `Patrol` and `Guard`. `Player.HandleJump` queues a `JumpTask` on the J key.

## Measured behavior (throwaway headless simulation, not committed)

NPC `Shuttle` (max speed 580, thrust 360, rotation 3.5 rad/s) in the real generated Sol system, stepping `Ship.Update` at 60 Hz.

| Task | Result |
|---|---|
| **Dock** | **Bug.** On the first `HoldPosition` frame the ship's position becomes NaN and the physics guard resets it to the system origin `(0,0)` with zero velocity. The ship then sits at the centre for the rest of the hold and completes there. See finding 1. |
| **Guard** | **Does not hold its orbit.** Asked for a 437-unit orbit around a planet of radius 218, it settles at 1100-1564 units (average 1334), at speed 676 against a max of 580. |
| **Patrol** | Works. Reaches every waypoint, but always at full speed (580) with no slowdown, and legs are very long (waypoints 21,000-25,500 units apart in a 19,656-radius system: 36-46 s per leg). |
| **Jump arrival (NPC)** | Works as designed: about 4,800 at 0.5 s, 580 at about 4.7 s, ending at 9,812 from the centre (half the system radius). |

## Findings

### Defects

1. **`DockTask` divides by zero.** `gravityStrength = gravityForce.Length() / (ThrustForce * GravityWell.GravityThrustRatio)`, and `GravityThrustRatio` is `0f` (`GravityWell.cs:16`). So `0 / 0 = NaN`, which flows into `ApplyThrust` and corrupts velocity and position. The `Integrate` guard hides it by teleporting the ship to the origin. Every merchant that docks hits this. It only appears when a correction is needed during hold, which is almost always. Also note that gravity is effectively disabled game-wide right now (ratio 0), so all gravity compensation in the tasks is dead code.
2. **`GuardTask` cannot orbit.** It thrusts at full power along the tangent with only a crude radial correction. Without speed control, the turning circle of the ship (speed squared over lateral acceleration) is far larger than the requested orbit.
3. **`PatrolTask` brakes ineffectively.** It applies forward thrust when aligned and then, in the same frame, calls `RotateTowards` a second time for "retrograde". The second call overrides the first and the ship swings back and forth rather than braking. In practice the slowdown never happens. It also uses a stale `distanceToTarget` after advancing to the next waypoint.
4. ~~**Player input fights the autopilot.**~~ **Withdrawn (my error).** The manual rotation, retrograde, thrust and jump handlers in `Player` already return while a nav task is active; I read `Player.Update` and missed those guards. What remains is only that there is no way to cancel a jump (the chosen rule is to ignore manual flight input while jumping, which is the current behavior).

### Structure

5. **One global `TaskState` enum** (15 values) is shared by every task. `Attacking` is unused, `Alerting` is an empty stub, and nothing prevents a task being put in a state it never handles. `CurrentState` is publicly settable, so anything can force a task to `Complete`.
6. **The same steering code is copied across tasks**: "rotate toward X, thrust when aligned" appears about a dozen times with different ad-hoc thresholds (0.05, 0.1, 0.15, 0.2, 0.3), and "brake: face retrograde then thrust" is repeated in `Jump`, `Dock` and `Patrol`. There is no shared steering layer (seek, arrive, brake, hold, avoid).
7. **`JumpTask` is a 250-line, 7-state machine** that mixes autopilot (steer and brake) with state-teleporting (writes velocity, position and rotation directly), so it behaves differently from the others and is hard to test.
8. **The navigator is minimal.** There is no `Cancel`, `Clear` or `Interrupt`, no priorities and no way to suspend a task (for example, to react to an attack and then resume). `Invalid` has no hook (`OnFailed`), and `OnCompleted` is not called for tasks that fail. Enqueuing a task whose `Owner` is a different ship is not rejected.
9. **Snapshot targets.** `DockTask` and `GuardTask` store a world position at creation. That is correct while planets are static, but not if anything moves.
10. **Unseeded randomness.** `DockTask` and `GuardTask` use `new Random()`, so NPC behavior is not reproducible, unlike generation and `EventController`, which are seeded.
11. **Behavior gaps.** Guard never ends, never reacts to threats, and has no attack. Patrol with no waypoints completes instantly (the scouted-system event posts a message and still queues it). Nothing persists nav state across a save.

### Tests

12. Only `JumpTask` completion and arrival, and `SpawnTask`, have tests. Dock, Guard and Patrol have none, which is how the NaN in finding 1 went unnoticed.

## Proposed order of work

1. **Fix the NaN** (small, high value): guard the division, and add a regression test. Decide separately whether gravity should stay disabled; if it is re-enabled, DockTask needs a pass.
2. **Simulation test harness**: step a ship plus a task in a fake or real world for N seconds, and assert on position, speed and completion. Add tests for Dock, Guard and Patrol that fail today (Dock teleport, Guard radius).
3. **Shared steering helpers** (`SteeringHelper` or a `Steering` component): seek, arrive-with-braking, hold-position and obstacle avoidance. Rewrite Patrol (proper arrival slowdown), Guard (speed-controlled orbit) and Dock on top of them.
4. **Navigator**: `Cancel`/`Clear`, `OnFailed`, a rule for player input while a task is active (ignore, or cancel the task), and owner validation.
5. **Per-task states** instead of the global enum, with `CurrentState` read-only outside the task.
6. **Seeded randomness** for Dock and Guard (derive from owner id plus system id).
7. **Optional**: split `JumpTask` into steering phases and a scripted arrival; add real Alert/Attack behavior for Guard.

Items 1 and 2 are independent and safe to do first. Items 3 to 5 change behavior and should be done one at a time, with the harness in place.

## Progress

Gravity is switched off on purpose while the nav system is reworked (`GravityThrustRatio = 0`), so the gravity-compensation code is left as it is and the tests describe the no-gravity mode.

### Steps 1 and 2 done

- **Dock NaN fixed.** `DockTask` now treats a zero gravity reference as zero gravity strength instead of dividing by zero. The ship no longer teleports to the system origin when it starts holding.
- **Simulation harness**: `Tests/Fakes/NavSim.cs` (a real generated system, one NPC, a fixed 60 Hz step, `Run(seconds, until, onStep)`).
- **Tests** (`Tests/NavBehaviorTests.cs`): Dock never jumps, completes near the planet and holds station; Patrol visits waypoints in order and completes with none; Guard does not drift away. The Dock tests failed before the fix.
- **Known defects recorded as skipped tests** (to be un-skipped by the step that fixes them): Patrol does not slow for a waypoint; Guard does not hold the requested orbit radius; Guard approaches head-on and passes through the planet (new finding 2b, seen while writing the tests; planet collision is off by default, so it is not visible in play).

Results: 126 pass, 3 skipped.

### Step 3a done: `Steering` and Patrol

- **`Steering`** (`Game/NavSystem/Steering.cs`): `Seek` (turn, thrust when aligned), `Brake`, `AllowedApproachSpeed` (the speed from which the ship can still slow to a target speed in a given distance, from the ship's real acceleration), `Arrive` and `AvoidObstacles` (moved out of `PatrolTask`). New `Ship.MaxAcceleration`.
- **`Arrive` uses velocity matching**: it steers so the velocity converges on "direction x allowed speed". A first version that only gated thrust by speed left the ship circling a waypoint at about 800 units for 80 s at corner speed, because nothing cancelled its sideways velocity. Planning also allows for the time to turn around before braking can start.
- **`PatrolTask` rebuilt on it.** Behavior changes: the ship now slows to 40% of max speed (`CornerSpeedFraction`) at each waypoint and then accelerates toward the next; the distance to the next waypoint is recomputed after advancing (it used to be stale); a waypoint also counts as reached when the ship came within twice the arrival radius and is moving away (`PassByFactor`). `CurrentWaypointIndex` is public.
- **Tests**: `Patrol_SlowsDown_ForAWaypoint` is un-skipped and passes; new `Patrol_LongRealisticRoute_KeepsMoving` (waypoints from a real system, every leg under 80 s) and `SteeringTests`.
- Still to move onto `Steering`: Guard (3b), Dock (3c), and the arrive/brake code in `JumpTask`.

Results: 133 pass, 2 skipped (the two Guard defects).

### Step 3b done: Guard

`GuardTask` rebuilt on `Steering`. Behavior changes:

- **Approach:** the ship aims at the point of the orbit ring facing it and slows to orbit speed on the way (`Steering.Arrive`), instead of flying at full speed at the target's center. It starts orbiting when it is within the orbit radius plus `EntryMargin` (300) of the target.
- **Orbit:** the ship matches velocity to "tangent at orbit speed plus a radial correction" (`RadialGain`). Orbit speed is `min(0.5 x max speed, sqrt(0.5 x acceleration x radius))`, so the engine can always supply the centripetal acceleration (`OrbitSpeedFor`). Direction is unchanged (counterclockwise).
- **No randomness:** the `new Random()` for the "on top of the target" case is gone; it uses a fixed direction.
- **Measured** (Shuttle, orbit radius 437 around a planet of radius 218; start 2,000 away, 9,000 away, inside the ring, on the ring): orbiting within 5 to 16 s; steady radius 464 (about 6% above the request, constant) and steady speed 280; the approach never came closer than 445 to the target (old behavior: orbit radius 1,100 to 1,564 at speed 676, and a pass within 6 of the planet's center).
- **Not done:** guards do not steer around other stars and planets while approaching (the old guard did not either).
- **Tests:** the two skipped Guard tests are un-skipped (tolerance tightened to 10%); new theory over four starting positions; orbit-speed bound test.

Results: 140 pass, 0 skipped. Still to move onto `Steering`: Dock (3c) and the arrive/brake code in `JumpTask`.

### Step 3c done: Dock

`DockTask` rebuilt on `Steering`. Behavior changes:

- **Approach:** the ship flies to a hold point `SafeDistance` (500) from the target on the side it comes from (fixed on the first update), using `Steering.Arrive` with arrival speed zero. It no longer heads for the target's center, and it brakes in time instead of overshooting. The approach ends within `HoldArrivalDistance` (100) of the hold point at no more than 10% of max speed.
- **Hold:** the same `Arrive` aimed at the hold point, with a tighter velocity tolerance (`HoldVelocityTolerance`, 1% of max speed). The gravity-compensation code is gone: velocity matching corrects any drift, gravity included, so the task works with gravity on or off.
- **Hold duration:** still 15 to 60 s by default, but derived from the owner's id instead of `new Random()`, so it is reproducible; a ship exactly on the target picks a fixed side instead of a random one.
- **Measured** (Shuttle, planet radius 163; starts 5,000 east, 6,000 north-west, 700 and 1,200 away): reaches the hold in 2.6 to 17 s at speed about 53, never closer to the planet than 490 (hold distance is 500), stays within about 55 of the hold point, and completes exactly `HoldDuration` after the hold starts. Before: the ship passed through at 232 and, with the NaN, was reset to the origin.
- **`Steering.Arrive`** takes an optional velocity tolerance.
- **Tests:** theory over four starting positions (stops at the hold point without overshoot, hold lasts as long as asked), hold point is on the arrival side, default duration is in range and reproducible.

Results: 146 pass, 0 skipped. Still to move onto `Steering`: the arrive/brake code in `JumpTask`.

### Step 3d done: `JumpTask` pre-jump phases (step 3 complete)

The three autopilot phases now use `Steering`; the scripted parts (jump acceleration, system translation, arrival) are unchanged.

- **MoveToMandeville:** flies straight out from the system center by matching an outbound velocity (`Steering.Seek` on the velocity error), so any sideways drift is cancelled instead of kept. Exactly at the center it still faces right.
- **Decelerate:** `Steering.Brake`, down to the same 50 units/s as before.
- **AlignForJump:** `Steering.Seek` with thrust off, same 0.05 rad tolerance.
- **Bug fixed in Jump:** the jump accelerated along the ship's *velocity*, not its facing. The brake stops at 50 units/s in any direction, so a ship with a small leftover drift would jump sideways (90 degrees off its facing) or even backwards (a ship drifting inward at 45 crossed the whole system, 12.4 s instead of 2.2 s). It now accelerates along the facing it aligned to. **Behavior change.**
- **Timings** (Shuttle, system radius 19,656, Mandeville 14,742) are unchanged from before, within 0.4 s: from rest near the center 30.1 s, from the center 31.9 s, inward at full speed 10.2 s, outside Mandeville at rest 2.6 s, sideways at full speed 25.3 s (was 24.9 s; it now flies out radially instead of carrying its drift).
- **Tests:** phase order and speed-at-jump-start over four starts; jump leaves along its facing from an inward drift (the old bug); a sideways start still leaves radially.

Results: 152 pass, 0 skipped. Remaining from the review: step 4 (navigator cancel and failure hooks, player input while a task is active), step 5 (per-task states), step 6 (seeded randomness; Dock and Guard are already done), step 7 (optional).

### Step 4 done: navigator control and hooks

- **Decision:** manual input is ignored while the autopilot is flying a task (no cancel key). This is what `Player` already did for rotation, retrograde, thrust and jump; finding 4 above was my misreading, now withdrawn. Weapons, targeting and the map are not blocked. A test locks the behavior in (all flight keys held for 60 frames during an active task: rotation, velocity and the jump queue are unchanged; with no task the keys work).
- **`ShipNavigator`:** `Enqueue` rejects a null task or one that belongs to another ship; new `QueuedCount`, `CancelActive()` (drops the active task, the next queued one starts on the next update) and `Clear()` (drops everything). Cancel and clear run no hooks.
- **`NavTask.OnFailed()`**: called once when a task reaches `Invalid`, before it is cleared (a counterpart of `OnCompleted`). An invalid task no longer needs special handling by callers, and it does not stop the queue.
- No production code uses `CancelActive` or `Clear` yet; they are for AI and events (for example, a guard that must drop its task when threatened).
- **Tests:** `NavigatorTests` (queue order, ownership checks, cancel, clear, hooks run exactly once, invalid task does not block the queue, player input).

Results: 161 pass, 0 skipped. Remaining: step 5 (per-task states), step 6 (seeded randomness; Dock and Guard already done), step 7 (optional).

### Step 5 done: per-task phases, read-only status

The shared `TaskState` enum and the public `CurrentState` setter are gone.

- **`NavTask.Status`** (`NavTaskStatus`: `Running`, `Complete`, `Invalid`) is what the navigator reads. Only the task can change it, through the protected `Complete()` and `Fail()`; once a task has finished its status is final. `IsFinished` is a shortcut. The navigator no longer calls `Update` on a task that has already finished.
- **Each task with stages has its own enum and a read-only `CurrentPhase`:** `JumpTask.Phase` (`MoveToMandeville`, `Decelerate`, `AlignForJump`, `Jump`, `SystemTranslation`, `ArriveInSystem`), `DockTask.Phase` (`Approach`, `Hold`), `GuardTask.Phase` (`Approach`, `Orbit`). `PatrolTask` and `SpawnTask` have no phases. The unused `Attacking` and `Alerting` values (and the empty patrol alert branch) are removed.
- **A task can only be in phases it handles**, and no outside code can force one to finish.
- **`JumpTask`:** the starting phase is now a constructor argument (`startPhase`, used by NPCs that jump into a system). A player's target is validated on the first update against the world the task actually runs in, instead of in the constructor against the owner's own system; an unknown or missing target fails the task then (same visible result: the task is dropped and `OnFailed` runs).
- **Tests** moved to the new API, plus: status is not publicly settable and `CurrentState` no longer exists, finished status is final, finished tasks are not updated, running tasks are, the lazy jump validation (unknown target, no target, NPC without a target), and every task starts in its first phase.

Results: 169 pass, 0 skipped. Remaining: step 6 (seeded randomness; Dock and Guard already done, check what is left), step 7 (optional).

### Step 6 done: seeded randomness

What was left after Dock and Guard (see step 3) was in the events and in how NPCs are placed.

- **Events had one shared, constant seed.** `SystemEvent.random` was `new Random(new Guid().GetHashCode())`, and `new Guid()` is always `Guid.Empty`, so every event started the same stream (a merchant always chose the same planet slot, and so on). `EventController` now gives each event a generator seeded from the controller's own stream (which is seeded from the system id), so a system's events always play out the same way. Behavior change: the event sequence of a system differs from before (it consumes one extra draw per event); it was not stable between builds anyway.
- **Bug fixed:** the patrol-point loops evaluated `random.Next(3, 6)` in the loop condition, so the bound was re-rolled every iteration and the number of waypoints was erratic. The count is now decided once.
- **The world takes a generator.** `IWorldContext.GetRandomSafeLocationOutsideAsteroidBelt`, `GetSystemEdgeEntryPosition` and `GetSafeEntryTransform` (and the spatial queries behind them) take an optional `Random`. Events pass theirs, so patrol routes are reproducible; with none, the system's own generator is used as before.
- **NPC placement is reproducible.** `NavTask.RandomFor(purpose)` seeds a generator from the ship id, the system id and the purpose. `SpawnTask` (spawn position) and `JumpTask` (arrival at the system edge) use it for NPCs. For the **player** it returns null on purpose, so the player's spawn and arrival spots keep their variety (no visible gameplay change for the player).
- **Not changed (not nav decisions):** weapon spread (`Equipment`, a static `Random`), particle and impact visuals, the unseeded fallback `Random` inside `SystemSpatialQueries` and `NameGenerator.GenerateCelestialName`'s `Random.Shared` fallback (both only used when a caller passes no generator).
- **Tests** (`NavDeterminismTests`): the same system replays the same events and messages; every event hands its own generator to the world; the patrol-point count is decided once; NPC spawn and jump-in arrival positions are reproducible in a real system (same seed and ship id, same spot; a different ship id, a different spot); the player gets no seeded generator.

Results: 177 pass, 0 skipped. Remaining: step 7 (optional: real alert and attack behavior for Guard; split `JumpTask` into steering phases and a scripted arrival).

### Step 7a done: `JumpTask` split into stages

`JumpTask` is now an orchestrator over `PrepareJumpTask` (flown), `JumpDriveTask` and `ArrivalTask` (scripted), with no behavior change (identical timings, all earlier tests pass unchanged). Details: [nav-jump-split.md](nav-jump-split.md). Results: 194 pass, 0 skipped. Remaining from step 7: real alert and attack behavior for Guard.

### Step 7b done: alert

NPCs go on alert when shot: the shooter is flagged hostile, the previous task is stored, and its disposition decides the response (aggressive tasks hunt and attack; defensive tasks fight only if a hostile is in range, otherwise leave the system). The alert ends when no hostile is left, and the previous task resumes. NPCs inherit weapons and shields from their ship type, so they are now armed and can be destroyed. Details, decisions and open questions: [nav-alert.md](nav-alert.md). Results: 224 pass, 0 skipped.

### Test mode and the NPC test event

- **`Launcher.TestMode`** (public static bool, default `false`). It is copied into `GameSettings.TestMode` when a universe is played, and `IWorldContext.TestMode` exposes it to the event system (so the simulation does not reference `Launcher`).
- **`EventController.SpawnEvent`**: `NpcTestEvent` is the first case of the system spawn roll. Its share is 100% in test mode (every system spawn is the test event, so the "discovered" messages and the normal patrol or guard spawns do not happen), otherwise `NpcTestEvent.EventProbability`.
- **`NpcTestEvent.EventProbability` is now 0.** It used to be 100, which would have made the test event happen all the time in normal mode once it was enabled in the roll. At 0 a normal game behaves exactly as before (tested: identical spawns and messages for 60 systems, and no extra random draw). Raise it to let the event appear in normal play.
- `EventController.NpcTestProbabilityOverride` overrides the share (used by tests).
- The test event spawns one armed, jump-in NPC per planet, each docking for a very long hold, which makes them convenient targets for the alert behavior.
- **Tests:** `TestModeEventTests` (test mode: every spawn is the test event across 100 systems, one NPC per planet, nothing else; normal mode: never with the default, about half at 50%, always at 100%, identical to before at 0; an explicit probability wins over test mode; the setting reaches the world).
- **Nav tests no longer depend on the live ship tuning.** The Shuttle's stats were retuned (max speed 580 to 58, hull 1 to 30, shield 1 to 15) and 15 nav and combat tests that hard-coded the old speeds failed. They now run on a fixed `TestShuttle` ship type (`Tests/Fakes/TestShips.cs`, the Shuttle's old numbers, registered before the tests run).
