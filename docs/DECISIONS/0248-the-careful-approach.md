# 0248 — The Sim's recruits stop short of a lethal approach

Date: 2026-10-05. Round 368 (#1033); #1044 step 1 (`docs/measurements/field-rook-arm-1044.txt`), built as #1054.

## Context

The heuristic's veto (the captain and a `protect:` unit) refuses a tile whose no-crit `Exposure` sum reaches the unit's HP. Every other recruit took section 8's approach with no check at all, so the fastest one arrived alone. On the field with Rook's pick, Rook (move 7) flew to 7,13 on turn 1 and died on turn 1 or 2 in 30 of 30 traced seeds. Gate 1 on that arm measured a recruit's speed, not the map.

## Decision

- **A recruit outside the veto takes section 8's approach unless that tile's no-crit sum reaches its HP; then it takes the captain's approach key, lethal tiles last.** Strikes stay unvetoed, so the Sim stays crude. Gate 1 measures a crude player, never a blind one (round 368). Tests: `HeuristicApproachTests`.
- **The route split on the field is no longer evidence.** The Sim now goes south in nearly every game on every arm. Route claims come from hand plays and `--trace`, never from `--full`'s line/south columns. 0233 stands, since it was tuned on hand plays and Keziah's arm passes gate 1 at 134.
- **The full-campaign parity script moves from seed 631 to seed 644** (same variant 66). 631 loses on map 8 under every variant with this planner. 644 wins all ten maps and takes every line `FullCampaignTests` asserts. `.github/workflows/ci.yml`'s godot campaign step changes its path and seed only.
- **Gate 1 moves are recorded, not chased** (#971): see the Maps cells and the numbers below.

## Measured (`--full --all`, 200 seeds, Release, two-roll average)

| map | gate 1 before | after | gate 4 |
|---|---|---|---|
| the_tollgate | 158 | 167 | ok, unchanged |
| the_field (Keziah's arm, the file) | 122 | 134 | ok |
| harrow_weir | 143 | 137 | ok |
| old_mill_road | 65 | 57 | ok (gate 1 already under 60) |
| brackwater_cut, sallow_grange, saltmarsh_ford, starting_alone, the_mill | 129, 161, 50, 1, 133 | unchanged | saltmarsh still fails at 0.075 |

Rook's arm on the field (the spike's row, `field-rook-arm-1044.txt`): 49 to 86, her attacks 76 to 426. Rows: `docs/measurements/careful-approach-1054.txt`. Harrow and Old Mill Road dropping is a planner that won't suicide sometimes waiting into a timeout. It is noted in their cells and not chased (round 368).

## Next

#1044 step 1b: Rook seated with `seen_far` off, to split the header's cost from her kit, then at most one pick-keyed lever measured both ways (round 368).
