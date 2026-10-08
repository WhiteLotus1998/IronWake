# 0363: Frozen Iron lands once a round; the keep's whole-fight read

Date: 2026-10-08. Issue #1395, the one-pass keep tuning Lotus asked for (relayed on #1368 2026-10-08 22:09Z). Numbers stay provisional on #1247.

## Decided

- **Frozen Iron lands at his side's phase start alone**, once a round, not at every phase start of either side. He casts it, and the Table's own arithmetic counted it that way: Chat's round 487 ("Hask, with his heal, has lost nothing net by then": +4, +2, 0, -2, -4 against a heal of 6) and Code's round 488 (the crossover at phase 3) both put one landing against each heal. #1385 built two, which doubled the clock on the company and put him a net -8 down by his second phase. The doses (2, +2, at most 10) are unchanged. The landing still comes first and the heal after it.
- **The Sim's stage line names the company the swallow found**: the median units standing and the captain's HP.

## What the read says (`docs/measurements/keep-1395-pass.txt`)

- The sample as shipped reads 0 / 0 after this change (1 / 0 before). The clock was not what held it there.
- **The swallow finds the company spent.** Median 4 of 12 standing, captain at 13 to 16 HP, on turn 9 or 10 of 12. Stage 2 finishes whatever is left.
- **No stage-2 number alone moves the read far.** Stage 2 at Def +0, at HP 20, or the clock halved to 1, +1: at most 16 / 6 on the pitched stage 1.
- **The best screens use several levers at once.** Stage 1 at the stand-in's bulk (keeping lance A and the line), stage 2 at HP 24 and heal 4: 56 / 16 at 200 seeds. The same with the turn-5 assault wave cut: 76 / 38. Limit 14 or 15, Hask on turn 4, or `enemy_level: 5` each add single digits.
- So gate 1's 120 is not in reach from Hask's numbers. The finale's attrition (the waves, then a 44-HP stage 1 with the line) is what spends the company. The lever is the keep's shape, and that goes to the Table (round 501) before it is built. The campaign keep keeps the stand-in (0346) until the pair passes.
