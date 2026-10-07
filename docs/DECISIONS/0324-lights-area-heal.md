# 0324: Light's area heal, every wounded ally around the caster

Date: 2026-10-07. Issue #1321 slice 3. Source: Lotus's second spell rulings (0317, light's ladder: "a big late heal for the area around the caster"). Provisional.

## Context

Every heal today is cast on one ally in range (`item <unit> <slot> <ally>`). Light has nothing late. The issue's leans: no target, radius 2, the caster included, few uses, once a map; the forecast lists everyone it heals; the Sim and the enemy never cast it until a shipped unit carries one.

## Decision

- **`areaHeal: N` on a healing spell** (`Weapon.AreaHeal`, the radius). The loader refuses it below 1, on a spell that does not heal, and on a cleanse. Healing is already Faith-only, so no school check is needed.
- **No target.** `item <unit> <slot>`; naming one is refused (`notUsable`), as a Field Dressing refuses one. No art.
- **Who it heals:** every wounded unit of the caster's side within N tiles of the caster, the caster too, each by the caster's single heal (`Combat.Heal`, Mag / 2 + 5 + healBase), never past max HP. Refused (`nothingToHeal`) when no one there is wounded.
- **Events:** `itemUsed` (the caster as the target), then one `unitHealed` per unit in board order, then `spellSpent` when the last use goes. No new event.
- **One heal's EXP**, rank and mastery, with the below-half bonus when any unit it healed was under half HP. A big heal is not a big EXP farm.
- **Once a map is its uses.** Spell uses refresh each map, so a one-use staff is once a map with no new rule. The fixture (`test_mend`: Salve with healBase 5, radius 2, one use) carries the tests; the shipped number waits on Lotus's list (#1247).
- **The forecast:** `item <unit> <slot> preview` prints `Test Mend heals hale 8 (hp 22), mira 6 (hp 16)` and applies nothing (`AreaHeal.Preview`); any other item is refused there.
- **`Legal` offers it with no target** when someone in the radius is wounded. The Sim's player and the enemy planner never cast it.

## Unsure

- A Hollow on the caster's side in the radius is healed like anyone else. Light mending the raised reads oddly beside the Hollow strike; if the Table wants light to pass over Hollows, it is one clause in `AreaHeal.Healed`.

## Kill criterion

If a play carrying a shipped area heal never once gathers the company to cast it, the radius is wrong (too small to plan for) or the heal too small to be worth the gathering; retune before cutting.
