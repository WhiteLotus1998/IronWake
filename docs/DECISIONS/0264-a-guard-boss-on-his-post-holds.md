# 0264: A guard boss on his post with no strike in reach holds it

Date: 2026-10-06. Issue #1138 (bug). Source: Chat's cold Drake Warden chair, the Field, seed 652 (Design Table #1112, 6005059467; `docs/transcripts/2026-10-05-drake_warden-644-chat.txt`). The lean was Chat's and Code's, recorded in DIALOGUE with #1138.

## Context

Under the boss veto (0077) a guard boss goes home when the veto refuses a strike he could otherwise make (0080, issue 393). When he had no strike in reach at all, `GoesHome` returned null and he took the vetoed approach instead. That approach refuses only a tile whose exposure reaches his HP *now*. So he stepped off his post onto a tile that was safe now. On the next phase a strike was in reach from that tile, every strike was refused, and he was sent home. Each rule looked one phase ahead, and the two disagreed. On the Field, Chat watched the Sworn Captain make three round trips off his fort (turns 5 to 10) with a heal on every return, and timed the kill to the rhythm.

## Decision

- **A guard boss standing on his post with no strike in reach holds it.** `EnemyAi.GoesHome` returns the post when the boss stands on it. `End`, and with it `threat`, read the same tile.
- He still sallies to any strike the veto passes, and a refused strike still sends him home (0080).
- Off his post with no strike in reach (pulled, or walked only partway home), he approaches as before.
- A spawned boss has no post and is untouched, and so is any boss off a Defeat Boss map.

## Measured (`docs/measurements/guard-boss-post-1138.txt`)

Gate 1 at 200 seeds, before and after:

| Map | Before | After |
|---|---|---|
| the Field, Keziah's arm | 134/200, median win t11 | 138/200, median win t13 |
| the Field, Rook's arm | 86/200, median win t13 | 92/200, median win t14 |
| Harrow Weir | 137/200 | 136/200 |
| the Old Watch | 8/200 | 6/200 |
| the Warden's Gate | 6/200 | 1/200 |
| the Oath Stone (a `boss`, not a guard) | 0/200 | 0/200 |

None of the tuned maps crosses a line. Harrow Weir's gate 4 stays ok, and so does the Field's. The side maps were already far under the Sim floor and are judged by hand chairs (DESIGN 11), so their small drops lose nothing those chairs measure. The Field's wins come two turns later, because the boss stays on his fort and the party has to take it there.

## Journaled lines

Eight hand plays were made while the shuttle existed, and they no longer replay strictly past the step he now refuses. Each is history. Its test now replays the script loose (the issue 393 precedent) and asserts the hold:

- Harrow Weir 1360
- the Field 1350, 1400 and 1440
- Chat's Drake Warden chair 652: he holds 18,6 from turn 5 to turn 9 and comes off only on turn 10, to strike Teodor
- the 0081 sample's 409 and 421
- the brace sample's 307 and 439

The loose tests for 397 and 401 used to assert the old crossing, so they now assert the hold. On 397 he holds, takes a strike from 12,7 on turn 6 and falls on his hill on turn 8. The Field 1390 line is the same play with the boss on his fort, so `rejournal.py` regenerated it, and it is still won on turn 15. The full-campaign parity script (variant 18) is rewritten by the Sim, and the campaign is still won.

## Not decided here

Whether a boss that holds his fort more often makes the Field or Harrow Weir less surprising is a question for a hand chair, not this fix. The Harrow tripwire (#1087, a crest mass that freezes the Foreman) is read more often now, because a Foreman with no strike stays on his hill.
