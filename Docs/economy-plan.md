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
- Slot kinds. Equipment falls in five kinds for limits: **weapon** (all six weapon kinds), **shield**, **plating**, **engine**, **utility**. `SlotKind` is a small enum, with `EquipmentComponent.SlotKind` giving an item's kind
  (an `UnknownComponent` counts as none and costs nothing).
- A new `ShipStats` part, `LoadoutLimits` (next to `Flight`/`Durability`): `MaxWeapons`, `MaxShields`, `MaxPlating`, `MaxEngines`, `MaxUtilities` and `MaxMass` (total equipment mass),
  exposed as `ShipStats.Limits`, with `MaxOf(SlotKind)`. Each ship type sets its own.
- On `Ship`: `EquipmentMass`, `EquipmentValue` (sum of prices), `ShipValue` (type price + equipment value), `CountOf(SlotKind)`, and `CheckInstall(item)` returning `Ok`, `NoSlot` (that kind is full) or `TooHeavy` (mass would pass the limit),
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
2. **Prices, kinds and limits.** `SlotKind`, `Price` on equipment and ship types, `LoadoutLimits` and the values for `Shuttle`/`Runner`, the `Ship` helpers and `CheckInstall`. Tests: counts and mass per kind; each limit enforced and exactly-at-limit allowed; every built-in type's default loadout fits; unknown items cost nothing.
3. **Purchase rules.** `PurchaseResult`, `BuyEquipment`, `BuyShip`, (`SellEquipment`). Tests: each failure leaves credits and equipment untouched; success spends, installs a separate object; trade-in math (new price minus ship and equipment value, including the case where the trade-in is worth more); the new ship has the default loadout, full hull/shield/fuel, kept position and credits; not docked is refused.
4. **Bays and pages.** `EquipmentBay`, `StarshipBay`, the offers, the catalogs, `SpaceportPage` + the two pages, the `Open` hook on services, the page stack in `SpaceportMenu`. Tests for the page rows (affordable / not, with reasons), selection, back, and that the menu's old behavior is unchanged.
5. **Overlay.** Draw pages and credits; Escape/Back handling in `PlayingScreen`. Drawing only compiled, so a manual check list.
6. **Cleanup.** Architecture tests (no graphics types in the new data), docs, manual check list: buy a weapon, hit the weapon limit, hit the weight limit, buy a ship, check the trade-in, save and reload with the new ship and credits.

## Risks and notes

- **Existing tests** that build ships with the default loadout or count equipment keep working (limits are only checked when installing). Tests that need a certain loadout already use `TestGear`; new limits tests should give the ship their own `LoadoutLimits` rather than depend on the Shuttle's numbers.
- **Saves:** `Credits` is a new field (old saves read 0, so give old saves the starting amount only if the field is missing: `int?` on load or a `Credits` default; decided in the open list).
- **Unknown/retired items** in a save: no slot kind, no price, no mass counted, and never removed.
- **Prices need balancing** once there is more than one source of credits; the first numbers are placeholders chosen so the loop can be tested (a new player can afford one useful upgrade).
- A ship's drawn art, type name and `Flight` change with a purchase, so anything caching them per ship (`EnsureShipArt`, the HUD speed bar) is refreshed through `AssignShipType` and `EnsureShipArt`.

## Open decisions (defaults in bold)

1. **Slot kinds**: weapon / shield / plating / engine / utility, as above? And per-type limits, e.g. Shuttle **1 weapon, 1 shield, 1 plating, 1 engine, 1 utility, mass 450**; Runner **2 weapons, 1 shield, 1 plating, 1 engine, 2 utilities, mass 900**.
2. **Starting credits** for a new player: **1000**. NPC credits: **about 100 for a Shuttle and 250 for a Runner, +/-50%**.
3. **Trade-in worth more than the new ship**: **the difference is paid to the player** (it is a trade-in), or the cost never goes below 0.
4. **Selling equipment** back (needed once a ship is full): **yes, at its full price, from the Equipment Bay**, or buy-only for now.
5. **What the bays stock**: **every spaceport sells everything** for now, or a seeded subset per planet.
6. **Missing `Credits` in an old save**: **start at 0**, or give the starting amount.
7. **Purchases save at once**, like docking: **yes** (an autosave after each purchase), or only on exit.
