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

Kept in `Universe` beside `Dock`/`Undock`, returning a `PurchaseResult` (`Ok`, `NotEnoughCredits`, `NoSlot`, `TooHeavy`, `NotDocked`) so tests and messages share one source:

- **`BuyEquipment(offer)`**: must be docked; checks credits, then `CheckInstall`; on success spends the price and installs a new object built from the offer (every ship gets its own copy).
- **`BuyShip(offer)`**: must be docked. Cost = the new type's price minus `Player.ShipValue` (the current ship plus everything it carries). On success the player's credits change by that cost, the new type is built fresh (`new Shuttle()` etc.) and
  given with `AssignShipType` (so it is full and equipped with its **default loadout**; the old equipment is gone, already paid for in the trade-in), the new ship's art is requested (`EnsureShipArt`), and the player's position, credits and name carry over.
- **`SellEquipment(item)`** (see the decisions): removes it and pays its price back.
- A price for an offer is read from the object, never typed twice.

### 4. The bays (spaceport data and screens)

- `Spaceport` gets the data: an **`EquipmentBay`** (a list of equipment offers) and a **`StarshipBay`** (a list of ship offers); both are plain data like the rest of `Spaceport`. An offer holds a builder (`Func<...>`) and a template built once for display,
  so a purchase always creates a fresh object and nothing is made from a name. The catalog gains a list of what can be sold (the existing builders), and a ship catalog (`Shuttle`, `Runner`; `ShipStats.FromName` uses it too, so there is one list).
  First version: every spaceport sells the whole equipment catalog and every ship type (stock varied per spaceport from the planet's seed is a later step).
- **Two services** join Refuel, Repair and Undock: `EquipmentBayService` and `StarshipBayService` ("Equipment" / "Starships"). A service that has nothing to act on opens a page instead: `SpaceportService` gets
  `Open(universe)`, returning a `SpaceportPage` (or null for the immediate kind like Refuel).
- **`SpaceportPage`** is a small abstract class: a title, rows (a name, a detail such as price and mass, and whether it can be bought now with the reason if not), and `Select(row, universe)`. `EquipmentBayPage` and `StarshipBayPage` implement it
  over the bays. `SpaceportMenu` keeps a stack of pages: the main service list is the first, Select on a service with a page pushes it, **Escape or a Back row pops** (and only on the main list does Escape leave for the main menu).
- The overlay draws whichever page is on top (title, rows with price/mass, the highlight, credits in the header, a one-line reason for a disabled row, hints), and everything stays driven by the same `InputState` menu keys.
- Equipment rows show: name, kind, price, mass, and the slot/weight left. Ship rows show: type, price, the trade-in value, the cost after it, and the ship's limits.
- Feedback goes through `Universe.Messages` ("Bought X", "Not enough credits", "No free weapon slot", "Too heavy for this ship").

## Steps

1. **Credits.** `Ship.Credits` + helpers, new-player amount, NPC credits from the ship type and a seeded roll, HUD and spaceport-screen display. Tests: spend/earn/can't overspend; saved and loaded; NPC credits reproducible and in range; two NPCs differ.
2. **Prices, kinds and limits.** `SlotLimit` on each kind of equipment, `Price` on equipment and ship types, `LoadoutLimits` and the values for `Shuttle`/`Runner`, the `Ship` helpers and `CheckInstall`. Tests: counts and mass per kind; each limit enforced and exactly-at-limit allowed; every built-in type's default loadout fits; unknown items cost nothing.
3. **Purchase rules.** `PurchaseResult`, `BuyEquipment`, `BuyShip`, (`SellEquipment`). Tests: each failure leaves credits and equipment untouched; success spends, installs a separate object; trade-in math (new price minus ship and equipment value, including the case where the trade-in is worth more); the new ship has the default loadout, full hull/shield/fuel, kept position and credits; not docked is refused.
4. **Bays and pages.** `EquipmentBay`, `StarshipBay`, the offers, the catalogs, `SpaceportPage` + the two pages, the `Open` hook on services, the page stack in `SpaceportMenu`. Tests for the page rows (affordable / not, with reasons), selection, back, and that the menu's old behavior is unchanged.
5. **Overlay.** Draw pages and credits; Escape/Back handling in `PlayingScreen`. Drawing only compiled, so a manual check list.
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

Step 3 (purchase rules) is built, in `Universe` beside `Dock`/`Undock`, with no UI and no messages (the pages in step 4 will word the results). `TradeResult` is Ok, NotDocked, NotEnoughCredits, NoSlot, TooHeavy or NotCarried; anything but Ok changes nothing.
- `BuyEquipment(EquipmentOffer)`: docked; credits checked first, then `CheckInstall`; spends the price and installs a new object from the offer (`EquipmentOffer` keeps a builder and a display template).
- `BuyShip(ShipOffer)`: docked; cost = the offer's `ListPrice` minus `Player.ShipValue`; a negative cost pays the player. The new type replaces the old one through `AssignShipType` (default loadout, full hull/shield/fuel) and its art is requested. `ShipCostAfterTradeIn(offer)` is the number the screen will show.
- `SellEquipment(item)`: docked; removes the item and pays its price.
