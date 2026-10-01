# 0129 — The signature ceiling: the Sim holds every bound item to the shop

Date: 2026-10-01. Issue #635, slice 3. The Builder building the measure DESIGN 14 already states in numbers (rounds 192 to 194): a signature item is at most 15 percent over the best shop weapon of its rank in damage per combat, and its signature art loses to the plain attack somewhere (0099). Mechanics only, under Lotus's story gate (#656). The definitions below are Code's leans; Chat can argue them on the PR.

## Decided

- **Damage per combat** is the attacker's expected damage in one combat through the section 5 functions: damage x hit probability x (1 + 2 x crit), x the strike count (doubles and gauntlets' two). Not capped at the target's HP, so a kill never hides the margin.
- **The owner** fights at their cast card on plain. **The targets** are every unit outside the cast with a weapon that strikes, carrying the first such weapon, on plain at full HP, struck at the attacking weapon's nearest range. The ratio is summed over the targets.
- **The comparator** is the shop weapon of the item's type and rank with the most damage: one some campaign map stocks, else any priced one. None at all fails, naming the rank. A healing spell is never a comparator.
- **The verdict:** the item's plain damage over the comparator's at most 1.15, and each signature art on it dealing less than the item's plain attack against at least one target. An item bound to nobody in the cast, or one its owner's class cannot use, fails.
- `ironwake-sim --ceiling` prints a line per item and per art; `--smoke` runs the verdict as a row, so CI holds the cap the moment an item ships. With no item it prints `signature ceiling: no signature item ships: ok`.

## Not decided here

Gate 4's ablation with and without the item (needs a shipped item on a board). Quest 1's second signature: 13.18 lives behind `signatures: on` on samples, so whether a campaign board plays a quest-earned signature is a Table question. The items themselves (#656).
