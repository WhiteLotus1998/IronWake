# 0269 — The levy floor: the camp raises the levy to map number less two

Date: 2026-10-06. Issue #1164, Design Table #1151 round 391. Provisional.

## Context

The focused chair (0268) failed round 389's bar with Teodor at p50 L1 after map 8: lever 1 ("the joins' levels and ranks") reaches only joiners, and the levy (Wren, Teodor, Ottilie, Pell, Dunstan, Brannock) is on the roster from map 1 and never joins. Round 391 weighed seating the levy along the curve against feeding the picked claimant instead.

## Decided (the Table)

- **The levy floor.** Before each main map N (from 1), every living levy member below level N less `levyFloor` (2) is raised to it on the average growth, EXP zeroed, weapon ranks untouched; benched and wounded count. Printed at camp, one rules line a unit: `Teodor drilled with the levy: L2 -> L6.` No scene, no voice line.
- The offset is the tuning number; the even chair's p50 0 at L7+C after map 8 binds it. If that moves, the offset steps to three before anything else.
- Feeding the claimant instead is declined: the second tier is not a claimant's privilege.
- If the paying line still fails with no tile leading its refusals, a chip-then-finish chair comes before lever 2.

## Decided (the Builder)

- **The levy is defined by content:** a cast member, not the captain, whom no campaign map names in `arrives`, `joins`, `branch` or `meets` (`CampaignRecord.IsLevy`). A content set whose campaign names fewer arrivals drills more members.
- The drill runs inside `AfterBattle` (`Fought` then `Drill`), so the console, the presenter and every Sim harness get it alike; the camp line is an event-log line right after the win line in both. A campaign opened with `--from` fights on its named roster and is not drilled until its first win.

## The read (`docs/measurements/levels-floor-1164.txt`, 200 runs)

- Even (the ceiling): 262 handed; after map 8 p50 0 at L7+C. **L7 alone p50 6**, rank C alone p50 0. The ceiling holds, on rank only.
- Paying: 25 handed; refused 451 no tile, 452 kill chance, 24 exposure, 105 forecast death. p50 0 at L7+C; the bar fails. After map 9: p75 1.
- Price after map 8 (82 runs): captain p50 L7; Teodor p50 L7 (was L1), main-weapon rank points p50 11 (C is 80); 0 kills given up; p50 73 HP lost in enemy phases, 243 falls.

## What it means

The floor fixed the level half and the bench: every levy member stands at L7 after map 8's win, because "after map 8" is read on the record after the camp that drills for map 9 (floor 7). The bar is now held shut by rank alone, and rank only moves with use. No tile and kill chance are tied as the top refusal, so per round 391 the next move is a chip-then-finish chair. One question goes back to the Table: round 391 put the floor "under the bar, never at it", reckoning L6 at map 8; read where the bar is read, the floor is at the level half. Offset three keeps it one level under at the read point.

## Kill criterion

Revisited if the chip-then-finish chair passes the bar with the floor at three, or if a hand campaign journal says the drill made the bench free (a levy member never deployed still fights at the floor without cost).

## Amended (issue 1166, round 392)

- **The offset is three.** "Under the bar, never at it" is read where the bar is read: the record after map 8's win, already drilled for map 9. At two that record stood at L7 for every levy member, the level half issued. At three it is L6, one level fed. The bench pays a level (L5 at map 8) and stays deployable.
- **The ceiling reads both halves.** The even chair must stand at p50 0 on L7 alone as well as on L7+C after map 8; if either moves, the offset steps before anything else.
- **The read** (`docs/measurements/levels-floor3-1166.txt`, 200 runs): even 275 handed, after map 8 p50 0 at L7+C and p50 0 at L7 alone (was 6); the ceiling holds on both halves. Paying 19 handed, refused no tile 461, kill chance 453, exposure 23, forecast death 103; p50 0 at L7+C, the bar fails. Price: captain p50 L7, Teodor p50 L6 (rank points p50 11), 0 given up, p50 73 HP lost, 251 falls.
- **Next** (round 392): the strike-then-finish chair (#1167) in place of chip-then-finish: Teodor strikes every planned target the paying guard allows, kill or not, and prints strikes per map; its read decides whether rank takes its own lever.
- **The parity script moves.** At offset three seed 644 wins the campaign only on a variant that takes no side maps by itself, so `full-campaign-644.script` is written by `--variant 21 --quest pell_1` (the header names the flag) and still takes every camp action and order the test lists.
