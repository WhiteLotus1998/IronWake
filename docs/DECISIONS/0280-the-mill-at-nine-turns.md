# 0280 — The Mill at nine turns (the Mill's lever a)

Date: 2026-10-06. Issue #1189; Design Table #1187, rounds 402 (Chat) and 403 (Code). Provisional. Amends nothing; the tuning number on top of 0279.

## Context

Both chairs found the Mill's limit of 12 never binds (Code 1480 won on turn 4, Chat 2061 on turn 9). Round 402's levers, one at a time: (b) `holds:` first (0279, shipped), then (a) a limit of 9 measured on top. Under `holds:` Code 1500 still won on turn 8 of 12, and the Sim's slack median was 5 turns. Chat's reasoning for 9 over 8: at 9, Full Measure's spent phase becomes a real price; at 8 the patient line dies and only the wake line survives. Code asked on the Table whether 9 still stands now that the wake line ends on turn 8; no answer yet, so 9 ships as the lean.

## Decision

- `the_mill.map` carries `turn_limit: 9`.

## Measured

Sim, 200 seeds, under `holds:`:

| limit | gate 1 | losses | slack median / p90 |
|---|---|---|---|
| 12 | 126 (63 %) | 0 | 5 / 4 |
| 9 | 123 (61 %) | 3 | 2 / 1 |
| 8 | 118 (59 %), FAILED | 10 | 1 / 0 |

The median win turn stays 7 at every limit; 9 is the tightest limit that clears gate 1.

Chat's 2061 patient line wins on turn 9 of 9, the last turn (`TheMillTests`). Code 1500 wins on turn 8 of 9.

Code 1510 warm, 8/7/6: won on turn 8 of 9, Maud on 1 HP, two Recalls spent. On turn 6 the captain's plain strike missed and left him lethal; the Recall bought Full Measure (a crit for 51 on the soldier), and its spent seventh phase was a turn the clock wanted: Maud alone on turn 7, then the captain's 95 percent Feint for exactly the archer's 7 HP on turn 8. Full Measure's price is now charged, as Chat predicted.

## Next

A cold chair on the Mill (0278); not Chat's (warm on both lines) and not Code's.
