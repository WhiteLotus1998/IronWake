# 0211 — Kinsbane's teeth on 2,2,1,3,4, woken at 12, and the field bar moved to the keep (#856; amends 0210)

Date: 2026-10-03. Built by the chain Builder. The lever is round 263's clause ("2,2,1,3,4 if the next read's median at the field is under 13"), proposed in round 278 and agreed in round 279. The bar's restatement is Chat's, from round 279, posted before this read and accepted in round 280. Both were settled before the numbers came in.

## What is built

- **The schedule.** `Kinsbane.ToothAt` goes from 2, 4, 6, 9, 13 to 2, 4, 5, 8, 12 (steps 2, 2, 1, 3, 4). It wakes at 12. A passed Keziah still comes back at the fourth tooth (`FedFor(4)`), which is now 8. Drain 5, the reach gate (0210), beside, the bind, the hunt and the heal of 10 are all unchanged.
- **`--kinsbane` prints the runs woken by each map's end** on the scythe arm. Woken by the field's end is the same as woken by the keep's first phase.

## The bar (round 279)

"Woken by the field on the median" was set in round 263, when the scythe was all she held. With the axe beside it, a fifth of her field swings are iron, and that is the feed-or-survive choice doing its job. The bar is now **four teeth by the field at p50, and woken by the keep's first phase in at least a third of the runs that reach the keep**. Sallow at 40 of 82 or better and the keep no worse than the axe still stand. Meeting the old bar would also have counted as a pass.

## The reading (200 runs each, same seeds, `docs/measurements/kinsbane-856-schedule.txt`)

| Map | Scythe won | Axe won | Fed p50 (teeth) | Woken by its end | Her attacks (scythe) |
|---|---|---|---|---|---|
| 6 the raid | 82 | 82 | 3 (1) | 0 | 250 (214) |
| 7 Sallow Grange | 44 | 49 | 5 (3) | 0 | 169 (102) |
| 8 Brackwater | 44 | 49 | 6 (3) | 0 | 44 (35) |
| 9 the field | 43 | 49 | 10 (4) | 17 | 232 (189) |
| 10 the keep | 9 | 7 | 12 (5) | 9 | 49 (26) |

The control arm reproduces 0207, 0209 and 0210 row for row.

- Sallow: 44 of 82 (bar 40). Met.
- Four teeth by the field at p50: fed 10 against the fourth tooth at 8. Met.
- Woken by the keep's first phase: 17 of the 43 runs that reach the keep (40 percent; bar a third). Met. Under 0210's schedule the same count was 10 of 44 (23 percent).
- The keep: 9 of 43 reachers (21 percent) against 7 of 49 (14 percent). Met.
- The old bar, woken at the field on the median, still misses (fed p50 10 against 12).

One field run fewer than 0210 is won (43 against 44). On the 43 seeds won under both, fed p50 is 10 in each, so the schedule moves the wake and not the kills.

**Round 279's composition check.** On the 29 seeds that won the field under both 0209 and 0210, field fed p50 is 10 in both arms. The 15 runs that only 0210 brings through are not less fed (p50 10). The gate cost no feed. 0209's printed 11 was the floor-index percentile over 30 runs.

## What follows

All four clauses pass, so the levers stop here (round 279). Drain 3 and the starved Mt -3 are not built. 13.23 stays provisional on its keep clause, a journal showing the hunger deciding a turn, and Chat's cold Sallow and cold field are the deciding plays. The passing read that Keziah's quest 2 (#635) waited on now exists, and its Table round is next.

No new hand play. Replaying Code's 854 Sallow script under the new schedule gives the same transcript byte for byte, because she ends that map at fed 2. The schedule only shows from the fifth kill on, at the field and the keep, and a `--from the_field` start gives a picked Keziah a fresh blade, not the median 6. So a warm hand play of the field would not test it. Chat's cold field, through the campaign, is the play that reads it.
