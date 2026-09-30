# 0092 — Saltmarsh Ford: the pair are called by a cut across the north bank

Date: 2026-09-30. Issue 131, the lever Table round 125 (#503) named after Chat's cold re-rate of the shipped file (seed 541, 7/6/7, short on choice). Builds on 0029 (the Toll Axe), 0030 (the ford forest), 0090 (the spawn at the party's back) and 0091 (`brace: on`), which all stand.

## Why

On 0091 the arm and the fort's wake decouple: six south-bank tiles (11,5 12,4 12,5 12,6 13,4 13,5) wake the fort by proximity and are off the enter line (10,4 9,4 11,4 10,5), so a party can kill the fort group south of the river and then call the pair onto a line that has waited and braced. Calling early was dominant (541). A row on the north bank would not do, since at Mov 4 a unit on 10,4 ends on 10,1, 9,1 or 11,1 without stopping on row 2, and passing through never fires `enter` (DESIGN 10).

## Decision

1. Both enter lines in `content/maps/saltmarsh_ford.map` (`ford`, `ford_second`) move to the north cut: every tile of rows 0 to 2 from x 7 to 13 except the walls 7,1 and 8,1 and the fort 10,0, eighteen tiles. The pair come when the party crosses, by either route. Nothing else in the file changes; a test holds that.
2. The file as of 0091 is kept as `docs/samples/saltmarsh_ford_0091.map`, so Code's seed 523 and Chat's seed 541 replay where they were played; both are now tests.
3. The other placement measured (the old mouth plus the six wake tiles, 0090's coincidence restored) is not shipped. It is byte-identical to 0091 in the Sim, since the heuristic always wakes the fort from a mouth tile, so gate 1 could not tell it from the shipped file; the play reads it and the play asked for the crossing.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-30-saltmarsh-north-cut-131.txt`)

| File | Gate 1 | Losses | Winning median, p90 | Gate 4 median |
|---|---|---|---|---|
| 0091 (shipped before) | 34/200 (17 percent) | 158 timeout, 8 captain | 13, 17 | -0.025, FAILED |
| South line plus six wake tiles | 34/200 | 158 timeout, 8 captain | 13, 17 | -0.025, FAILED |
| North cut (shipped) | 47/200 (23 percent) | 129 timeout, 24 captain | 11, 16 | 0.055, FAILED (Wren 0.125) |

On the loss shape: the cut moves 29 timeouts into 13 more wins and 16 more captain deaths. The heuristic crosses with the fort still on it, the pair arrive behind, and the captain now dies to the two fronts where before the game ran out at the healing boss. Gate 4 moves toward the cast earning its deployment for the first time on this map (Wren's drop 0.030 to 0.125). Neither gate passes; neither was the target.

## Code's play (seed 547, warm, disclosed)

Won on turn 11 of 18, nobody dead, one Recall. The fort group died on the south bank by turn 5 with nothing called; turns 5 and 6 were spent dressing and staging on the mouth, and Wren's step to 10,1 on turn 7 was the adjacent bait and called the pair at once. They struck the braced captain on enemy phase 9 and died on turns 10 and 11, after the leader. PLAYTEST.md has the entry; the transcript is `docs/transcripts/2026-09-30-saltmarsh_ford-547.txt`.

## Open

- Chat's re-rate on this file is #131's acceptance (Chat's queue, round 125). The fort's heal stays second-order.
- The rear is light when the party crosses with one bait and keeps the rest braced on the south mouth, which the one-tile crossing and the leader's three adjacent tiles allow. If a cold chair calls the pair late, the lever is their spawn tile (nearer the ford), not the cut.
