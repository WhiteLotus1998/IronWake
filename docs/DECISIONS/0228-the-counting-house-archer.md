# 0228 — The Counting House: the bank archer becomes a house guard at 11,2

Date: 2026-10-04. Issue #925, round 311 (Chat's cold play) and round 312.

## Context

Three chairs lost The Counting House on the clock: Code 980 (turn 8), Chat 980 cold (turn 10, the bank archer at 17/17), and the Sim at 0 of 200, all timeouts. Both journals keep the opening as built: the cork at 6,7, the road pair, and the house waking on the near-bank kill. The defect is the finish. A held archer at 7,3, over a canal only a bow can answer, never comes to the water.

## Decision

Chat's lean was the archer as `group:house behavior:guard`, staying on 7,3. On that tile she wakes the house on turn 1: the first shot at the bank soldier (7,7) is 4 from her, inside the noise radius of 6. That breaks the opening both chairs asked to keep. So she moves to 11,2, inside the house, as a house guard (`E archer 11,2 group:house behavior:guard`). Every standable tile is at least as close to the lector or the Sworn Captain as to her, so proximity and noise find the house exactly where they found it before. A test pins this, and a falsifying test shows 7,3 nearer the bank fight. What the opening loses is one option. In Code's 2026-10-03 play, a bow duel with her from 5,3 on turn 2 woke the house early. In Chat's play she did nothing before turn 9.

A second Code run (the Partner wake, on #925) found the same turn-1 wake and proposed 10,2, pinned beyond `wake radius + 2` from 7,7 and 4,3. 11,2 meets that test too. It is the stricter tile: 10,2 is nearer than the lector to the north bank (a fight at 4,2 or 7,2 would wake the house sooner), and 11,2 is nearer than the house to no standable tile. The arrival is the same: 8,3 on turn 5, the water on turn 6.

Level 3, limit 10 and Recall 2 stay. One lever at a time.

## Measured

- `--full` at 200 seeds: gate 1 at 0 of 200, all timeouts (unchanged).
- Code's warm play on seed 980: she came to the water with the house and struck at the cork, then fell back to the yard forest at 10,7, and survived to turn 10 unhit in four strikes. Lost on turn 10.

## Next

Limit 11 is the next lever, in Chat's order, if a cold chair also finds the finish a chase. The bank soldier stays `hold`.
