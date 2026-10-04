# 0232 — The Oath Stone: turn limit 9 to 10, and the kill row reads as a condition

Date: 2026-10-04. Issue #939, rounds 317 to 320.

## Context

Chat's cold play on seed 1133 (round 317) reached the envoy on turn 8, two player phases from the end, against 28 HP on a fort that heals 5. Keziah hits him 54 percent of the time for 16; he hits her 81 percent for 14 of her 26. Chat put the win from turn 8 at 45 to 50 percent, most losses her death: a dice wall at the end of the oath road, not its price. Round 318 agreed one lever, the limit, with the fort next if 10 still reads as a wall and the envoy's level untouched. Rider timing (#940) waits on this one; the two are coupled, since #940 makes the road dearer and the extra turn pays for it (round 319).

In the same play Chat read `Kill: Keziah +10 HP` as a promise when a hit for 22 could not kill 24; two of Code's three reads paused on the same line. Round 319 agreed `On a kill:`, with `Kills on hit:` where one plain hit takes the foe.

## Decision

- `content/quests/the_oath_stone.map`: `turn_limit: 10`. Keziah's quest-2 card says "by the end of turn 10" (0231's test checks the card against the limit). The fort, the envoy's level, the rear timings and every placement stay.
- A hungering weapon's forecast rows open with `On a kill:` (the feed, the hunt running on), or `Kills on hit:` when the forecast's plain damage for that side reaches the other's HP. `Kinsbane.KillLabel` is the one place the wording lives; `PlaySession.HungerLines` takes the forecast to decide it, and reads `On a kill:` when it has none.
- The `threat` row under an enemy strike reads `Counter kills on hit:` when one counter strike is enough and `Counter kills if all land:` when only a double does. It is still printed only when the counter is lethal if every strike lands.
- The journaled slice-16 play (`2026-10-04-the_oath_stone-1133-fort.txt`) is regenerated: the limit and the rows change, the play does not. The older 1133 transcripts and Chat's are records of what was printed and no test replays them, so they stay.

## Measured

- `--full` at 200 seeds, 9 and 10: gate 1 at 0 of 200 both (9: 128 losses, 72 timeouts; 10: 122 losses, 78 timeouts). The Sim plays the map's own roster (captain and a recruit, no Kinsbane), not the oath save, so it decides nothing here; no Sim mode loads a campaign save, and building one was out of this issue's scope.
- Code's warm play (`2026-10-04-the_oath_stone-1133-939.*`, Chat's line through turn 7, then Code's): lost on turn 10, Keziah dead on enemy phase 10. The archer died on turn 8 (74). After a turn-9 Recall (Keziah's 54 missed and his counter left her at 13), Maud chipped from 11,4 with no counter and Keziah stood at 13,3; the brigand missed Maud at 68 and her counter took it to 11; the envoy missed Keziah at 81 and her counter missed at 54. Maud's Radiance (two uses) was then spent, and on turn 10 the envoy stood at 24, past a Cleave's 22. Every Kinsbane swing at him missed, three of three. Warm, 7/6/5.

## Next

Chat's cold read at 10 decides it. If 10 still reads as a wall, the fort is next (round 318). Then #940.
