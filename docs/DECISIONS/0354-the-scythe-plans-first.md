# 0354 — The scythe plans first when it can feed

Date: 2026-10-08. Issue #1395 (`bug`), slice 2, the follow-up 0353 named; Design Table round 497 ("if the Sim feeds it badly, that is a player fix, and the weapon still goes in"). This fixes the Sim's player and changes no rule. Amends 0209's hunt.

## The trace

The keep, the full company, 200 seeds. A scratch probe counted each of Keziah's player-phase decisions. It was not committed.

- **Her per-turn choices barely move.** With the scythe she attacks in 34 percent of her decisions; without it, 38 percent. Among her approaches, the careful (lethal-tile) share is about the same either way. So the planner is not refusing her strikes, and her approach is no more timid.
- **She feeds rarely.** In 691 of 1173 decisions the scythe is still unfed (fed 0), and it starves 55 times. Allies plan in roster order, and on most turns some of them plan before her. They take the kills her hunt would have taken on a hit (0209: the scythe swings only for a kill). She drains 5 a phase, sinks, and fights fewer turns (1173 decisions against 1392 without the scythe).

Planning-order arms on the same seeds (wins out of 200):

| Arm | Wins | Decisions at fed 2 or more |
|---|---|---|
| Kitted, as main | 149 | 110 |
| The hungry carrier always first | 149 | 198 |
| The hungry carrier always last | 135 | 17 |
| **First only when the scythe can kill on a hit** | **157** | **397** |
| No scythe | 153 | n/a |

## Decision

- **`HeuristicPlayer.PlanOrder(state, content)` moves a unit to the front when `CanFeed` holds.** `CanFeed` means the unit has not acted, carries a hungering weapon not yet woken, and that weapon kills a seen foe on one plain hit from a tile it may end on, one not left free for a corked captain and not refused by the ledger. Ties keep the existing order: the cork's captain-last rule and Escape's farthest-first rule are applied before it.
- This is what a human with the scythe does: give her the last hit first. The kill criteria in 13.23 and Spark Storm's numbers do not change, and neither do Kinsbane's drain, heal or schedule.

## Read

`--finale content/keep/ironwake_keep.map`, L8, 200 seeds, in `docs/measurements/keep-1395-hunt.txt`:

- Before (0353, Kitted): 149 / 101 / 0. After: **157 / 101 / 0**. The scythe now adds 4 wins to the full company over no scythe (153), where before it cost 4.
- Depleted and floor field no Keziah, so they do not move. Depleted stays 16 under its pre-storm 117. That gap is the content (Pell's lost Gust uses, 0352), which layers 2 and 3 address.
- `--full --all`: see the measurement file. The campaign maps field the cast without `Kitted`, so no Keziah carries the scythe there.

## Next

Layer 2 of #1395 (median campaign-earned ranks), then layer 3 (kit in rank), each read from 157 / 101.
