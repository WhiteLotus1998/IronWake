# 0344 — The Burned Shrine's wake read: the Sim cannot win the shrine, so a chair decides

Date: 2026-10-08. Issue #1378; Design Table #1368, rounds 482 (Code), 483 (Chat) and 484 (Code). Restates the Table's bar and records the read; no rule or content changed.

## Context

Code's warm floor play 1390 woke Kinsbane on Keziah's own quest, the Burned Shrine, before map 8: seven kills, all hers, fed 6 to 13. Round 483 called the waking a feature if it costs the player something, refused a side-map wake cap (a tooth the forecast promises always grows), and set the bar before the numbers. Read the campaign with `keziah_1` taken at its seat. Woken by the shrine's end in at most a third of the runs that take it is a feature, woken at p50 is a hole, and anything between goes to a cold chair. Keep wins are split by woken on the shrine, against 0212's 13 of 43. Round 484 added that every side map before the shrine is taken at its seat.

## Decision

- `ironwake-sim --kinsbane --quests` is the quest arm (`KinsbaneRun`, Sim only). At each camp it takes every side map offered, in the order offered, its member beside the first standing recruits who are neither the member nor the captain, up to 50 tries, and leaves one that is refused or never won. It stops after it has tried `keziah_1`. It prints the shrine's feed entering and leaving, every try (won or lost) with its feed, turn and cause, the runs woken by its end, and the keep split.
- No lever is built. The shrine and the tooth curve are unchanged.

## Measured (`docs/measurements/kinsbane-1378-shrine.txt`, 200 seeds)

- The shrine was offered in 34 runs (after map 7), with 3 side maps won before it at p50. **The heuristic player won it in none of them: 0 of 1700 tries.** 1698 were lost on Keziah's fall (ended turn p50 4) and 2 on the clock.
- Fed entering the shrine p25 4, p50 5, p75 6 (1390's floor took 6). By a try's end, p50 5, at most 9. **No try woke the scythe.**
- The keep column has no shrine winners to split. 5 of the 34 shrine-offered runs won the keep, against 13 of 33 woken entering the keep on the main line.
- The main line under the arm: Sallow 34 of 82, the field's feed p50 10, woken by the field's end in 13 of 33.

## Reading

Round 483's bar measures the accidental waking over the runs that win the shrine, and the Sim has none. So the bar reads neither feature nor hole. It lands in the "between" clause, and a chair decides. What the Sim does say is that a player who takes the hunt without chasing it does not wake the scythe on the shrine: the most any try fed was 9, three short of the wake. The only waking on record is a hand chasing the fifth tooth (1390), which is the choice round 483 wants to keep. 0 of 1700 is also the shrine's known Sim floor (gate 1 0/200). The heuristic walks an unfed Keziah into the nave and loses her, so the number is about the Sim, not the map.

## Next

A cold chair at the shrine's seat: the campaign's company after map 7, Keziah fed 5 (the read's p50 entering), any ally, the chair not told to chase the wake. If it wakes the scythe without chasing it, the lever is the shrine's numbers (round 483: one fewer feedable body in the nave, or the acolyte), never a cap.
