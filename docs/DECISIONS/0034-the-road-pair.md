# 0034 — Old Mill Road: a road archer trails the brigand

Date: 2026-09-25. Issue 160's next pass (issue 13's Old Mill Road slice), from the twenty-fifth and twenty-sixth rounds: both Fun Gate entries on DECISIONS/0031 (Code 7/6/7 on seed 73, Chat 5/6/5 cold on seed 83) name turns 1 to 3 as the failure, a lone brigand who dies to the captain's counter double. Built by Code; Chat re-rates cold and argues on the PR if it disagrees.

## Decision

One line is added: `E archer 8,7 group:road behavior:aggressive`, appended so the mill archer keeps the id `archer-1` and the road archer is `archer-2`. Nothing else moves: grid, limit 12, the brigand at 6,5, the mill group, 0031's walking bandit.

The pair is the brigand plus an archer (twenty-fifth round), because a bow is the body the captain's counter cannot answer. Both move 4, and from 6,5 and 8,7 they reach the party together: if the party holds on turn 1, enemy phase 1 leaves the brigand at 3,6 and the archer at 4,7 in the open beside it, and both strike on enemy phase 2. The twenty-sixth round's check holds: every tile a cadet can strike the archer from on turn 2 (3,7, 4,8, 5,7) lies inside the brigand's reach, and two of them are reachable by the two cadets at once, so turn 2 has two lines. Front the axe: the captain doubles the brigand at 90 for exactly its 22 and somebody takes the arrow (6 at 70). Rush the bow: both cadets, 87 and 85 for 11 and 10 against 17, and both left inside the brigand's reach (10 or 11 at about 50). The wave on a full-HP cadet is 17 at most and cannot kill.

## Placements probed (two rolls, 200 seeds; hand-probed for the turn-2 board first)

| Road archer at | Turn-2 board after holding | Gate 1 | Gate 4 (Wren) |
|---|---|---|---|
| 7,5 | archer at 4,6 behind the brigand; only 4,7 strikes it, so one cadet can reach it | 46 percent, 72 captain | 0.440 |
| 8,6 | | 46 percent, 73 captain | 0.440 |
| 9,7 | | 46 percent, 73 captain | 0.315 |
| 10,6 | | 48 percent, 68 captain | 0.305 |
| **8,7 (shipped)** | **archer at 4,7 in the open; both cadets can strike it** | **54 percent (107/200), median 9, p90 11; 38 timeout, 55 captain** | **0.515** |

Before, on 0031: 78 percent, 4 timeouts, 41 captain deaths, Wren 0.500. Full output: `docs/measurements/2026-09-25-full-omr-road-pair-200seeds.txt`. Gates 2 to 8 pass.

## Gate 1 fails, and why that is not read as the map's verdict yet

Gate 1 falls from 78 to 54 percent, under the 60 line, and the losses are captain deaths, not stalls, so the fifteenth round's rule about the heuristic's stalls does not cover it. Traces of seeds 4, 7, 8 and 9 show where they come from. The heuristic walks both cadets forward on turn 1 into the wave, keeps a recruit at 3 HP swinging on turn 2 (it has no recruit veto, the same recklessness DECISIONS/0027 found with `protect: wren`), and loses Wren on turns 2 to 6. The captain then fights the mill alone and dies, on turns 4 to 11, in two of the four in the corner at 0,0 and 2,0. The hand play did not meet that board: holding on turn 1 was enough for the wave to be a price. So the gate is read as the planner's recruit policy against a wave it walks into, not as the map being unbeatable, and the Fun Gate decides first. If Chat's cold re-rate keeps the pair, the gate is the next thing this map owes. The levers, in order, are the party's start (the second check's lever) and the archer's HP or hit, never the mill.

## The hand play

Seed 101, won on turn 10 of 12 with two Recalls. PLAYTEST.md has the entry (7/7/6); the transcript is `docs/transcripts/2026-09-25-old_mill_road-101.txt`, and a test replays its script under `--strict` in place of seed 73's, which no longer replays since its opening changed. The turn-2 fork was real (rushed the bow, and the brigand hunted Wren for 11). The second check, the road's cost reaching the fort, is only half met: the road cost a dressing, felt on turn 9, but turns 3 and 4 are still a clean-up and a walk.

## Tests that moved with the map

The CLI's turn-1 enemy phase and its history counts (the road archer adds two states per phase), the journaled-script test (seed 101 in place of 73), the trace-with-an-item test (seed 5 in place of 3, since seed 3 no longer dresses), and the stall test's seed (5 in place of 1, since seed 1's win now falls on the limit turn).

## Not decided

The map is not `tuned`. It needs Chat's cold re-rate on this version, and gate 1 is owed after it.
