# Credits, loadout limits and the spaceport bays (plan)

Goals: ships carry credits (NPCs too, to be looted later); ships have limits on how much and what kind of equipment they carry; a spaceport has an
Equipment Bay to buy equipment and a Starship Bay to buy a new ship, with the current ship and its equipment traded in.

## What the code has today (the parts this touches)

- `Ship.Equipment` is a plain `List<EquipmentComponent>`. Every item already has a `Mass`; nothing limits the count or total mass. `ShipStats` (abstract; `Shuttle`, `Runner`) is
  made of parts: `Flight`, `Durability`, `Appearance`, plus `DefaultLoadout` (objects). `Ship.AssignShipType(type)` already does "replace the ship with this type and give it its default loadout, full of hull, shield and fuel".
- `EquipmentCatalog` builds every item fresh; `EquipmentComponent.Create(name)` is the one lookup from a saved name. A ship's equipment is saved by name; the ship type by name.
- The docked screen: `Spaceport` (description + `Services`), `SpaceportService` (`Name`, `IsAvailable`, `Perform(universe)`), `SpaceportMenu` (a flat list with one highlight; Up/Down/Enter),
  `SpaceportOverlay` (draws it). Services act immediately (Refuel, Repair, Undock); there is no nested screen yet. Escape leaves for the main menu.
- Only the player is saved; NPCs are rebuilt with each system. The player's save already carries any public settable `Ship` property.

## Design

### 1. Credits

`Ship.Credits` (an `int`, default 0, saved with the player because it is a public property). Helpers `CanAfford(amount)` and `TrySpend(amount)` (false and no change when short), `Earn(amount)`.
A new player starts with a set amount (a constant on `Universe`, in the open decisions). **NPCs get credits when they are made:** each `ShipStats` has a typical amount, and an NPC's own amount is
that, varied by a roll seeded from its id (so it is reproducible, like the rest of NPC setup). Nothing spends or takes an NPC's credits yet; looting comes later and will use `Credits` as it is.
The HUD stats area and the spaceport screen show the player's credits.

### 2. Prices and loadout limits (data)

- `EquipmentComponent.Price` (int) set in the catalog beside `Mass`. `ShipStats.Price` (int) on the type.
- Slot kinds need no enum: each kind of equipment is its own class (`WeaponComponent` for all six weapon kinds, `ShieldComponent`, `PlatingComponent`, `EngineComponent`, `UtilityComponent`), and each names the limit that applies to it with `EquipmentComponent.SlotLimit(limits)` (an unknown item has none). Items are counted by class.

- A new `ShipStats` part, `LoadoutLimits` (next to `Flight`/`Durability`): `MaxWeapons`, `MaxShields`, `MaxPlating`, `MaxEngines`, `MaxUtilities` and `MaxMass` (total equipment mass),
  exposed as `ShipStats.Limits`. Each ship type sets its own.
- On `Ship`: `EquipmentMass`, `EquipmentValue` (sum of prices), `ShipValue` (type price + equipment value), `CountOf<T>()` (for example `CountOf<WeaponComponent>()`), and `CheckInstall(item)` returning `Ok`, `NoSlot` (that kind is full) or `TooHeavy` (mass would pass the limit),
  and `Install(item)`/`Uninstall(item)` that apply the check. Limits are enforced only when installing: a loaded save or a default loadout is never trimmed.
