# Docking, credits and spaceports (as built)

One document for the Docking branch; it replaces the two plans it grew from. Everything below is built and has tests, except the drawing and key handling of the
spaceport screen, which is only compiled (see the check list at the end).

## Planets and docking

- **Spaceports.** `Planet.Spaceport` is plain data (a description and a list of services) or null. A planet that is not a gas giant has one with probability `Planet.SpaceportChance` (50%),
  rolled from its own seeded `Random` (`SeedHash($"{id}_spaceport")`) so planet generation and saves are unchanged.
- **The check.** `Planet.CheckDocking(ship)` is the only rule: needs a spaceport, within `Planet.DockRange` (300) of the planet's *surface*, moving no faster than `Planet.MaxDockSpeed` (5, flat).
  It returns `DockingCheck` (Ok, NoSpaceport, OutOfRange, TooFast). `StarSystem.NearestSpaceport(position)` finds the nearest planet with a spaceport at any distance.
- **Targeting with `L`.** No spaceport targeted: `L` targets the nearest ("Targeted X" / "No spaceport in this system"). One targeted (`Player.DockTarget`, runtime only, forgotten in another system): `L` runs the check and docks, or says "Too fast to dock" / "X is out of docking range".
  The ship target (Tab, R) and the landing target are separate. The landing target and the galaxy-map route share one HUD slot (the last set is shown); opening or closing the map with a route selected calls `Universe.TargetJumpRoute()`, which drops the landing target (not a targeted ship).
  The HUD hint follows the target ("L: dock at X" / "Slow down to dock at X"). The main display draws chamfered corner brackets (a faint outer line and a brighter inner one) around a targeted ship or planet, sized to its sprite.
- **Docked state** (`Universe`, runtime only). `Dock(planet)` ends nav tasks, stops the ship, clears the active star system (`ActiveStarSystem` throws while docked), publishes `PlayerDocked` (the playing screen saves on it: docking is the only save point besides quitting).
  `Undock()` rebuilds the system and puts the player back where they docked, at rest. Nothing new is saved, so a loaded game starts in the ship. Entering any system refills the shields.
- **The screen.** `SpaceportMenu` (selection logic) and `SpaceportOverlay` (drawing, with the planet's picture from `Art/` faded into the panel: `SpaceportArt.PathFor`, `EdgeFade`) live inside `PlayingScreen`;
  the keys are `InputState.MenuUp/MenuDown/MenuSelect` (arrows or W/S, Enter), Escape goes back one level and then to the main menu.

## Credits, limits and trading

- **Credits.** `Ship.Credits` (saved; old saves read 0) with `CanAfford`, `TrySpend`, `Earn`. A new player starts with `Universe.StartingCredits` (1000). An NPC gets credits when made: its type's `TypicalCredits` (Shuttle 100, Runner 250) times 0.5-1.5, rolled from its id (`Nonplayer.RollCredits`).
- **Grade.** `EquipmentComponent.Grade` and `ShipStats.Grade` (1 and up) say how advanced something is; the stores sort by it (below).
- **Prices and limits.** `EquipmentComponent.Price`, `ShipStats.Price` (the ship alone) and `ListPrice` (ship plus its default loadout). `ShipStats.Limits` (`LoadoutLimits`): the most items of each kind and the most equipment mass
  (Shuttle 1 of each kind, mass 450; Runner 2 weapons, 2 utilities, 1 of the rest, mass 900). Each kind of equipment (its class; all weapons are one kind) names its own limit with `SlotLimit(limits)`; an unknown item has none.
  `Ship.CheckInstall` returns `InstallCheck` (Ok, NoSlot, TooHeavy); `Install` adds only if it fits. Limits are checked only when adding: a default loadout or a loaded save is never trimmed. Equipment mass is a payload limit; it does not change flight.
- **Trading** (`Universe`, docked only, `bool` results, refusals say why through `Messages`). `BuyEquipment(item)`: credits, then fit. `BuyShip(type)`: costs `ListPrice` minus `Player.ShipValue` (ship plus everything it carries); a negative cost pays the player.
  The new ship replaces the old through `AssignShipType` (default loadout, full hull/shield/fuel), keeping position, name and credits. `SellEquipment(item)` pays the item's price. `ShipCostAfterTradeIn(type)` is the number shown.
- **Services and stores.** A `SpaceportService` has a `Name`, `IsAvailable(universe)` and `Perform(universe)`: Refuel (only while fuel is missing), Repair (only while the hull is damaged), Equipment, Starships, Undock.
  A store (`OpensRows`) also has `Rows` and `Choose`. `EquipmentBayService` and `StarshipBayService` each keep a `Stock` list (the catalogs for now) and give a buyer `Clone()` of the stock item (`EquipmentComponent.Clone`, `ShipStats.Clone`).
  Each store keeps its stock sorted: equipment by `EquipmentType` (projectile weapons, beams, missiles, shields, plating, engines, utilities), then grade, then price, the highest grade last; ships by grade. The "Sell X" lines use the same order.
  Equipment rows show grade (`G1`), price, mass and `[carried/limit]`, then the sell lines; ship rows show grade, list price, trade-in and cost. `SpaceportMenu` holds the open store and `Back()` leaves it.

## Numbers to tune

Dock range 300, dock speed 5, spaceport chance 50%, starting credits 1000, NPC credits, every price, and the limits above. The prices are placeholders; nothing earns credits yet.

## Manual check list

1. Dock at a planet with a spaceport (`L` to target, fly to it, `L` again when slow and close). The panel shows "Credits: 1000"; the list reads Equipment, Starships, Undock (Refuel and Repair only when needed).
2. Equipment: a wide panel, one line per item with price, mass and `[carried/limit]`, the highlight scrolls. Try a full slot ("No free slot for ..."), too few credits, over the mass limit ("... is too heavy for this ship"), then sell an item.
3. Escape in a store returns to the list with the store highlighted; Escape on the list goes to the main menu.
4. Starships: Runner from a Shuttle (credits drop by the cost shown, new loadout, new art after undocking); Shuttle from a Runner (credits rise).
5. Undock and dock again; quit while docked and load: ship, equipment and credits as left, flying in space.
6. Targets: Tab/R with a planet targeted keeps both; opening the map with a route replaces only the planet target; the brackets fit the sprite at different zooms.
