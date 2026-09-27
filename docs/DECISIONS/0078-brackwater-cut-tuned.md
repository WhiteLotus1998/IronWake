# 0078 — Brackwater Cut at dusk is `tuned`

Date: 2026-09-27. Seventy-fourth and seventy-fifth rounds on the Design Table (#364). Closes the Fun Gate on the file DECISIONS/0074 (#377, exit without a Move) and 0076 (#382, the lamps) built.

## Decision

1. Brackwater Cut at dusk is `tuned`, the second map to pass. Gates 1 to 8 pass on the built file (gate 1 64 percent, gate 4 ok at 0.158 on units out; re-run on main at 9c96e99 for this record), and both Fun Gate entries on that file are 7 or better on every axis:
   - Chat, seed 271, on 354693a: 8/7/7, escape on turn 7, all three Recalls, Wren and the captain out, Pell left behind, Dunstan and Rook dead. Warm: sixth Brackwater play.
   - Code, seed 283, on 9c96e99: 7/7/8, escape on turn 6, one Recall, Wren, Rook and the captain out, Pell left behind, Dunstan dead. Warm: Code built both rules and had read Chat's script.
   Issue 385, merged between the two plays, changes only `defeat_boss` maps, so both entries are on the same Brackwater rules. Both chairs disclosed that they were warm, and warm entries count under 0073.
2. The seventy-fourth round's watch item is closed. It said that if a third lamps-era play ended with the same survivors as the first two (Wren and the captain), the toll would count as fixed rather than chosen. Seed 283 ended with Rook out as well and Pell left behind, by a route neither earlier play took, so the toll is chosen.

## Why

Seed 283 found a second route through the map, and the lamps made it: a noise wake. Pell's kill on the gap, from 13,3, was a combat 6 tiles from the shieldbearer, so the bank woke on the player phase and its lamps were lit. At dusk the enemy knows only what its side can see or hear. The pocket party was out of both, while the chase could see Pell and Dunstan at the gap. So the bank marched west on the gap and left every exit empty for the party in the north pocket. The price was the gap's defenders: the brawler came out of the dark and doubled Pell. One Recall moved Pell to the one tile it could not reach, and Dunstan held the gap to the end. A map whose guards can be drawn off the door by a fight elsewhere, and whose price for that is paid by whoever made the noise, is the map we wanted.

## Records

Transcripts and scripts: `docs/transcripts/2026-09-27-brackwater_cut-271.*` (Chat) and `docs/transcripts/2026-09-27-brackwater_cut-283.*` (Code), both replaying under `--strict`; the seed 283 replay is held byte for byte by `CliPlayTests.TheJournaledReRateEmptiesTheExitsOnSeed283`. The PLAYTEST.md entries are there too.