- Every ship type's default loadout must fit its own limits (a test over all built-in types, so a new type or a changed loadout cannot break it).
- Equipment mass stays a payload limit only; it does not change flight (a possible later step: add it to the ship's mass for acceleration).

### 3. Buying (rules, no UI)

Kept in `Universe` beside `Dock`/`Undock`. **No new types:** the trade methods take the item or ship type to hand over, which the bay makes with `Clone()` from the stock it keeps; there are no "offer" classes and no result enum. Each method returns `true` when the trade went through; a refusal changes nothing and says why through `Messages` ("Not enough credits",
"No free slot for X", "X is too heavy for this ship"), so whatever lists the stock only has to try the trade.

- **`BuyEquipment(item)`**: must be docked; credits are checked, then `CheckInstall`; on success the price is spent and the item installed.
- **`BuyShip(type)`**: must be docked. Cost = the new type's `ListPrice` (the ship plus the default loadout it arrives with) minus `Player.ShipValue` (the current ship plus everything it carries); a negative cost pays the player. On success the new type replaces
  the old through `AssignShipType` (so it is full and equipped with its **default loadout**; the old equipment is gone, paid for in the trade-in), the art is requested (`EnsureShipArt`), and position, name and credits carry over. `ShipCostAfterTradeIn(type)` is the number to show.
- **`SellEquipment(item)`**: must be docked and carrying it; removes it and pays its price.

### 4. The bays (no new data classes)

- **The spaceport's services are the bays.** `Spaceport` is unchanged (a description and a list of services); two services join Refuel, Repair and Undock: **`EquipmentBayService`** and **`StarshipBayService`**. Each **holds a `List` of the objects it sells**
  (`Stock`: `List<EquipmentComponent>` / `List<ShipStats>`), filled from the catalogs for now (every spaceport sells everything; a per-planet stock later is just a different list handed to the constructor).
- **A buyer gets a clone.** `EquipmentComponent.Clone()` (a copy as if freshly made: a weapon is ready to fire, a shield full, each weapon with its own look) and `ShipStats.Clone()` (a new ship type of the same kind with its own parts and default loadout) make the object
  that is handed over, so the stock never runs out and the shelf object is never installed on a ship.
- **A service with rows.** `SpaceportService` gets `OpensRows`, `Rows(universe)` (lines of text) and `Choose(universe, row)`, all "none" by default. The equipment bay's rows are one line per stock item ("Light Laser   150 cr   mass 50   [1/2]", the last
  being how many of that kind the ship carries against its limit), then "Sell X" lines for what the player carries; the starship bay's rows are one per ship type with its list price, the trade-in and the cost after it. `Choose` clones the stock item and calls `BuyEquipment` /
  `BuyShip`, or calls `SellEquipment`; the trade posts its own message.
- **`SpaceportMenu`** has one extra field, the service that is open (or none). While one is open it shows that service's rows (and heading) instead of the service list; `Back()` closes it (Escape calls it, and only leaves for the main menu when nothing is open).
- **`SpaceportOverlay`** draws whichever list the menu has with the same row style, in a wider panel for a store, as a window of rows that follows the highlight, without the planet picture, with the credits in the header.

## Steps

1. **Credits.** `Ship.Credits` + helpers, new-player amount, NPC credits from the ship type and a seeded roll, HUD and spaceport-screen display. Tests: spend/earn/can't overspend; saved and loaded; NPC credits reproducible and in range; two NPCs differ.
2. **Prices, kinds and limits.** `SlotLimit` on each kind of equipment, `Price` on equipment and ship types, `LoadoutLimits` and the values for `Shuttle`/`Runner`, the `Ship` helpers and `CheckInstall`. Tests: counts and mass per kind; each limit enforced and exactly-at-limit allowed; every built-in type's default loadout fits; unknown items cost nothing.
3. **Purchase rules.** `BuyEquipment`, `BuyShip`, `SellEquipment` (bool results, messages for refusals). Tests: each failure leaves credits and equipment untouched; success spends, installs a separate object; trade-in math (new price minus ship and equipment value, including the case where the trade-in is worth more); the new ship has the default loadout, full hull/shield/fuel, kept position and credits; not docked is refused.
4. **Bays.** `EquipmentBayService` and `StarshipBayService` (each with a `Stock` list), `Clone()` on equipment and ship types, `Rows`/`Choose` on `SpaceportService`, the open-service field and `Back()` in `SpaceportMenu`, the two services added to every spaceport. Tests: the rows (names, prices, what the player can sell), choosing a row buys or sells, Back, and that the old services and menu behave as before.
5. **Overlay.** Draw the open service's rows (the credits are already there); Escape/Back handling in `PlayingScreen`. Drawing only compiled, so a manual check list.
6. **Cleanup.** Architecture tests (no graphics types in the new data), docs, manual check list: buy a weapon, hit the weapon limit, hit the weight limit, buy a ship, check the trade-in, save and reload with the new ship and credits.

## Risks and notes

- **Existing tests** that build ships with the default loadout or count equipment keep working (limits are only checked when installing). Tests that need a certain loadout already use `TestGear`; new limits tests should give the ship their own `LoadoutLimits` rather than depend on the Shuttle's numbers.
- **Saves:** `Credits` is a new field (old saves read 0, so give old saves the starting amount only if the field is missing: `int?` on load or a `Credits` default; decided in the open list).
- **Unknown/retired items** in a save: no slot limit (so never installable), no price, no mass counted, and never removed.
- **Prices need balancing** once there is more than one source of credits; the first numbers are placeholders chosen so the loop can be tested (a new player can afford one useful upgrade).
- A ship's drawn art, type name and `Flight` change with a purchase, so anything caching them per ship (`EnsureShipArt`, the HUD speed bar) is refreshed through `AssignShipType` and `EnsureShipArt`.

## Decided

All the defaults were accepted:

