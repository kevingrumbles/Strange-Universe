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

Kept in `Universe` beside `Dock`/`Undock`. **No new types:** the stock is what the catalogs already build fresh with every read (`EquipmentCatalog.All`, `ShipStats.BuiltIn()`), so an item taken from them is already
its own new object; there are no "offer" classes and no result enum. Each method returns `true` when the trade went through; a refusal changes nothing and says why through `Messages` ("Not enough credits",
"No free slot for X", "X is too heavy for this ship"), so whatever lists the stock only has to try the trade.

- **`BuyEquipment(item)`**: must be docked; credits are checked, then `CheckInstall`; on success the price is spent and the item installed.
- **`BuyShip(type)`**: must be docked. Cost = the new type's `ListPrice` (the ship plus the default loadout it arrives with) minus `Player.ShipValue` (the current ship plus everything it carries); a negative cost pays the player. On success the new type replaces
  the old through `AssignShipType` (so it is full and equipped with its **default loadout**; the old equipment is gone, paid for in the trade-in), the art is requested (`EnsureShipArt`), and position, name and credits carry over. `ShipCostAfterTradeIn(type)` is the number to show.
- **`SellEquipment(item)`**: must be docked and carrying it; removes it and pays its price.

### 4. The bays (no new data classes)

- **The spaceport's services are the bays.** `Spaceport` is unchanged (a description and a list of services); two services join Refuel, Repair and Undock: **`EquipmentBayService`** and **`StarshipBayService`**. Their stock is the catalogs
  (every spaceport sells everything for now; a per-planet stock later would be a list handed to the service's constructor).
- **A service with rows.** `SpaceportService` gets two virtual members, default "none": `Rows(universe)` (a list of text lines) and `Choose(universe, row)`. The equipment bay's rows are one line per catalog item ("Light Laser  150  mass 50  (1/2 weapons)"),
  then one per carried item to sell back ("Sell Light Laser  150"); the starship bay's rows are one line per ship type with its list price, the trade-in and the cost after it. `Choose` just calls `BuyEquipment` / `SellEquipment` / `BuyShip` on the row's item (rebuilt from the
  same catalog index) and the trade posts its own message. No row, page or offer classes.
- **`SpaceportMenu`** needs one extra field: the service that is open (or none). While one is open the menu shows its rows instead of the service list; Up/Down/Select work the same way; `Back()` closes it (Escape calls it, and only leaves for the main menu
  when nothing is open). There is no page stack because there is only ever one level.
- **`SpaceportOverlay`** draws whichever list the menu has (services, or the open service's rows) with the same row style, and shows credits in the header, as it does now.

## Steps

1. **Credits.** `Ship.Credits` + helpers, new-player amount, NPC credits from the ship type and a seeded roll, HUD and spaceport-screen display. Tests: spend/earn/can't overspend; saved and loaded; NPC credits reproducible and in range; two NPCs differ.
2. **Prices, kinds and limits.** `SlotLimit` on each kind of equipment, `Price` on equipment and ship types, `LoadoutLimits` and the values for `Shuttle`/`Runner`, the `Ship` helpers and `CheckInstall`. Tests: counts and mass per kind; each limit enforced and exactly-at-limit allowed; every built-in type's default loadout fits; unknown items cost nothing.
3. **Purchase rules.** `BuyEquipment`, `BuyShip`, `SellEquipment` (bool results, messages for refusals). Tests: each failure leaves credits and equipment untouched; success spends, installs a separate object; trade-in math (new price minus ship and equipment value, including the case where the trade-in is worth more); the new ship has the default loadout, full hull/shield/fuel, kept position and credits; not docked is refused.
4. **Bays.** `EquipmentBayService` and `StarshipBayService`, `Rows`/`Choose` on `SpaceportService`, the open-service field and `Back()` in `SpaceportMenu`, the two services added to every spaceport. Tests: the rows (names, prices, what the player can sell), choosing a row buys or sells, Back, and that the old services and menu behave as before.
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
