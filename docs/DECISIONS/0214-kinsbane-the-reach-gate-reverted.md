# 0214 — Kinsbane's reach gate is reverted, and Sallow Grange asks before she marches (#871; amends 0210)

Date: 2026-10-03. Built by the chain Builder. The ruling is Lotus's, relayed on the Design Table (#820, round 288): "Keep the drain when no enemies are nearby. This makes you decide when you want to take her out." Chat wrote the issue's shape and bars before this read, and Code's build note (`march sure` only) is on #871.

## What is built

- **The revert.** `Kinsbane.AtPhaseStart` drains again at every phase start after the first, whether or not an enemy is near, as 0209 built it. `Kinsbane.Smells` stays in the code as a measurement and is never a rule. The card reads `Hungry: -5 HP at the next phase start.`, and the weapon's description drops its reach clause. 0211's teeth, 0212's hunt, Drain 5 and the heal of 10 are unchanged, and the levers stay stopped (round 284).
- **The header.** `keziah_warning: on`, in snake_case like every other map header, where the issue wrote `keziahWarning: true`. The warned unit is whoever `kinsbane` is `boundTo` in `weapons.json` (`Kinsbane.Bearer`). The flag is refused with a named error in three places: on a `deploy: all` map, on a map that places the bearer by name (both in the map parser), and on the bearer's own side maps (`validate` and the quest loader).
- **The confirm.** If the bearer is on the living roster, the camp's Roster panel says before seats are chosen: `This map is not ideal for Keziah; march asks first if Keziah deploys (bench keziah to leave her).` With her in the deployment, a bare `march` prints Lotus's line verbatim, `This map is not ideal for Keziah. Are you sure you want to continue with her?`, and the screen stays open. Only `march sure` answers it. A second bare `march` asks again, and `bench keziah` removes the question. A strict script stops on the bare `march` and names the line. The record keeps the map index the answer was given on (`warningConfirmed` in the record JSON), so the question comes once per map and a retry never asks again. Recall happens inside the battle and never reaches `march`. The client has the same check (`March(sure)`, script word `march sure`). The Sim's campaign scripts answer `march sure`, because the Sim fields her.
- **The read.** `--kinsbane` prints walking drains: drains paid at a phase start where `Smells` is false on the board the drain fired on. It also prints her falls per map (the tries she fell on), and it has a third arm, `--heeding`, which benches her on every flagged map.

## Which maps

Walking drains p50 on the committed arm (`docs/measurements/kinsbane-871-revert.txt`), over the five maps she can enter:

| Map | Walking p50 (p75) | All drains p50 | Flagged |
|---|---|---|---|
| 6 the raid | 0 (0) | 2 | no |
| 7 Sallow Grange | 2 (2) | 5 | **yes** |
| 8 Brackwater | 1 (1) | 3 | no |
| 9 the field | 1 (2) | 4 | no |
| 10 the keep | 0 (0) | 2 | no |

Only Sallow reaches the bar of 2, which is 10 HP lost to walking on the median run. The cap (a third of five maps, so one) does not bind. Chat's guess from 0209 against 0210 named the field too. The field's p75 is 2, but its median is 1, so it stays unflagged. The shipped keep is not `deploy: all`, so the keep counts toward the five and could be flagged; it reads 0.

## The read (200 seeds, all three arms)

| Map | Committed won | Heeding won | Axe won | Committed fed p50 (teeth) | Her falls (committed / heeding / axe) |
|---|---|---|---|---|---|
| 6 the raid | 82 | 82 | 82 | 3 (1) | 210 / 210 / 258 |
| 7 Sallow Grange | 34 | 82 (benched) | 49 | 5 (3) | 796 / 0 / 866 |
| 8 Brackwater | 34 | 82 | 49 | 6 (3) | 93 / 169 / 89 |
| 9 the field | 31 | 64 | 49 | 10 (4) | 274 / 915 / 378 |
| 10 the keep | 9 | 12 | 7 | 12 (5) | 207 / 330 / 194 |

The axe arm reproduces 0211's control rows row for row.

**279's clauses, committed.** Four teeth by the field at p50: fed 10 against the fourth tooth at 8. Met. Woken entering the keep in a third of reachers: 12 of 31 (39 percent; 0211 had 17 of 43). Met.

**Against 0211 and 0212.** Sallow falls back to 34 of 82, 0209's number (0211 had 44). The walk now costs her 2 drains on the median run there, which is the tax Lotus asked to keep. Sallow's 40-of-82 bar is retired because Sallow is flagged, and the confirm is the answer to that tax, not a lever. Brackwater and the field each lose about ten runs to the attrition carried down from Sallow. The keep wins 9 against 0211's 9.

**Heeding, report only.** The bar was that the heeding arm's campaign wins are within noise of the axe arm's. Heeding wins the campaign 12 times against the axe's 7, so it is above the axe, not just level with it. Benching her at Sallow lets every run through to Brackwater with her at full HP. She reaches the field in 64 runs and the keep at fed 12 in 9 of those 64. At the field her median is 9 fed and four teeth, so heeding costs about one kill against committed and buys 30 more reachers. "Ended starved in 7" on the benched Sallow row is the stack carried from the raid, not a fight there.

## What follows

13.23 stays provisional. Its keep clause is still a journal showing the hunger deciding a turn: Code's warm Sallow 871 has two (the fort detour, and the hexer at 6 HP), so a cold chair is what decides it. Chat's cold field at `--fed 10` stays next in Chat's queue.
