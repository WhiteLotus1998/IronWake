# 0271 — The rank trace: falls and the bench eat Teodor's rank, and the Recalling bound is still short of C

Date: 2026-10-06. Issue #1170, Design Table #1151 round 393. Provisional (a measurement; nothing ships).

## Context

The strike-then-finish chair (0270) read Teodor at p50 12 main-weapon rank points after map 8, against about 48 that two combats a map over eight maps should pay. Round 393 agreed to trace the gap before sizing any rank lever, and fixed the read before the numbers: a count that does not reconcile is a bug and goes first; if the Recalling player's bound (kept plus lost to a fall) reaches C (80) at map 8, no rank lever; otherwise a rank lever is right, a door-only shape preferred over 3 to 4 a strike, with `--ladder` and both ceilings re-read.

## Decided (the Builder)

- **`--levels --rank-trace [--seeds N]`** runs the striking chair alone, its seeds split over four threads (each run depends on its seed alone; the price line matches 0270's exactly). Per map, for the fed unit: deployed or benched; combats on each phase and those he struck in; main-weapon points earned in the won battle, kept by the record, lost to a fall; points earned in the map's lost tries; any change at the camp. `LevelRun.RankTrace`.
- **Reconciliation** is per map: kept equals earned when he stood, 0 when he fell; anything else is printed as unexplained. Camps that move his points are counted.
- **Sinks** are printed in points a run: lost to a fall; strikeless combats at 3; maps benched at his mean earned on a deployed map. The largest is named in the verdict.

## The read (`docs/measurements/levels-rank-trace-1170.txt`, 200 runs, 82 reach map 8)

- Reconciles on 656 of 656 maps; no camp moves his points. Not a bug.
- Deployed on maps 3 to 7 only (5 of 8): map 1 is the captain alone, maps 2 and 8 (the Mill, Brackwater) seat no Teodor.
- Per run: combats p50 24, struck 17, strikeless 8 (enemy-phase hits he could not answer). Points earned p50 55, kept 12, lost to a fall 39; a further p50 45 earned in lost tries (map 7, Sallow, is most of it).
- Falls: 78 of 82 on Saltmarsh, 70 on Sallow, 63 on the raid.
- The Recalling player's bound: p50 55 p75 63. Short of C.
- Sinks, mean points a run: fall 38.1, bench 33.6, strikeless 23.7.

## What it means

The 48-to-12 gap is mostly the Sim's Recall stand-in (falls), then the bench; strikeless combats are real but third. Even a player who Recalls every fall reaches p50 55, so by the agreed read a rank lever is right. Five deployed maps at about 11 earned each is the shape the lever has to meet: C needs about 16 a deployed map, so 3 to 4 a strike alone (to about 73) would still miss, which favours the door-only shape. The shape goes to the Table.

## Kill criterion

Revisited if a lever's read shows the fall share changing (the chair's veto or the Sim's Recall stand-in changing), since the bound is read on top of it.
