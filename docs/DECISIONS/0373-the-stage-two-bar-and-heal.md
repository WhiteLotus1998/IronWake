# 0373: Hask's stage-2 bar 20, the Kin's heal 2, on the Warden sample

Date: 2026-10-09. Issue #1395. Code's round 516 lean, building Chat's lever order (round 514: HP, then the heal, then the step) with the race on (0372). Numbers provisional on #1247, like every stage-2 number.

## Decided

- **The screen** (`docs/measurements/keep-1395-bar.txt`): stage-2 HP 24, 20, 16 against heal 4, 2, 0, both arms, 200 seeds at L8. The target is a race, not a burst: won kills in 2 to 4 player phases.
- **Shipped: HP 20, heal 2** (`hask_warden`'s `swallow`). The read: **78 / 49**, from 39 / 33. Two thirds of won kills land in 2 to 4 player phases on both arms; a third are 1-phase bursts.
- **Why not the others.** Below HP 20 the race becomes a burst: at 16, under half the won kills take 2 to 4 phases. HP 24 heal 0 ties on wins (78 / 48) with a slower race, but it deletes the Kin's heal, a rule of #1385's stage, which is a Table call rather than a tuning one. HP 20 heal 0 reads 97 / 56 and is mostly bursts.
- **Still short of gate 1** (120) on both arms. No cell passes; the best, HP 16 heal 2, reads 102 / 60. The stage-1 timeouts (25 / 61) do not move with any cell, so the rest of the gap is stage 1 (round 513's fronts) and the step.

## Open, for the Table

- The step (Frozen Iron's +2 a round), Chat's third lever, read next at HP 20 heal 2.
- Whether the heal goes to 0 for the slower race (HP 24 heal 0).
- The clock-death gate on won games only (round 514's lean 2, round 516's ask). At HP 20 heal 2, full's won games read 56 / 17 / 5 (0, 1, 2+).
