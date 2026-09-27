# 0080 — A refused guard boss goes home, and the ford wakes the weir

Date: 2026-09-27. Seventy-eighth round on the Design Table (#364), issue 393 (Chat's lean, agreed by Code with one clarification). Amends DECISIONS/0077 and 0079, and closes 0077's open item. Provisional in the ordinary way.

## Decision

1. **A refused guard boss goes home.** When the boss veto refused a strike the boss could otherwise make this phase, a boss whose map behavior is Guard and that has a post (the tile of its map placement) ends on the reachable tile nearest the post: Manhattan distance, then its own tile, then the lower movement cost, then the reach's order. That tile is not vetoed, and 0079's swing follows from it. `EnemyAi.End` is the one end-tile function, and `PlanUnit` and `threat` (`SwingFromEnd`) both read it.
2. **Clarification (Code, round-78 reply):** "every strike refused" is not "no strike in reach". A guard boss with nothing in reach at all approaches as any woken guard does, so waking him still means something. A spawned boss has no post and keeps the approach.
3. **Wake links.** The map header `wake_links: ford>weir` (comma-separated pairs) makes the second group wake whenever the first does, in the same `WakeCheck.Run`, transitively, with the new cause `call` (`group weir wakes: called by ford`; protocol `cause: "call"`, `by`). The loader refuses an unknown group, a self-link, a target with no Guard member, and a pair listed twice. While the called group sleeps, a line under the wake legend names the link. Harrow Weir ships `wake_links: ford>weir`. A group link was chosen over moving the Foreman into group `ford`, because the link keeps the archer waking with him and reads on the opening screen.

## Measured (Harrow Weir, 200 seeds)

| | Gate 1 | Median / p90 | Timeouts | Captain deaths | Gate 4 median drop |
|---|---|---|---|---|---|
| before (#389, 0079) | 70 percent | 8 / 12 | 43 | 17 | 0.310 |
| (1) alone | 67 percent | 8 / 11 | 46 | 21 | 0.305 |
| (1) and (3) | 67 percent | 8 / 11 | 46 | 21 | 0.305 |

All eight gates pass. The gate rows with and without the link are identical. The likely reason, not traced, is that the heuristic, which knows nothing of the wake rule, reaches the weir's radius at the bridge no later than it wakes the ford. The link changes hand play (below), which is what it was for. Gate 1 stays above the floor of 60, so the limit lever is not taken.

## Replays

- **Chat's seed 401 script, (1) alone** (the map without the header): on enemy phase 7 the full-HP Foreman stays on his hill at 13,6 instead of running to 14,8, then swings at Keziah on 9 and kills Ottilie on 10 (`AFullHpForemanStaysOnHisHillOnSeed401WithoutTheLink`).
- **Chat's seed 401 script, shipped**: Dunstan wakes the ford on turn 3 and the ford calls the weir. On enemy phase 3 the Foreman crosses to 9,6 and Waits (nothing in reach), then walks back toward his post on 4 (`TheFordCallsTheForemanIntoTheSplitPartyOnSeed401`).
- **Code's seed 397 script**: called on turn 3, he crosses onto 10,6 and Waits, then walks home to 13,6 on enemy phase 4 (`TheCalledForemanStandsOnTheBridgeThenGoesHomeOnSeed397`).
- **Chat's seed 293 script**: a passing strike on Keziah from 12,6 on enemy phase 3, then home to 13,6 on 4 (`ARefusedForemanGoesHomeToHisHillOnSeed293`).
- **Chat's seed 263 and Code's seed 379 scripts**: he keeps his hill on enemy phase 3 (`TheWokenForemanHoldsHisHillOverTheTenSixPocket`). Code's ranged-only 263 script: he keeps the hill on 4 and strikes Dunstan from 12,6 on 5.
- **Code's seed 389 script**: he fights from 12,6 and the hill and never runs to the mountain at 15,0.
- **Code's seed 409 hand play** (journaled): called on turn 3 to 9,6, he hunted Teodor to 9,3 on enemy phase 4, walked home onto 10,6 when the gathered party refused him on 5, was broken to 10 there on turn 6, reached the hill on 6, and fell on turn 9. 7/7/7, warm.

The pre-393 transcripts are history and no longer replay past the phase where he first went home or was called.

## Open

- **10,6 is not always shut.** The pocket is closed as a strike tile, but walking home from the west bank, the nearest reachable tile to 13,6 can be 10,6 itself (the shieldbearer holds 11,6), and the seed 409 party broke him there, the 379 gang in a new place. Levers if the re-rates call it a pocket: veto the home tile too (hold where he stands when home is refused), or measure home by path cost so the bridge is not "near". Not built.
- **He paces.** Called while the party is split, he approaches, finds every strike refused next phase, and walks back (seed 401: 13,6 to 9,6 to 12,6 to 12,9 to 12,6). Whether that reads as a guard or as indecision is for the re-rates.
