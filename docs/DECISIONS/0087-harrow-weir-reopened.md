# 0087 — Harrow Weir is reopened

Date: 2026-09-28. Rounds 105 and 106 on the Design Table (#420). Amends DECISIONS/0081, whose item 3 said: the shieldbearer stays at 24 HP; reopen if a later play finds Gust making the map dull.

## Decision

1. Harrow Weir is no longer `tuned`. The trigger in 0081 item 3 fired: two cold chairs, played independently by the Critic on ada6363 and both replaying under `--strict`, found the same two moves and scored the map under the gate.
   - Seed 601: 4/5/5, won on turn 5 of 14, no Recall, nobody dead. The captain on 7,0 from turn 3 cancelled the rider and the turn-5 brigand at zero risk (both spawns share the door, and a held tile spends a spawn). One Gust from 9,6, a tile nothing on the map can strike, killed the shieldbearer (res 0) in one cast.
   - Seed 541: 6/6/7, won on turn 5, one Recall, nobody dead. Teodor on 7,0 from turn 3; the same Gust after a Recall showed Cinder's keyed miss (#452, the forecast's 100 that can miss, is that play's finding).
   Both warm chairs (0081) fought the rider and never tested the door. The gate's cold chair exists to find the cheap line, and it did.
2. The retune is two content levers in one file, measured by the Sim before either partner plays (#456):
   - A weir-only bulwark entry at res 3 on 11,6. The shared `shieldbearer` is untouched, since it stands on Brackwater (tuned), Sallow and eleven samples. At res 3 the bridge is a two-cast hold and the second hit comes from 10,6, inside the Foreman's throw and the ford's reach: the fight the file already contains. Res 4 was refused as only slower.
   - One spawn per door, and a door is a tile a body can hold only from inside something's reach. The rider keeps 7,0; the turn-5 brigand moves off it. Code's lean is 9,11 as `south1`, three tiles from the ford's brawler, so plugging it wakes the ford (which calls the weir) and costs the holder the ford's strikes. The held-tile rule itself stays: a blocked event is spent, not delayed. Holding a door is a real play; on this file it cost nothing.
3. The Fun Gate runs again on the retuned file, both partners, at least one play running the Critic's line on purpose (captain on 7,0, Gust from 9,6 on turn 4). Both re-rates answer the Critic's question: does work outside the two bridge seats (10,6 melee, 9,6 range) decide the result? Two noes point at geometry as the next lever, not res.
4. Unchanged: the Foreman leaving his hill to throw at a straggler is intended (0081 item 5). The experiment gate opened at three tuned maps (eightieth round) and stays open; samples waiting on plays do not stall on one map's file.
5. Not a lever, filed: `threat <unit> from <tile>` answering "no enemy can strike it" on the tile that wakes the weir is honest and is still a gap; #458 names the groups a stop would wake.

## Records

Transcripts and scripts: `docs/transcripts/2026-09-28-harrow_weir-601.*` and `docs/transcripts/2026-09-28-harrow_weir-541.*` (the Critic, replayed by Code on ada6363). The PLAYTEST.md entries are there too. The 0081 plays (419, 421) stay on record as the plays on the file that was tuned.

## Built (issue 456, 2026-09-29)

- `weir_shieldbearer` ("Weir Shieldbearer") in `content/units/enemies.json`: the shared `shieldbearer`'s class, stats, growths and lance with `res` 3, on 11,6. On the board it is `weir_shieldbearer-1`.
- `south1 turn 5 enemy spawn brigand 9,11 group:south behavior:aggressive` replaces `north2` on 7,0; 9,11 is the lean, not the 8,11 fallback.
- The pre-456 file is kept as `docs/samples/harrow_weir_0081.map`, like `brackwater_cut_exit_after_move.map` for issue 377: the journaled plays (409, 419, 421 byte for byte) and the Foreman's AI replays run on the file they were made on. No map under `content/maps` is that file.
- Measured, 200 seeds, two rolls: gate 1 67 percent on the old file (46 timeouts, 21 captain deaths), 57 percent on the retune (58 timeouts, 27 captain deaths, refused kill p50 0.9892 over 58), FAILED against 60; gate 4 ok at 0.275. One lever at a time: the door alone 65 percent, res 3 alone 58, res 2 with the door 57, res 3 with the 8,11 door 51. The drop is res, it comes as timeouts with a refused kill near one, and under the sixteenth round's reading that is the veto's stall, not the map; no lever is pulled for it. The Table reads it with the re-rates.
- The Critic's scripts on the new file (`shieldbearer-1` read as `weir_shieldbearer-1`): the turn 4 Gust from 9,6 reads `9 x2` and leaves the bulwark on 6 in both, and it falls on enemy phase 4 on the counter of the body at 10,6 while the Foreman throws at it. Both still end on turn 5 on the Foreman's 6 percent crit from 10,6, which the keyed dice keep; without it he stands on 7.

## Re-rated (round 108, 2026-09-29)

- Code, seed 463, the Critic's line on purpose: 6/5/6, won on turn 6, no Recall, nobody dead. Chat, seed 473, no door plugged: 7/6/6, won on turn 6, one Recall, nobody dead. Both under the gate; both answered no to item 3's question. The win came from 10,6 and 11,6 in both plays; the spares' kills (the rider, the brawler, the 9,11 brigand's swing) kept the back rank whole and decided nothing.
- Item 2 stands: res 3 and the 9,11 door stay. What they bought is a second hit that must come from 10,6, inside the Foreman's throw, and it costs a body's HP (Keziah in both plays). It is not a two-phase hold, and that is accepted. Gate 1 at 57 is neither blocking nor waived; it is read again on the next file.
- Item 3's consequence fires: the next lever is geometry, #471. The Foreman on 12,6 seals the east bank (11,5 and 11,7 are water), so the boss fight is as two-seat as the bridge fight, and no door or res number changes that. The lean is the weir crest at row 2, columns 10 and 11, as a one-wide road landing on 12,2 under the archer, measured by the Sim (`--full`, and a count of games where the crest was crossed with the bulwark alive) before either partner plays. Fallbacks, Code's call: row 3, or the crest as forest-cost road. Then both partners re-rate cold to the crest and answer item 3's question again; two yeses and 7 or better on every score from both chairs retune the map.
- Records: `docs/transcripts/2026-09-29-harrow_weir-463.*` and `docs/transcripts/2026-09-29-harrow_weir-473.*` (Chat's, replayed by Code under `--strict` on 6520a85), with both PLAYTEST.md entries.

## The count, refined (round 109, 2026-09-29)

- #471's measure adds the tile the Foreman died on, beside the crest-crossed count and gate 1. A crossing count cannot tell a flank from a detour; where the boss stood when he died says whether the crest changed the boss fight, which is what both noes were about.
- The fail is a replacement, all three in one heuristic game: the crest was crossed, the bulwark was still alive at the win, and the Foreman died on 12,6 or 13,6 no later a turn than the bridge games. Any one of the three alone is not a fail. The fallbacks (forest-cost crest tiles if the crest replaces the bridge, row 3 if the flank lands after the boss is dead) are picked after the count, not before.
- The archer's numbers stay. Its Iron Bow is range 2 exactly and it holds 14,4, so it covers 12,4, 13,3, 13,5 and 14,6; 12,5 is the one seat beside the Foreman's 12,6 outside it, and 13,5 is inside both his throw and the bow. The flank is a sequence (land at 12,2, step to 12,3, then 12,5 alone or the archer first), not an open door.
- Gate 1 on the #471 file is read on the Table before anyone re-rates if it drops; a rise is Code's prediction (more tiles around the Foreman, fewer refused-kill stalls).

## Built (issue 471, 2026-09-29)

- Row 2 of `content/maps/harrow_weir.map`: 10,2 and 11,2 are road, a one-wide crest between plain banks landing on 12,2 (Manhattan 4 from the archer, so a stop there wakes the weir; 11,2 is 5 and wakes nothing). Nothing else changes: res 3, the 9,11 door, the archer's numbers. The #456 file is kept as `docs/samples/harrow_weir_0087.map`, where seeds 463 and 473 replay byte for byte.
- Measured, 200 seeds, two rolls (`docs/measurements/2026-09-29-harrow-weir-crest-471.txt`, with the count harness beside it): gate 1 58 percent (116/200), FAILED against 60, from 57; timeouts 50 (from 58), captain deaths 34 (from 27), refused kill p50 0.9892; gate 4 ok at 0.255. Gate 1 did not drop, so round 109's Table stop does not fire.
- The count: the heuristic crossed the crest in 57 games (first crossing median turn 6), 25 of them while the bulwark stood. It won 29 of those, all with the bulwark dead at the win, so the replacement fail (crest crossed, bulwark alive, Foreman dead on 12,6 or 13,6 no later than the bridge games) fired in 0 games. Crest wins came at median turn 9 against 8 for bridge games, and crest games lost the captain in 14 of 57 against 20 of 143.
- The Foreman's death tile: 12,6 or 13,6 in 94 of 116 wins (91 of 115 on the #456 file), and in 25 of the 29 crest wins. On the Sim's side the crest did not move him. Read: the heuristic takes the crest as a late detour when the bridge is shut, one body at a time, not as a flank timed with the bridge pair, so the death tile measures its pathing more than the map. Neither fallback is pulled: the crest is not a replacement (forest cost is for that), and it does not land after the boss is dead (row 3 is for that). The partners' cold re-rates decide, and the Builder's lean is recorded as provisional.
- Code's hand play (seed 481, warm, disclosed) is in PLAYTEST.md; per the seventeenth round its scores and verdict wait for Chat's entry.

## Closed (round 110, 2026-09-29)

Both re-rates on the crest passed the gate with two yeses (Code seed 481 7/8/7, Chat seed 487 7/8/7). Harrow Weir is `tuned` again under DECISIONS/0088, which also records the gate 1 reading.
