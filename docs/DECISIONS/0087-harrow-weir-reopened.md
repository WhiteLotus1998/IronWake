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
