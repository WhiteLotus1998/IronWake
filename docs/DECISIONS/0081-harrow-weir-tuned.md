# 0081 — Harrow Weir is `tuned`

Date: 2026-09-27. Seventy-ninth and eightieth rounds on the Design Table (#364). Closes the Fun Gate on the file DECISIONS/0080 (#393, the home rule and `wake_links: ford>weir`) built.

## Decision

1. Harrow Weir is `tuned`, the third map to pass. Gates 1 to 8 pass on the built file (gate 1 67 percent, gate 4 ok at 0.305, as measured for 0080), and both Fun Gate entries on that file are 7 or better on every axis:
   - Code, seed 419, on aa16fde: 8/7/7, won on turn 7, one Recall, nobody dead, the bridge route. Warm: fifth Harrow Weir play, and Code built 385, 389 and 393.
   - Chat, seed 421, on 1f409a9: 7/7/7, won on turn 9, one Recall, nobody dead, the south crossing. Warm: fifth Harrow Weir play, and Chat had read 409 and 419.
   Between the two plays only docs changed, so both entries are on the same rules and the same map. Both chairs disclosed that they were warm, and warm entries count under 0073.
2. With three maps tuned (the Tollgate, Brackwater Cut, Harrow Weir), the sixty-first round's experiment gate is open.
3. The shieldbearer stays at 24 HP. Code's seed 419 finding (Pell's Gust breaks him at full HP from 9,6) is noted, not acted on: both passing plays were made on 24, and the certainty shows on one route of three. Reopen if a later play finds Gust making the map dull.
4. The archer on 14,4 has not fought in 401, 419 or 421. Noted for any future pass on this map, not a reason to hold `tuned`.
5. A passing strike leading the Foreman off his post is intended: he hunts stragglers, `threat` prices it, and the bait pays real HP. A leash is the lever only if a cold play finds it cheap.
6. 0080's open items are settled by play. Called and then refused, he goes home and comes back out as a guard, which reads as a patrol, not a runner. On the south crossing the 9,6 visit is not a kill box (21 of 28 reachable on turn 4 of seed 421).

## Records

Transcripts and scripts: `docs/transcripts/2026-09-27-harrow_weir-419.*` (Code) and `docs/transcripts/2026-09-27-harrow_weir-421.*` (Chat), both replaying under `--strict` and held byte for byte by `CliPlayTests.TheJournaledSeed419PlayReplaysWithTheFullHpThrowAndTheWalkHome` and `CliPlayTests.TheJournaledSeed421PlayReplaysWithThePatrolAndTheBox`. The PLAYTEST.md entries are there too.
