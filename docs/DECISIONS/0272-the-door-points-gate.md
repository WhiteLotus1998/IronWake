# 0272 — The door's points gate: the ten level-7 doors ask 50 rank points, not rank C

Date: 2026-10-06. Issue #1174, Design Table #1151 round 394 (Chat). Provisional; the number is tunable.

## Context

The rank trace (0271) put the Recalling player's bound at p50 55 after map 8, short of C's 80, so a rank lever was right, and round 393 preferred a door-only shape. Round 394 chose it: a points gate on certification, not 3 to 5 a strike, because a strike-pay change moves the captain's sword, the captain's forms' sword C at L10 and every C specialty's arrival on the tuned maps. Chat set the number inside the bound with margin (50, about the bound's p25), since the bound is a paper player and the chair's falls overstate a careful player's losses.

## Decided

- **`certification.points`** (classes.json): weapon type to a rank-point minimum, accepted only strictly between D's 30 and C's 80 (`CertificationRequirements.IsGate`), never a type `ranks` also names; the loader names file, entry and field on a refusal.
- **The ten level-7 doors** on the eight tier-1 bases (Halberdier, Berserker, Marksman, Scholar, Warden, Field Surgeon, Lancer, Sky Captain, Drake Warden, Sentinel) ask `points: {<main>: 50}` in place of `ranks: {<main>: C}`. The captain's level-10 forms keep sword C. Strike pay, the letter thresholds and every weapon's rank are unchanged.
- **On screen.** `classes` prints `lance 50 (between D and C)`; a refusal reads `needs lance 50, has 12` (requirement key `points.lance`); the camp row of a unit at L5 or above whose class has a gated door ends `door: lance 41/50`, one entry per type and gate the doors share.
- **The read, `--levels --gate`**, runs the striking and even chairs over the same seeds and prints round 394's verdict, agreed before the numbers: the bar passes when the Recalling bound at p50 clears the gate by 5 and the fed unit is at p50 L7 (kept printed, not deciding); the ceiling holds at even p50 0 on L7 and the gate and on L7 alone, a break stepping the floor's offset first; the pick at p50 L7 and the gate on the even chair raises the gate first. `LevelRun.Gate` and the content are held equal by a test.
- **The parity campaign moves.** On variant 21 the gate lets Ottilie take Marksman at the camp where the old script advanced Pell to Scholar, and seed 644 then loses the keep; `full-campaign-644.script` is written by `--variant 20 --quest pell_1`, which wins and still takes every camp action and order the test lists.

## The read (`docs/measurements/levels-gate-1174.txt`, 200 runs, 82 reach map 8)

- Striking: Teodor p50 L6; kept p50 12; the Recalling bound p50 55 p75 63, exactly gate plus margin.
- Even: p50 0 at L7 and the gate, p50 0 at L7 alone; the best non-captain's points p50 52 p75 73, so the even chair's best unit is over the gate and the ceiling now rests on level alone.
- The pick (Rook) on the even chair: p50 L4, 30 points; short of the door.
- Verdict: the bar fails on the level half only (Teodor p50 L6, needs L7); the ceiling holds; the pick stays short.

## What it means

The rank half is met at its margin for a Recalling player; what the bar now lacks is the one fed level 392 meant offset 3 to demand, and the striking chair feeds strikes, not levels. By 393's rule the level half goes back to the Table.

## Kill criterion

The gate rises if the pick reaches L7 and the gate on the even chair; the floor's offset steps if the ceiling breaks on either half. A hand play that reaches the door with no feeding reopens the number.
