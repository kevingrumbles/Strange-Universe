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
4. **Player input fights the autopilot.** `Player.Update` applies manual rotation, retrograde and thrust before `Navigator.Update`, so keys held during a jump perturb the autopilot, and there is no way to cancel a jump.

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
