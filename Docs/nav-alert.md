# Alert: how NPCs react to being shot

Branch `Nav-System-Review`. An NPC that is hit by a projectile goes on **alert**. The task it was carrying out decides how it responds.

## The rules

1. **Trigger.** When an NPC takes a hit and survives, `Nonplayer.OnAttacked(owner of the projectile)` runs. A ship's own shots and shots with no owner are ignored; a killing shot does nothing (there is nothing left to alert). Only NPCs go on alert, not the player.
2. **The attacker is flagged hostile** on the NPC that was hit: `Ship.Hostiles`, `IsHostileTo`, `MarkHostile`, `ForgetHostile`. Hostility is one-way (the NPC's view), per ship, and not saved. Destroyed ships are forgotten.
3. **The previous task is stored.** `ShipNavigator.RaiseAlert()` swaps an `AlertTask` in for the active task and keeps the old one in `AlertTask.Previous` (null if the ship had no task). A second hit during an alert just adds another hostile to the running alert; alerts never nest. Tasks queued behind stay queued.
4. **The previous task decides the behavior** (`NavTask.Disposition`):
   - **Aggressive** (`PatrolTask`, `GuardTask`): seek the nearest hostile and attack it.
   - **Defensive** (everything else: `DockTask`, `JumpTask`, no task): attack only if a hostile is within weapon range; otherwise try to leave the system.
5. **The alert ends** when no hostile ship is left in the system (destroyed, or left). The previous task then resumes where it left off (patrol keeps its waypoint, a dock keeps its hold timer). If the previous task had finished meanwhile (a jump that carried the ship away), the next queued task starts.

## What each response does (`AlertTask`)

| Phase | Who | Behavior |
|---|---|---|
| `Pursue` | aggressive | Closes in on the hostile with `Steering.Pursue`: speed is relative to the target, so it slows in time and settles at 70% of its weapon range, moving along with the target. It shoots on the way if already lined up. |
| `Engage` | aggressive, or defensive with a hostile in range | Gets level with the hostile's speed (aggressive ships also hold the 70% distance), then aims at the **lead point** (shots inherit the shooter's velocity, so the target's motion is taken relative to it) and fires when within 4.6 degrees and 90% of range. |
| `Flee` | defensive with no hostile in range; any unarmed ship | Jumps out of the system. If the ship was already jumping, that jump carries on. A flee that has passed its autopilot stage cannot be interrupted, so a defensive ship that is fleeing fights only while it is still in the autopilot stage. |

An aggressive ship switches from `Pursue` to `Engage` at 90% of its range and back at 100% (hysteresis, so it does not flip). A defensive ship does not chase.

## Tasks that cannot be suspended

`NavTask.CanBeInterrupted`: `SpawnTask` (it places the ship in one update) and a `JumpTask` after its autopilot stage (the drive and the arrival move the ship by script and would leave it at hyperspace speed). An attack during one of these is remembered and the alert starts as soon as the task ends (the alert then has no previous task, so it is defensive).

## Other changes this needed

- **NPCs inherit their loadout and durability from their ship type.** `ShipStats.DefaultEquipment` (the Shuttle: "Light Laser"); an NPC starts with that equipment (separate copies, so cooldowns are not shared) and with hull, shield and fuel at the type's maximums. **Before this, NPCs had no weapons and no hull or shields at all, so they were unarmed and could not be damaged.** This is a gameplay change: NPCs can now be destroyed and can shoot. (The Shuttle's `MaxHull` and `MaxShield` are 1, so it dies from one hit.)
- `Equipment.Create(name)` / `Equipment.CreateWeapon(name)` / `Equipment.Clone()`: separate equipment instances (`FromName` returns the shared preset).
- `IWorldContext.Ships` (the player and all NPCs); `Ship.WeaponRange` and `Ship.ProjectileSpeed`.
- `Steering`: `MatchVelocity`, `Pursue` / `PursuitVelocity`, `LeadDirection`, `AimAt`, `FireIfAimed`.
- `GuardTask` goes back to its approach phase if it ends up far from its orbit (for example after chasing), so it returns to its post after an alert.
- `NavTaskStatus`/`TaskDisposition`: dispositions are properties of tasks; `PatrolTask` and `GuardTask` override to aggressive.

## Measured (Shuttle, aggressive patrol, attacker is the player with a large hull)

| Attacker | First hit | Hits in 60 s | Distance held |
|---|---|---|---|
| stationary, 5,500 away | 13.4 s | 59 | 1,030 to 1,069 |
| moving at 250 | 2.3 s | 77 | 1,109 to 1,203 |
| 400 sideways | 11.5 s | 60 | 1,026 to 1,188 |

A defensive dock with the attacker 700 away shoots back after 0.8 s; with the attacker 3,000 away it jumps out in under 10 s and never fires.

## Decisions and open questions

- Hostility is **per NPC and one-way** (the NPC that was hit flags its attacker). Other NPCs do not take sides.
- No "lose interest": an aggressive ship pursues until the hostile is destroyed or leaves the system.
- NPCs can now shoot the player. The player's hull is 1 for the Shuttle, and nothing handles the player being destroyed yet.
- A hit that kills an NPC raises no alert. An NPC hit by another NPC's shot flags that NPC hostile (there is no friend/foe check on projectiles).
- Defensive "nearby" is the NPC's own weapon range.

## Tests

`AlertTests`, `CombatSteeringTests`, `NpcLoadoutTests` (trigger and bookkeeping, no alert for own/null/killing shots or for the player, second attacker, suspended-task handling, resume with state preserved, queue order, clear, aggressive hunting of stationary and moving attackers, aggressive guard, unarmed flees, defensive flees without firing, defensive shoots back in range and does not chase, a fleeing ship fights when a hostile arrives, lead aiming, match velocity, pursuit settling, guard re-approach).

## Player destroyed

NPCs can now shoot the player, so a destroyed player ends the session:

- `PlayerDeathMonitor` (`Game/Screens`) subscribes to the universe's `ShipDestroyed` events and notes when the player's ship is the one destroyed.
- `PlayingScreen.Update` checks it first, every frame, and returns to the main menu (the same transition as Esc).
- `PlayingScreen.OnExit` does **not** save when the player was destroyed (it also covers the window being closed in the same frame), and instead calls `ISaveService.ReloadInto(ctx.Universes)`, which replaces the in-memory universes with what is on disk. The menu therefore offers the last saved state; progress since the last save is lost.
- A new universe is saved the moment it is created (`ISaveService.CreateUniverse`, used by the naming screen), so it is on disk from the start and is still in the menu after a death in its first session. (Before this, a universe was first saved when you left play, and one that was never saved disappeared.)
- Saves happen only when leaving play normally, so "the last persist point" is the last time you left play, or the last time you quit.

Tests: `PlayerDeathTests` (the monitor flags only the player's destruction, stops when disposed, handles an already-destroyed player and a kill inside a full system update; reload replaces in-memory changes with the saved state, drops never-saved universes, works with both save services). The screen change itself needs a display and was not run.

## Decisions (confirmed)

- **Aggressive ships never lose interest.** They pursue until the hostile is destroyed or leaves the system. There is no give-up time or distance, and none is planned.
- **No friend-or-foe check yet.** Factions do not exist, so projectiles cannot tell friend from foe: any shot that hits an NPC flags its owner hostile, including another NPC's. Revisit when factions are added: that is where a check on `Ship.OnAttacked` (and on who a ship may flag hostile) belongs.
