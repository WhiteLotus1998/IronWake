# 0358 — The seize step

Date: 2026-10-08. Issue #1409 (`bug`), from #1392 (DECISIONS/0357). Implementation, a planner fault. Amends DESIGN 11's gate 1 planner.

## What lost the art run

With casts on, `--campaign-script 644 ... --quest pell_1,maud_1,maud_2 --deploy maud --stop-after art` never reached the Psalter art.

- **The loss that mattered was the First Shrine (maud_2), not map 7.** The Shrine was lost on turn 10, so the Psalter was never paid. The run then marched into Sallow Grange without the art and lost there.
- **The hand play ran out a turn early.** The Tollgate's casts move EXP around, so Wren reaches the Shrine one level up. She kills archer-1 on turn 9 instead of turn 10. On turn 10 the hand play's `attack wren archer-1` had no target, and the heuristic took over.
- **The heuristic healed instead of seizing.** No enemy was left and the altar was two steps away, yet Maud salved Wren at 6,1. `PlanUnit` plans a heal below half HP ahead of the approach, and the approach is the only path that walks the captain to the throne. Strikes are also scored from every tile in reach.

## Decision

- **The seize step.** On a Seize map, a captain that has not moved and can end on a throne tile this turn plans its attack, cast or heal from that tile alone. If it has none, it waits there. A heal or a strike from another tile never costs the throne (`HeuristicPlayer.SeizeTile`).
- **On a plain Seize map the step is always taken,** since the step wins outright.
- **On a `seize_hold` map the step is taken when the throne's no-crit exposure stays under the captain's HP** (the veto's rule), **and on the last turn regardless,** because not stepping loses the map as surely as falling does. A captain already on the throne stays on it.
- The Psalter-art script (`tests/parity/campaign/psalter-art-644.script`) and the Lazar/Shrine Psalter test are rewritten with casts. `--no-casts` stays as a writer flag.

## Read

- **The art run:** all thirteen variants tried (32, 38, 40 to 47, 50, 56, 58) reach the art after 6 maps. With no deploy, variant 44 now also reaches it, on map 9.
- **`--full --all`** (main, then this change). Only the two Seize maps move:

| Map | Gate 1 | Gate 4 |
|---|---|---|
| the_tollgate | 190 to 191 | 0.280 to 0.285, ok |
| sallow_grange | 162 to 175 | 0.385 ok to 0.395 **FAILED on Wren** (0.130 to 0.120, se 0.038) |

  Wren on Sallow sits on gate 4's edge. 0349 failed her and 0352 passed her. A captain who takes the hall sooner needs her less. This is a reading for the Table, not a lever built here.
- **The keep** (`--finale`): 148 / 114 / 0, unchanged (Defeat Boss).