1. Limits per kind of equipment (weapon / shield / plating / engine / utility). Limits: Shuttle 1 weapon, 1 shield, 1 plating, 1 engine, 1 utility, mass 450; Runner 2 weapons, 1 shield, 1 plating, 1 engine, 2 utilities, mass 900.
2. A new player starts with 1000 credits. NPCs carry about 100 (Shuttle) or 250 (Runner), between half and one and a half times that.
3. If the trade-in is worth more than the new ship, the difference is paid to the player.
4. Equipment can be sold back at its full price, from the Equipment Bay.
5. Every spaceport sells everything for now.
6. A save without credits loads with 0.
7. **Nothing saves on a purchase.** Only docking saves (as before), and quitting saves as it always has.

## Status

Step 1 (credits) is built: `Ship.Credits` with `CanAfford`, `TrySpend`, `Earn`; `Universe.StartingCredits`; `ShipStats.TypicalCredits` and `Nonplayer.RollCredits`;
credits on the HUD and at the top of the spaceport screen. The sample save now has `"Credits": 120`, and a test removes it to check an old save loads with 0.

Step 2 (prices, kinds and limits) is built: `EquipmentComponent.Price` and `SlotLimit(limits)` (no enum: the kind is the equipment class); `LoadoutLimits` as `ShipStats.Limits` (values as decided above);
`ShipStats.Price` (the ship alone) and `ListPrice` (the ship plus its default loadout); `Ship.CountOf<T>`, `EquipmentMass`, `EquipmentValue`, `ShipValue`, `CheckInstall` (an `InstallCheck`: Ok, NoSlot, TooHeavy), `Install`, `Uninstall`.
`ShipStats.BuiltIn()` lists every ship type (`FromName` uses it too).

**One refinement for step 3:** `ShipStats.Price` is the ship without equipment, so buying a ship costs its **`ListPrice`** (ship plus the default loadout it arrives with) minus the player's `ShipValue` (current ship plus current equipment).
Otherwise a new ship's free default loadout would be an exploit.

Placeholder prices: Shuttle 800 (list 1250), Runner 2500 (list 3900); equipment from 100 (Cargo Expander, Light Hull Plating) to 1500 (Military Shield Generator). They need balancing once credits can be earned.
The Plasma Gun was in the Runner's default loadout but missing from the catalog's list, so a saved Runner would have loaded it as an unknown item; it is in the list now.

Step 3 (purchase rules) is built, in `Universe` beside `Dock`/`Undock`, with no UI and no new types: `BuyEquipment(item)`, `BuyShip(type)`, `SellEquipment(item)` return `true` when they went through and post the reason through
`Messages` when they refuse; `ShipCostAfterTradeIn(type)` is the number the screen will show. (The first version had offer classes and a result enum; both were removed to keep the code small.)

Steps 4 and 5 are built: the two bays with their `Stock` lists, `Clone()` on equipment and ship types, rows and choosing, the open-store state and `Back()` in `SpaceportMenu`, and the overlay and Escape handling. The drawing and the keys have only been compiled, not played; see the manual check list.

## Done (step 6)

All six steps are built. The cleanup removed two things nothing needed: `CountOf<T>` became `Ship.CountLike(item)`, which both the install check and the bay rows use, and `SpaceportService.Perform` is an empty virtual so the two stores need no empty override.
A test buys a ship and equipment, saves and loads, and checks the credits, the ship type and the equipment come back. The architecture tests still pass: nothing in the new data uses a graphics type.

**Manual check list** (the store screen is only compiled so far; none of this has been played):

1. Dock at a planet with a spaceport. The panel shows "Credits: 1000" at the top right; the list reads Equipment, Starships, Undock (and Refuel/Repair only when needed).
2. Open Equipment: a wide panel with a heading, one line per item with price, mass and `[carried/limit]`; the highlight scrolls the list. Buy a weapon you have no slot for: "No free slot for ..."; buy one you cannot afford: "Not enough credits".
3. Buy the heaviest item with the weapon slot free: "... is too heavy for this ship" when over the mass limit. Sell an item: it leaves the ship, the credits rise, and it appears in the sell lines at the bottom.
4. Escape in a store goes back to the list with the highlight on the store; Escape on the list goes to the main menu.
5. Open Starships: each line shows list price, trade-in and cost. Buy the Runner from a Shuttle: credits drop by the cost, the ship changes (new art after undocking), the loadout is the Runner's default. Buy a Shuttle from a Runner: credits go up.
6. Undock, dock again at once (the numbers are the same), quit to the menu while docked and load: the ship, equipment and credits are as left. Fly with L/Tab as before.
