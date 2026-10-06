# 0275 — The drill's print; the drill is not the cause, the cold hand play decides the bar

Date: 2026-10-06. Issue #1181, Design Table #1151 round 396 (Chat). Restates the round's read; no new lever.

## Context

The chip (0274) moved Teodor's kept rank points and not his level (p50 L6, p75 L6). Chat read the levy floor's drill as the likely sink: `CampaignRecord.Drill` raises a levy member below the floor with `Exp = 0`, so a fed unit that ends a map below the next floor loses what he earned. Round 396 agreed one print before any lever, with the read set in advance: if the chipping chair's EXP zeroed by the drill through map 8 is at least 50 more than the even chair's at p50, the drill carries EXP over (its own issue, under the even ceiling's tripwire); otherwise the cold hand play decides the bar as agreed in 395.

## Decided

- **The print is built** as `--levels --drill`: `LevelRun.Measure` splits `AfterBattle` into `Fought` and `Drill` and records the fed unit's `FedExp(Deployed, Earned, Zeroed)` per won map on every chair. It runs the chipping chair and the even chair on the same seeds.
- **The read** (`docs/measurements/levels-drill-1181.txt`, 200 seeds; 82 chipping and 81 even runs with Teodor after map 8): EXP zeroed per run, chipping p50 74 (p75 92), even p50 36 (p75 61). Zeroing lands mostly at the camps after maps 4 and 5 on both chairs. EXP kept per deployed map is p50 0 on both (p75 32 chipping, 10 even): on most maps he earns nothing the record keeps.
- **Verdict (round 396, agreed before the numbers):** 38 apart, under 50. The drill is not the cause, and the cold hand play feeding Teodor through map 8 (Chat's chair) decides the bar. The drill is unchanged.
- The floor, the curve and kill EXP are untouched, so the even-ceiling tripwire does not fire.
- Chat's lean to carry EXP over on its own merits (a hidden cliff, #485) is not decided here; it stays open on the Table.

## Kill criterion

As 0272 to 0274. The bar is decided by the owed cold hand play, not by a chair.
