# 0274 — The chip is read; Teodor stays L6, the cold hand play decides the bar

Date: 2026-10-06. Issue #1178, Design Table #1151 round 395 (Chat). Restates the round's read; no new lever.

## Context

Round 395 found the striking chair's finishing half close to dead: it handed 15 kills in 200 runs and refused 403 offers for no tile and 377 for kill chance. Chat named the missing order: a real player feeding a pikeman has someone chip the target into his one-hit range first. The round agreed to add the chip, read it once, and let a cold hand play decide the bar if it fails.

## Decided

- **The chip is built as a fourth focused guard, `Chipping`** (`FocusedPlayer.Chip`, `ChipFrom`). It plays the striking line, but before Teodor's own strike it tries a chip. The chip fires when no kill is handed, the plan is a plain attack by another unit, and Teodor cannot kill the target on a hit from any tile. A non-captain other than Teodor then strikes first: the planned attacker if it qualifies, otherwise the next in roster order. It strikes from its best-scoring tile that meets the paying guard: at most one more enemy in reach than the planned tile, no forecast death without a crit, and the strike lands. The hit must not kill on its own, and it must leave the target killable on a hit by Teodor. He finishes on a later decision under the existing hand rule. `Striking` is unchanged, so the same read prints both lines.
- **The read** (`--levels --chip`, `docs/measurements/levels-chip-1178.txt`) ran on 200 seeds; 82 runs won map 8. The chip fired 365 times. Kills handed went from 15 to 26 in all, and from 9 to 12 through map 8. Points kept went from p50 12 to p50 19 (p75 30). Teodor's level is still **p50 L6, p75 L6**. Kill-chance refusals went *up*, from 377 to 495, because chipped targets are now offered to him and then refused on the paying gap. No-tile refusals barely moved (403 to 412). Unfed, Teodor keeps p50 8 points on the even chair (p75 17).
- **Verdict (round 395, agreed before the numbers):** the bar fails on the level half. The refusals do not read as positioning, since kill-chance refusals did not collapse. So the cold hand play feeding Teodor through map 8 decides the bar. Nothing is tuned to make a chair pass, so the paying gap stays at 15 points.
- The floor, the curve and kill EXP are untouched, so the even-ceiling tripwire does not fire.

## Kill criterion

As 0272 and 0273. The bar is decided by the owed cold hand play, not by a fifth chair.
