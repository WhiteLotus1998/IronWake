# 0289 — The Mill's fort goes south, out of the woken pair's reach

Date: 2026-10-06. Issue #1210 (#1212 a duplicate); Design Table #1187, round 409 (Chat's cold 2250) and Code's reply. Provisional. Amends 0279's map only; `holds:` ships unchanged.

## Context

`holds: mill 0,0 11,2` (0279) stopped the woken mill pair marching on the fort, but from the edge of its ground the pair still struck the fort at 8,5 and fed counters into Maud. Chat's cold 2250 won on turn 5 of 9 with nobody fallen and no Recall, against warm turn 8s. #1210's lever: move the fort and Maud's start south so that no woken mill member can strike her start, with the road pair still reaching her by turn 2, the tile picked with `threat`. Kill: gate 1 under 50 percent.

## Decision

- The fort and Maud's start move from 8,5 to **7,9**. The stream runs on from 6,6 to 6,8, so the fort still stands on its far bank (the before scene): the ways over are the road bridge at 6,3 and the ford at 6,9.
- The road pair starts at **11,5 (brigand) and 11,4 (archer)**, not 11,8 and 11,9. From the old corner it would have struck a southern fort on enemy phase 1 (17 against 17 if both land, before the captain can arrive); from the east bank it reaches her on enemy phase 2, as it always did.
- `TheMillTests` pins the claim. From no tile of the held ground can either woken member strike Maud's start, and the same check fires on the old map: the archer reaches 8,5 from 9,2.

## Tiles tried (`threat` with the pair woken, then the Sim)

- Any row from 4 to 8 is in the archer's reach from 7,2 or 8,2. Only 7,9 and 8,9 are clear from every tile of the ground.
- 8,9 with the pair at 11,4 and 11,5 measured 94/200 (47 percent, Maud lost in 98), under the kill line.
- 7,9 with the pair the other way round, 11,4 archer and 11,5 brigand, measured 75/200. With the pair as shipped it measured 136/200. The pair's order decides whether the archer opens on the captain or on Maud.

## Measured

Sim, 200 seeds: gate 1 from 123 to **136 (68 percent)**, median win turn 7 to **8**, p90 9. Timeouts rose from 3 to **19**: the limit of 9 now binds. Maud was lost in 39 games, down from 73, and the captain in 6, up from 1. No fight on the fort wakes the mill any more (it is 9 or more tiles from both members), so the mill is the captain's errand across the road.

Code 632-south, warm, 8/7/6: won on turn 8 of 9 after two Recalls. The captain on 7,8 shielded Maud, and the road pair was dead by turn 4. Waking the mill cost a Recall twice: first the captain fell to the pair's strikes, then a bait at 14 HP left him on 1. A Salve-first bait at 21 HP drew both onto the road, where Maud's range-2 Radiance and the captain finished them on turns 7 and 8.

Chat's 2250 opening no longer plays: its turn-2 Full Measure from 6,9 has nothing to strike, since the road pair now comes down from the north-east.

## Kept for the replays

`docs/samples/the_mill_0280.map` is the Mill before this change. The 1500, 1510 and 2061-held replays read it. The full-campaign script opens the Mill with the new hand play and was regenerated. Variant 20 then lost the field, so the script is now variant 30, which keeps a side map and the spent purse.

## Next

A cold chair on the south fort, if it is not Chat's.
