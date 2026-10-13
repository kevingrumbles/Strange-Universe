# Step 7a: `JumpTask` split into stages

`JumpTask` was one 250-line state machine that mixed autopilot flying with scripted movement. It is now an orchestrator over three stage tasks, each a `NavTask` that can be run and tested on its own.

| Stage | File | Kind | Does |
|---|---|---|---|
| `PrepareJumpTask` | `Game/NavSystem/PrepareJumpTask.cs` | flown (`Steering`) | `MoveToMandeville` (fly straight out, cancelling drift), `Decelerate` (to `SafeJumpSpeed`, 50), `AlignForJump` (face away from the center, tolerance 0.05 rad). Completes when aligned. |
| `JumpDriveTask` | `Game/NavSystem/JumpDriveTask.cs` | scripted | Accelerates along the facing, ignoring the speed limit (exponential, growth 1.5), until beyond the system edge at hyperspace speed. Completes there. |
| `ArrivalTask` | `Game/NavSystem/ArrivalTask.cs` | scripted | `Translate` (the player enters the target system; the ship is placed at the edge, moving inward at 25x max speed, facing inward) then `Taper` (speed eases down to normal at half the system radius). Completes there. |

`JumpTask` (`Game/NavSystem/JumpTask.cs`) is still the one task that the navigator, the player and events queue, cancel and complete, so a jump cannot be left half done by a cancel. It:

- validates a player's target on the first update (unchanged);
- runs the current stage and moves to the next when it completes (the next stage starts on the following update, as the phases did before);
- decides how the jump ends: an NPC is removed after the drive stage and never arrives; a player goes on to arrive;
- keeps `OnCompleted` (using the route entry and fuel when the ship is in the target system);
- keeps its public API: the `Phase` enum, `CurrentPhase` (now derived from the current stage), the `startPhase` constructor argument, `TargetSystemId`, and the `ArrivalSpeedMultiplier` / `ArrivalTaperEndFraction` constants (now forwarded from `ArrivalTask`).
- hands its stages the same world it was assigned (`NavTask.AssignedWorld`), so tests that give a jump a fake world still work.

## Behavior

No change. Measured before and after (Shuttle): phase timestamps and total time are identical in all five scenarios (rest near the center 30.1 s, flying sideways at full speed 25.3 s, inward at full speed 10.2 s, from the center 31.9 s, outside the Mandeville radius at rest 2.6 s). All 177 existing tests passed unchanged on the split.

## Tests

`JumpStageTests` (17 new, 194 total): each stage on its own (prepare reaches a ready state through the three phases; the drive accelerates straight along the facing and does not remove the NPC; arrival enters the target, places the ship, tapers and completes at the halfway point; an NPC's arrival does not switch systems), and the hand-over (every start phase maps correctly; an NPC is removed only after the drive stage and never arrives; a player continues through translation and arrival and only then completes; stages use the assigned world; an unknown target still fails).
