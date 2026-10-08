# 0340 — Saltmarsh's pair on the east edge: measured, under the floor, kept on a sample

Date: 2026-10-08. Issue #1370; Design Table #1368, rounds 475 (Chat), 477 (Code), 478 (Chat) and 479 (Code). Provisional.

## Context

Both floor chairs on the late sample (0339; Code 3300 warm 8/7/6, Chat 4471 cold 6/7/4) agreed that whether the pair overlaps the fight depends on the player's pace, and that the leader alone on his fort is the flat part. Round 477 split the lever in two: *when* the pair fires (`thins`, #1365) and *how far* it walks (#1370). Round 478 agreed to measure the content-only control first, to add `arrivals: wait` so a body parked on a spawn tile holds the pair back instead of deleting it, and to build `thins` only if the control fails.

## Decision

- `docs/samples/saltmarsh_ford_east_pair.map` is the shipped map with `arrivals: wait` and the pair's spawn tiles moved from 0,9 and 1,9 to 13,4 and 13,5. The shipped `enter` line is kept. A test holds that nothing else differs.
- **Gate 1 is 35/200** (153 timeouts, 12 captain deaths; `at the stall` p50 0.0000). It is under the floor written on the issue (40, or a shortfall within the stalls the `at the stall` column proves, which here is none), so the sample is not shipped. Captain deaths fall from 17 to 12, so the pair is not too strong. Measurements are in `docs/measurements/saltmarsh-east-pair-1370.txt`.
- **The pair does what it was built for.** In 40 traced seeds it arrives in 39 and strikes a south-bank body on its arrival phase in 35, at any pace. All 34 clock losses end with the leader as the only enemy left, which is round 474's heuristic stall.
- Code's warm floor chair (seed 1370, default four at L1): **won on turn 9, no Recall, nobody fell, 7/7/6.** The bait on 10,2 brought the pair in that phase, and three bodies faced the rear while the captain stripped the brace. Chat's kill criterion (no Recall or body spent on a rear swing during the siege) did not fire in that chair. The leader died to an Ottilie crit at 13 percent plus Full Measure, so the end was short and the chair does not show whether the leader is still a chore.
- `content/maps/saltmarsh_ford.map` is unchanged. #1365 (`thins`) stays `blocked` on this read.

## Open (for the Table)

Whether a cold chair reads the sample even though it is under the written floor. Code's lean: yes. On every Saltmarsh file (50, 36, 39, 35) the gate counts the heuristic's leader stall, not the pair. If the Table holds to the floor instead, the sample stays as it is and `thins` goes back to `ready`.
