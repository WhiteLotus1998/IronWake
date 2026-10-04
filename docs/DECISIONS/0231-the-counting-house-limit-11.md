# 0231 — The Counting House: turn limit 10 to 11

Date: 2026-10-04. Issue #931, rounds 311 to 314 (the order of levers), round 318.

## Context

Lever 1 (#925, 0228) made the bank archer a house guard at 11,2. She came to the water with the house, but in Code's warm play she then fell back to the yard's forest at 10,7, and the map was lost on the clock with her at 17/17. That made three chairs lost on the clock, and the Sim read 0 of 200, all timeouts. Rounds 311 and 312 set the next lever: one more turn.

## Decision

`turn_limit: 11`, and Ottilie's quest-1 card says "by the end of turn 11". Level 3, Recall 2, the archer at 11,2 and every placement stay. A new test checks every quest card's "end of turn N" against its map's limit, and a falsifying test catches the old card. Another test pins 11, 2, 3.

The journaled #925 play (`2026-10-04-the_counting_house-980.txt`) is regenerated. Its script now runs through enemy phase 10, where the archer shoots Teodor dead, and it stops undecided on turn 11. Its journal stays as written. The 2026-10-03 transcript and Chat's are older boards and no test replays them, so they stay as they were.

## Measured

- `--full` at 200 seeds: gate 1 at 0 of 200, all timeouts, unchanged. Gates 2, 3 and 5 to 8 pass. Gate 4 reads 0.000, as before, since nothing ever wins. The Sim's heuristic never finishes this board, so it decides nothing here, as #931 expected.
- Code's warm play on seed 980 (`2026-10-04-the_counting_house-980-931.*`): lost on turn 10 when Ottilie fell, with the clock not yet run out. The spare turn paid for a retreat on turn 8 that drew the Sworn Captain and the archer over the bridge. The captain died on our bank on turn 9, and the archer, with no cover on that bank, stood on the plain at 5,7. The loss came on turn 10, ending into a printed `Lethal if all land` on a 6 HP Ottilie. Warm, 8/7/6.

## Next

Chat's cold play decides the Fun Gate. If 11 still times out in a hand play, the next question goes to the Table, not to a third lever chosen here (#931).
