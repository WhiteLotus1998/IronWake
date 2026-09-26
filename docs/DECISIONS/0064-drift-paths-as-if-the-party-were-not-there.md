# 0064 — Drift paths as if the party were not there

Date: 2026-09-26. Issue 326, from Code's hand play of the shipped Brackwater Cut at `dusk: 5` on seed 41 (issue 318, DECISIONS/0062). Code's lean, posted on the Design Table (#322) and built before Chat's reply, as CLAUDE.md's "Take it to the Table, then proceed with a lean" says. It is provisional: Chat may argue it on the PR or the Table.

## Decision

`EnemyAi.Drift` measures the path cost to the objective as if no player unit stood on the board. A tile holding a player unit is empty to the distance field; allies, walls and water count as before. The move itself is still chosen from the unit's reach set, so it never ends on or passes through a player unit: the drifting unit stops at the best tile it can reach along the field, which on a held gap is the tile beside the blocker. The objective filter is unchanged (an exit or throne someone stands on is no destination), and so is `Approach` toward a known target.

Why: 13.7's third arm rests on "it knows the ground, not the party". Pathing around player units let the party's position steer drift through a wall. On Brackwater, one unit on the gap at 11,3 left no chase unit a path to any exit, so all of them Waited for as long as it stood there, which is the second arm's zero-attack night reached through geometry.

## Gates, Brackwater Cut at dusk 5, 200 seeds

| | before (main at be9aa35) | with 0064 |
|---|---|---|
| gate 1 | 88 percent (175/200), median 5, p90 7, 10 losses, 15 timeouts, quiet tail 4.2 | 91 percent (181/200), median 5, p90 6, 11 losses, 8 timeouts, quiet tail 4.1 |
| gate 4 | FAILED, 0.025 (Pell 0.560, Rook 0.100, Wren -0.050, Dunstan -0.125) | FAILED, 0.075 (Pell 0.600, Rook 0.120, Wren 0.030, Dunstan -0.095) |

Gate 4 still fails on Dunstan: the heuristic never attacks with him (atk 0 in both runs), so benching him costs nothing whether or not the chase comes. That is the heuristic's blocker play, not drift, and it stays open on the map. No other shipped map is a dusk map, so no other gate moves; the one journaled replay that changed is the seed 41 cork, whose transcript is kept as the record of the bug and whose test now asserts the new line (Wren killed on the gap on enemy phase 4).

## Played

Code, seed 53, by hand (PLAYTEST.md, `docs/transcripts/2026-09-26-brackwater_cut-53.txt`): the chase reached Dunstan on the gap on enemy phase 2 and killed him on phase 3; on phase 5 a rider drifted onto the exit at 19,3 and had to be broken at sight 1. Escape on turn 7, Dunstan and Pell fallen, one Recall. 8/7/8.

## Kill condition

Revert if a cold play at dusk reads the chase as arriving too early to be escaped, that is if the gap cannot be held for even one enemy phase. The alternative in #326 (a unit with no path makes for the tile nearest the objective by straight distance) is the fallback.
