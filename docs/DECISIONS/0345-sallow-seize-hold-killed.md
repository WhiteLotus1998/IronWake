# 0345 — Sallow's seize hold (lever b) killed on its first read; the Gate tile is the hole

Date: 2026-10-08. Issue #1383; Design Table #1368, rounds 485 (Code), 487 (Chat) and 488 (Code). Killed.

## Context

0342 left Sallow Grange's hole at the seize tile beside the Reeve's post. Round 487 agreed lever (b): `seize_hold: 1` (0306) on the `goes_home: hall` sample, so the captain must survive one of the Reeve's phases on 16,6 beside him. Measure against 174/200 first; kill if the only winning line becomes killing the Reeve.

## Measured (200 seeds, the same code)

| | Gate 1 | Winning turn p50 / p90 | Losses (timeout / captain) | Gate 4 |
|---|---|---|---|---|
| `goes_home: hall` (0342) | 174/200 | 9 / 10 | 25 (1 / 0) | 0.325, FAILED on Wren |
| plus `seize_hold: 1` | 174/200 | 10 / 11 | 25 (1 / 0) | 0.325, FAILED on Wren |

The hold adds a phase to every win and costs the heuristic none. In `docs/measurements/sallow-seize-hold-1383.txt`.

## The 3400 line under the hold

Replayed through turn 5 as played, then turn 6 by hand (`SallowGrangeHoldReplayTests`, transcript `docs/transcripts/2026-10-08-sallow_grange_hold-3400.txt`). The captain (14/23) steps onto 16,6; `threat captain` prints the Reeve at **9 percent for 6**: the Gate tile gives the holder 30 avoid, Def 3 and Res 3. On his post the Reeve strikes out as any woken unit, and the planner prefers Wren at 13,6's reach (54 percent, 10) to the 9 percent swing. The hold ends the enemy phase with the captain untouched: won turn 6, no Recall, nobody fell, the Reeve at 28/28.

## Decision

- Lever (b) is **killed**. It did not turn into (a), but it priced nothing: the seize tile is the best defensive tile on the board, and a hold on cover is free. `docs/samples/sallow_grange_hold.map` stays only as the record's evidence; Sallow ships unchanged.
- What the read names as the hole is the Gate's own terrain under the holder. Next lever is the Table's (round 488 carries Code's lean).
