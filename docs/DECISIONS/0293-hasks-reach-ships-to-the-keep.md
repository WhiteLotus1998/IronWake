# 0293 — Hask's reach ships to the campaign keep (amends 0286)

Date: 2026-10-06. Issue #1227; Design Table #1218, rounds 411 to 414. Amends 0286. Provisional.

## Context

0286 built Hask's reach (`behavior:aggressive` plus `holds: lord 0,6 0,6`) and backed it out of the campaign keep to `docs/samples/ironwake_keep_hask_holds.map`, because the Sim's depleted roster read 58 of 200, under #1204's floor of 60. Rounds 411 and 412 agreed that a hand pair decides instead: on one seed with `--company depleted` (#1217), the sample first, then the keep with scenery Hask, read by four outcomes. Chat played the pair on seed 2410 (round 413): the sample won on turn 11, the keep on turn 12, nobody fell on either. That is the first outcome: the reach ships.

## Decision

- **The campaign keep carries the reach.** `content/keep/ironwake_keep.map` gets exactly the two lines the sample differed by: Hask's spawn `behavior:aggressive` and `holds: lord 0,6 0,6`. So does `docs/samples/ironwake_keep_finale.map`, the measured finale sample whose events and `holds:` the campaign keep must equal (`TheCampaignKeepSeatsTheFinaleWhole`, 0285). `ironwake_keep_pair.map` is not a finale seat (no `deploy: all`, the 0148 hunt for its 695 replay) and is left alone.
- **The sample is inverted, not deleted.** `ironwake_keep_hask_holds.map` is renamed `docs/samples/ironwake_keep_hask_scenery.map` and holds the keep as it stood before this record: Hask `behavior:boss`, no `holds:`. The pair stays replayable both ways: Chat's 2410 sample script on the campaign keep (`docs/transcripts/2026-10-06-ironwake_keep-2410-shipped.*`, identical to the sample transcript but for the map's name; `Hask moves 0,6 -> 4,6`, strikes Wat for 15, then `4,6 -> 0,6`), and the 2410 keep script, the control, on the scenery sample (identical but for the name). The content test now pins the inverse difference (`TheCampaignKeepsHaskHoldsHisPostAndTheScenerySampleKeepsHimABoss`), so it never pins two identical maps. The older transcripts named `ironwake_keep_hask_holds-*` and the PLAYTEST entries that cite that path are history; the file they name is now the campaign keep.
- **The hand pair overrides the Sim's 58.** This does not drop #1204's floor of 60. Two reasons, both on the Table before the pair was played: the Sim's chairs are lower bounds (0277), and rounds 411 and 412 agreed that this pair, not the Sim, decides lever 3. Against the depleted company the reach read as a gift, not a tax (round 413): the bait saves a walk, and once hurt the veto sends him home.

## Measured (`docs/measurements/keep-1227.txt`)

`--finale content/keep/ironwake_keep.map --seeds 200 --level 8 --gates` on the shipped keep:

| Roster | Wins | Losses | Timeouts |
|---|---|---|---|
| Full | 169 (84) | 23 | 8 |
| Depleted | 117 (58) | 58 | 25 |
| Floor | 0 (data) | 119 | 81 |

Gate 2: random 0/200. Gate 4: median drop 0.150, ok. These match 0286's sample column exactly, as they should: the shipped keep is the sample. `--finale` prints FAILED on the depleted row; that is a read, not a gate here, and it is the number the next lever starts from.

The full-campaign parity script (`tests/parity/campaign/full-campaign-644.script`, variant 44, `pell_1`) was regenerated with `--campaign-script`: one line moved in the keep (Keziah's staging tile), still won, 10 maps.

## Not changed

The bait and the veto (the fresh keep chair's question). Range plus 2 stays parked. The walk home stays unpriced (round 414): if the fresh keep chair ends in a walk home and an unanswered death, the first lever is the priced walk home, the 1-2 sidearm the fallback.
