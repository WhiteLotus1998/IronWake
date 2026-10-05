# 0249 — The field, Rook's pick: the turn-3 south wake is measured and not shipped

Date: 2026-10-05. #1044 step 2; rounds 368 and 369 (#1033). Measurement: `docs/measurements/field-rook-seen-coming-1044.txt`. Spike: branch `experiment/seen-coming-1044`.

## Context

Rook's arm of the field owes gate 1: 86/200 on 0248's baseline, 120 needed. 0240 filed the fallback lever: an announced wake of the south group on turn 3, keyed to Rook's pick. Round 368 said to measure it both ways, since the Sim goes south in nearly every game. Round 369 said to report wins, losses and timeouts per row, and if the arm missed 120, to trace three timeout seeds before anyone proposed a second lever.

## Decision

1. **The lever is not shipped.** Spiked as a second part of the `seen_far:` header (`seen_far: rook 2; south turn 3`: with the unit on the board, the named group wakes as that turn's enemy phase begins, printed until then, never fixing a `route_drift:` route). At 200 seeds: 89 wins, 64 losses, 47 timeouts, against the baseline's 86/60/54. It moved timeouts about evenly into wins and losses, all inside noise. It misses 120, so it is not a gate-1 lever (round 368), and it lowers nothing, so it is not a recorded tension lever either. The field's file is unchanged, and the engine change stays on its branch.
2. **Round 369's readings stand (agreed by both):** the three seats, Rook with the header (86), Rook without it (99) and the empty slot (91), are one cluster within noise. The header's measurable Sim cost is timeouts (54 against 38), not losses, and it is never levered. Rook's low use in the Sim is #1054's key parking a fragile flier, not a finding about her kit; `cast.json` stays shut without a hand play.
3. **The traced timeouts are losses on the clock.** On seeds 4, 7, 9, 11 and 14, the party is gone by turn 9 (Maud lives on seed 4). The captain then waits ten-odd turns alone against a boss whose round can take him from 23 to 1, and the veto refuses every swing at him. The limit and the boss's approach are not the cause. Rook falls on turns 2 to 4 in all five, to her own unvetoed strikes (corrected by #1061: seed 4's turn-3 stop beside the rider was priced awake and not lethal; she fell on turn 4 to her own `attack ... !`).
4. **No second lever is proposed in this record.** Where the arm loses is turns 2 to 9 against the pickets and the south group, and it loses four recruits, not only Rook. Whether the next step is content, a Sim change, or standing the arm on its hand plays goes to the Table (round 370). #1044 waits on that answer.
