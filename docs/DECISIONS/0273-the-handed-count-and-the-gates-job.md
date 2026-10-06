# 0273 — The handed count reconciles; the gate stays 50, level is the investment half

Date: 2026-10-06. Issue #1177, Design Table #1151 round 395 (Chat). Restates the round; #1178 (the chip) follows.

## Context

Round 395 read the points gate (0272): the rank half passes at its margin, the level half fails (Teodor p50 L6), and the even chair's best non-captain keeps p50 52 points. Chat also asked why every price line printed `kills handed ... (total 0)` while the header counted 15 to 25.

## Decided

- **The count was right; the label was not.** The `(total 0)` followed `given up` and was that figure's total. The header sums every won map of every run; the price line is per run over the runs that won map 8, through map 8. The price line now prints a total for kills handed as well, and `--levels --focused` and `--levels --gate` print the split: on the striking line, 15 in all, 9 through map 8 on the 82 runs that won it, 6 on later maps or runs lost before it; the per-map prices sum to each run's count (`docs/measurements/levels-gate-1177.txt`). A split that does not sum is printed as a bug (tested).
- **The gate stays at 50** (round 395). Rank points measure use and survival, not investment: no number separates fed Teodor's bound (p50 55) from the even chair's best (p50 52, p75 73). The gate is the "has used the weapon" half; level is the investment half. It is not raised to chase the even best.
- **The ceiling's tripwire:** any change to the levy floor, the EXP curve or kill EXP re-reads the even chair on L7 alone before it lands, and it must stay p50 0.
- **Next:** the striking chair gains the chip (#1178), read once on Teodor's level after map 8. A cold hand-play chair feeding Teodor through map 8 is owed either way and decides the bar if the chip read fails.

## Kill criterion

As 0272. The chip read's verdict reads the handed total this record fixes.
