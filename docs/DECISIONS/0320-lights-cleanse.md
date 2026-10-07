# 0320: Light's cleanse, a staff that clears burn, chill and stun

Date: 2026-10-07. Issue #1321 slice 1. Source: Lotus's second spell rulings (0317, light's ladder: "a cleanse that clears burn, chill and stun"). Provisional.

## Context

Light is Faith, not a Lore school (0296): Salve, Beacon and the Psalter heal, Radiance strikes. Every place in the code that asks "is this a strike or a staff" asks `heals`, so a cleanse built as a weapon with `heals: false` would be equippable and swing at 0 Mt.

## Decision

- **`cleanses: true` on a healing spell.** It requires `heals: true` and refuses `healBase`, so it is a staff everywhere a heal is (wielded by a class that heals with its type, never equipped, never a strike) and never mends HP.
- **It clears all three or what of them the ally carries:** burn (stacks, amount and count), chill, stun. A lock is the chill turned all the way (`Lock`), so it drops with the chill.
- **Refused on an ally carrying none of the three** (`nothingToCleanse`), however wounded: no use spent on nothing (0018's rule for heals).
- **Heals nothing** (the issue's lean). A reason to add a little did not turn up: Salve sits beside it in the same healer's pack.
- **A stun being skipped this phase is lifted:** the ally may move and act again, unless it is also resting. This is the cleanse's one turn-winning use and the lean most likely to be argued; the alternative is a cleanse on a skipping ally that only clears the clock.
- **The healer earns a heal's EXP** (as for an ally above half HP), rank and mastery, so a cleanse is never a dead action for its caster's growth.
- `Legal` offers it on an afflicted ally in range; the Sim's player and the enemy never cast it until a shipped unit carries one. A fixture staff (`test_cleanse`) carries the tests; nothing ships until Lotus signs #1247.

## Kill criterion

If a hand play shows the freed stun turning a lost map on one cast every time it is offered, the freeing goes and the cleanse only clears the clock.
