# 0242: at dusk, threat prices an enemy a side-mate lights the unit for

Date: 2026-10-04. Issue #987 (bug; found in a warm replay of Brackwater Cut, seed 1310). Built by the chain Builder.

## Decided

- `Dusk.Knows` reads the live board, so once an enemy stands within sight of a unit, every enemy acting after it in the phase knows where the unit is and can strike it (DESIGN 13.7, issue 302). `threat` read the phase-start board, so it printed `cannot see you (dark)` for an enemy the phase then sent in. That broke "rules go on screen" (rounds 42, 44).
- `Queries.Threats` now asks each enemy `Unseeing` would list again, on the board with a side-mate moved to one of its own strike tiles within sight of the unit. If the enemy then strikes, it is a priced line carrying `LitBy`, in the total and in `Lethal` (`end`'s warning). A lit enemy can light the next one.
- The side-mate must act before the enemy in the phase's order, which is unit id order (`EnemyAi.Plan`). On a `pincer: on` map the anvils reorder the phase, so any side-mate counts there.
- Like every `threat` line it prices what the enemy would do were it to choose the unit. It is not a promise that the side-mate takes that tile.
- The console marks the row `(once <side-mate> lights you)`, or `(once a side-mate in the dark lights you)` when the player cannot see it. The protocol's threat line carries `litBy` (the id, or null when hidden). `cannot see you (dark)` stays only for an enemy no earlier side-mate's strike tile would light the unit for.
- This takes the issue's first lean. The rewording fallback would have left `end` blind to the lethal.

## Committed transcripts

`2026-09-27-brackwater_cut-241-lamps.txt` changes in two places: turn 4's `threat captain` prices the soldier lit by the shieldbearer (15 against 22, not 7), and turn 5's `end` names Pell lethal (shieldbearer 10, soldier 11, against 16). In the same play's enemy phase 5 the shieldbearer steps beside Wren and the soldier, lit by it, strikes her: the case this fixes.

## Tests

`DuskTests`: the lit enemy is priced, marked, totalled and named by `Lethal`; with no side-mate, or with the only one acting after it (a brigand before a shieldbearer), it stays `cannot see you (dark)`; the planner's enemy phase on the same board strikes the unit with both. The ordering guard was falsified by removing it (the brigand case fails). 4383 tests pass.
