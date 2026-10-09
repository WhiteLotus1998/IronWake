# 0402: Grit's one reshape fails its screen; the capped form rule stays

Date: 2026-10-09. Issue #1489 (Table rounds 559 to 561). Follows 0386, 0398, 0400, 0401.

## Decided

- **`grit_gain: hits`** (header, only with `forms: on`). Every unit starts at 1 Grit, spawns included (`Grit.AtPlacement`). A phase start gains nothing. Each hit taken gains 1, counters included, cap 3. A kill gains nothing. The map legend says so.
- **One declaration rule for both sides** (`FormChoice`, Core). A form is offered when its expected damage, with each outcome capped at the target's current HP, is strictly higher than the plain strike's. The expectation counts doubles, crits, and the first strike's Stoop, mark and shell. Ties go to the plain strike. Of the affordable offers, the unit declares the best. The enemy planner, `threat` and the Sim's player all call it, which replaces the two copies of 0400's rule. A boss under the veto still declares none. **This rule is the default and stays whatever happens to Grit** (round 560): it lands before #1453 reads forms.
- **`form_rule: lethal`** (header, only with `forms: on`) keeps 0400's rule. Code's 1461 play moves to `docs/samples/the_tollgate_forms_1461.map`, which carries it, so its replay still holds. The screen's comparison rows use it too.
- **The screen** is `--full <map|--all> --forms --screen`. It plays forms off, then {refill, hits} x {lethal, capped}, on the same 200 seeds and with the same player. It prints each side's offers, the split by weapon type, and gate 1 on every map (`docs/measurements/forms-1489-screen.txt`).
- `docs/samples/the_tollgate_forms.map` now carries `grit_gain: hits` for Chat's cold chair.

## The reading (all nine maps, 200 seeds, both sides)

| row | offers | unaffordable | 2-cost affordable | wins of 1800 |
|---|---|---|---|---|
| forms off | | | | 961 |
| refill, lethal | 3509 | 13 % | 87 % | 1033 |
| refill, capped | 13644 | 22 % | 78 % | 948 |
| hits, lethal | 3861 | 64 % | 36 % | 991 |
| **hits, capped** | **14553** | **78 %** | **22 %** | **988** |

**On the shipped row, Grit fails 0386's criterion on its other half.** 78 % of the offers are unaffordable, against the one-third line. Hits-only did not make Grit a price. It made Grit a gate that is mostly shut. Reason is worst, as Chat predicted (round 560): the player's Overcast is unaffordable 81 % of the time, and the enemy's 100 %. Swords and bows sit near 72 %. Only axes, which close to melee and take counters, mostly pay (14 % player, 43 % enemy). The capped rule is not what failed it. Under hits, the lethal rule fails the same line (64 %).

Code's warm chair on the hits sample (PLAYTEST, 1489, 7/7/7) agrees: the column changed decisions three times. But Pell's Overcast and Wren's Heavy Cut were never affordable all game.

The capped rule alone moves the refill row from 87 % to 78 % affordable, which is just under the 80 % line. That is information only. 0386 allows no second shape, and the refill shape was already killed in 0400.

## What follows (0386)

- **Grit is killed.** The basic forms fall back to cooldowns, with no second shape. The cooldown slice gets its own issue. Until it lands, nothing changes off the header, and no `forms: on` map ships.
- **The capped rule stays** for enemy forms under cooldowns, and lands before #1453 reads forms.
- **Gate 1 under forms** is in the measurement, per map. The capped rule is harder on the player than 0400's rule: under refill, the Mill fell from 115 to 76 and the fixture from 58 to 34, because enemy archers now spend Aimed Shot whenever it adds expected damage. Cooldowns will change that number again, so it decides nothing here.
- Chat's cold chair on the hits sample is no longer the deciding read, because the Sim alone kills the shape. It stays open as a journal if Chat wants it.
