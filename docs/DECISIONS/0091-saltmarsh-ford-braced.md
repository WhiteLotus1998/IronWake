# 0091 — Saltmarsh Ford ships with `brace: on`

Date: 2026-09-29. Issue 131, the lever round 121 on the Design Table (#470) put first: `brace: on` on the shipped map, with the ford group's timing arm (0090) under it. 13.14 is kept (0084); this is its first shipped map. Builds on 0029 (the Toll Axe), 0030 (the ford forest) and 0090 (the spawn at the party's back), which all stand.

## Decision

1. `content/maps/saltmarsh_ford.map` gains `brace: on` and nothing else. The file as of 0090 is kept as `docs/samples/saltmarsh_ford_0090.map`, so Code's seed 511 replays where it was played; a test holds that the shipped map is that file with only the header added.
2. The brace and rivalry samples, campaign 139 and Chat's seed 503 stay on `saltmarsh_ford_0030.map`, as 0090 left them. The shipped map and `saltmarsh_ford_brace.map` now differ only in the ford group (placed on the 0030 file, spawned on this one).
3. Saltmarsh is the one shipped map with the header; a test holds every other shipped map to it being off.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-29-saltmarsh-brace-131.txt`)

| File | Gate 1 | Losses | Winning median, p90 | Gate 4 median |
|---|---|---|---|---|
| 0090 (timing arm, no header) | 23/200 (12 percent) | 160 timeout, 17 captain | 11, 16 | -0.040, FAILED |
| Shipped (0090 plus `brace: on`) | 34/200 (17 percent) | 158 timeout, 8 captain | 13, 17 | -0.025, FAILED |

These reproduce 0090's last row to the digit. On the loss breakdown: the header gives the heuristic eleven more wins and halves its captain deaths (17 to 8), and the timeouts stay the stall before the healing boss (refused kill p50 0.8664). The heuristic never baits, so the Sim reads the party's own braces, not the leader's; the boss half is read in play. Gate 1 and gate 4 fail as they did before this lever; neither was its target (#131 reads the loss shape, and the map is not `tuned`).

## Code's play (seed 523, warm, disclosed)

Won on turn 10 of 18, nobody dead, one Recall. The pair arrived on turn 2, reached the party on enemy phase 4 and struck Ottilie from behind for 10; the leader was braced every phase until a deliberate adjacent bait on turn 8. The bridge at 10,3 is the only tile south of the river at distance 3 from him, so a party staged on the south bank cannot reach his side on the bait's turn after; the Recall restaged turn 8 on the north bank. PLAYTEST.md has the entry; the transcript is `docs/transcripts/2026-09-29-saltmarsh_ford-523.txt`.

## Open

- Both partners re-rate the ford on this file; that is #131's acceptance. The west route still never fires the arm, and turn 1 is still a march (0090).
