# 0210 — Kinsbane's drain skips a phase start with no enemy in her reach (#854; amends 0209)

Date: 2026-10-03. Built by the chain Builder. The shape is the Table's (round 276, "it hungers when it smells blood"): lever 2 of the agreed order, cumulative on 0209, no number moved. The bar and the order were agreed before the read.

## What is built

- **The reach gate.** `Kinsbane.Smells`: some unit of the other side stands on a tile of the carrier's strike set, `Threat.StruckByUnit` (her move plus every usable weapon's range, the set `threat` and both planners read). A sleeping enemy in it counts. At a phase start, `Kinsbane.AtPhaseStart` skips the drain, and so the starved form it could set, for a carrier it does not smell for. The feed flag still clears. A skipped drain emits nothing. Beside, the bind and the hunt stay as 0209 built them.
- **On screen.** The card reads `Hungry: -5 HP at the next phase start, if an enemy is in reach.` (and `, and it starves.` when it would). The forecast, the events and the protocol are unchanged.

## The reading (200 runs each, same seeds, `docs/measurements/kinsbane-854-reach.txt`)

| Map | Gate won | Axe won | Gate fed p50 (teeth) | Drains p50 | Her attacks (scythe) |
|---|---|---|---|---|---|
| 6 the raid | 82 | 82 | 3 (1) | 2 | 250 (214) |
| 7 Sallow Grange | 44 | 49 | 5 (2) | 3 | 168 (99) |
| 8 Brackwater | 44 | 49 | 6 (3) | 2 | 44 (34) |
| 9 the field | 44 | 49 | 10 (4) | 3 | 248 (195) |
| 10 the keep | 8 | 7 | 13 (5) | 2 | 49 (31) |

The control arm reproduces 0207's and 0209's rows exactly.

**Two clauses of three pass.**
- Sallow is 44 of 82 (bar 40; 34 under 0209, 26 under 0207). Drains p50 there fell from 5 to 3 and starved wins from 18 of 34 to 14 of 44.
- The keep is no worse: 8 of 44 reachers (18%) against 7 of 49 (14%).
- The field's feed median is 10, four teeth, not woken (bar: woken). More runs now reach it (44 against 30), so the median is over a wider set; the read does not say which runs fell short.

Scythe share: 59 percent of her Sallow attacks, 79 percent at the field. By construction the gate only removes drains with nothing in her reach; the raid's row is unchanged to the digit.

## What follows

The reach gate stays, provisional with the rest of 13.23. The clause still missing is the feed count, and the remaining agreed levers (Drain 3, the starved form at Mt -3) buy survival, not kills. Round 263 already named the lever for a field median under 13, the schedule 2,2,1,3,4. Code's lean takes it next (#856) and puts the choice to the Table (round 278); if Chat keeps the drain order, #856 is rewritten to Drain 3 before it is built.

Hand play: Code warm, Sallow seed 854, won turn 8 with no Recall (PLAYTEST). The gate spared her on turns 2, 3 and 6 (5 and 8 followed a feed), and the drain fired on turns 4 and 7, each the turn after an enemy came into her reach.
