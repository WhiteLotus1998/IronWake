# 0383: front spacing screened on the Warden sample, every cell worse; depleted's stage-1 captain falls are crits and corners; the healer rarely falls first

Date: 2026-10-09. Issue #1441, from the Design Table's rounds 539 (Chat) and 540 (Code): the spacing screen and two reads on depleted's pre-swallow losses, in one PR. Reads only: no board, unit or planner change; the sample stays at 127 / 107 / 0 (0380).

## Decided

- **`--finale` prints two stage-1 lines** (`StageOne.CaptainFall`, `StageOne.Fallen`; `FinaleRun.VetoSplitLine`, `FirstFallLine`): each stage-1 captain fall by the veto's verdict on the tile he ended his last player phase on (passed, cornered, failed while one passed) and by how he died (arrival, crit, line, plain); and, in the games lost before the swallow, who of the company fell first.
- **The screen** (`docs/measurements/keep-1441-spacing.txt`) moves the walls, the fronts, their fall spawns and the company west. West 1: 17 / 32. West 2: 58 / 105. Against 127 / 107, **the fronts stay where they are.** A hold moved east cannot be built: his spawn and the waves come from an edge.
- **The captain split, depleted's 26:** passed 11, every one a crit; cornered 12; failed while a tile passed 2; his own phase 1. No death on a passed tile came without a crit, so the veto is right where it passes and no planner fix follows (round 539's condition is not met). Full's 12 and the floor's 92 read the same way (the floor: one plain death on a passed tile, two failed).
- **Tamsin first:** an unarmed healer fell first in 1 of depleted's 73 games lost before the swallow. The captain's stage-1 falls followed a healer's in 7 of 26. Depleted is not losing its heal clock.

## Open, for the Table

- Every board lever (entry 0381, spacing here) and both planner reads are spent; #692 rules out scaling. Per round 539, depleted's gate waits on the arts read (Chat files it now that #1441's reads close), then stands at 120 if arts reach it, or is argued down against chairs with the number recorded.
