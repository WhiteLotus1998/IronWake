# 0321: Light's Hollow strike, effective against a raised unit

Date: 2026-10-07. Issue #1321 slice 2. Source: Lotus's second spell rulings (0317, light's ladder: "a strike that is effective against Hollows"). Provisional.

## Context

Effectiveness keys on movement types (`MovementType`, the `effective` list), and `Combat.Atk` triples Mt when the weapon names the target's. A Hollow (#1284, 0314) is a `BattleUnit` carrying a `HollowMark`, of whatever class it rose from; it has no movement type of its own. Adding `hollow` to `MovementType` would reach terrain costs, the crit-against list and every switch over movement.

## Decision

- **`Weapon.EffectiveAgainstHollows`, written as a `hollow` entry in the `effective` list.** One key in content, a flag in Core. The serializer writes it back into the same list.
- **Only a Faith strike may carry it:** the loader refuses it on a heal, a physical weapon or a Lore tome, and refuses it repeated. Light owns the answer to dark's raise.
- **One multiplier.** `Combat.IsEffective` reads movement or Hollow; Mt is tripled once (`EffectiveMultiplier`), never stacked when both hold.
- **The board decides.** `Combatant.Hollow` is set from the mark by `BattleUnit.ToCombatant(state, ...)`; off the board it is false. Every forecast, the resolver, `threat`, both planners and the Sim read it through `Combat.Atk`, so there is no second path.
- **No new forecast line.** A flier's tripled damage is shown as the damage number and the card says `Effective against flying.`; the Hollow strike is the same, `Effective against hollows.`
- A fixture tome (`test_sunlance`, Radiance's numbers) carries the tests; nothing ships until Lotus signs #1247.

## Kill criterion

If a play with a shipped Hollow strike never once picks its target because it is a Hollow, the tag is decoration; the Table then weighs a stronger light answer (a strike that ends the Hollow outright).
