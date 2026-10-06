# 0276 — The drill keeps EXP; the camp row prints what was kept

Date: 2026-10-06. Issue #1184, Design Table #1151 round 398 (Chat). Restates the round; the tripwire read is below.

## Context

The levy floor's drill (0269) raised each levy member below the floor with `Exp = 0`. 0275 found it was not what holds Teodor's bar, but Chat's round 398 kept the question apart from the bar: a camp that erases earned EXP without a word is a hidden cliff (#485). The field 1460 (#1183) showed it in a real row: Teodor's four kills and 36 EXP, and Pell's 99, gone, the line reading the same as for units who never deployed.

## Decided

- **The drill raises Level to the floor and leaves Exp alone.** Exp is under 100, so nothing overflows, and a two-level raise keeps the same remainder as a one-level raise. Stats still come from the average growth (`Unit.AtLevel`); weapon ranks untouched.
- **The row names it:** `Teodor drilled with the levy: L6 -> L7, 36 EXP kept.`; no suffix at 0 EXP. CLI and client share `CampaignSession.DrillLines`, so the full-campaign parity holds.
- **The tripwire (396) holds** (`docs/measurements/levels-keep-exp-1184.txt`, 200 runs, `--levels --focused`): the even chair after map 8 stands at p50 0 on L7 alone and p50 0 on L7+C, as under 0269. So carry-over lands; the "EXP lost to the drill" arm is not built.
- The bar is unchanged: Teodor p50 L6 on both chairs (paying, striking); Chat's cold chair through map 8 still decides it (0275).
- The Sim's `--levels --drill` now counts what the drill removes (0 under this rule).

## Kill criterion

Re-read under 0269's tripwire: if a floor, curve or kill EXP change moves the even chair off p50 0 on L7 alone after map 8, the carry-over is the first thing to revert, and the row then prints the loss.
