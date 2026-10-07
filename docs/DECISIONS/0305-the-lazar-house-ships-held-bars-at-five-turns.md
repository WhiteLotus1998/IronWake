# 0305: The Lazar House ships held bars at five turns (0303 kept)

Date: 2026-10-07. Issue #1266. Source: Design Table #1251, rounds 423 to 425 (Code's warm 1259, Chat's cold 4311 with Ottilie and her `turn_limit: 5` lean, Code's replay at limit 5). This restates what the Table agreed.

## Context

0303 built held bars and the waiting queue on a sample. Its keep clause asked for a journal naming a turn where a unit stayed on or stepped off a bar for a price; Code's 1259 turn 3 (Teodor on the north bar at 6 HP, the brigand printed as waiting, Salve or step off both priced) met it. Chat's 4311 never stood on a bar, so it fires no kill clause and counts as a door-line play. 1259 lost in turn 6's enemy phase; Chat argued the map is decided before turn 6 and that dropping `east3` would make holding free again (round 422's reach condition).

## Decision

- **0303 is kept.** Held bars and `arrivals: wait` ship on `content/quests/the_lazar_house.map`; the 9,1 fallback is retired.
- **`turn_limit: 5`.** `east3` (the turn-5 archer on 11,2, which reaches both holder tiles) stays.
- **The rules line** on maud_1's card says five turns, that a bar closes its lane only while one of yours stays on it, and that what it blocks waits and comes through one a turn once it is left. Chat's "The bar wants someone who can stand a swing" is advice in a voice, not a rule: it goes to #1144's list for the Fable pass.
- **Old plays.** 701, the 664 wounded Tollgate and the 647 forge play replay on copies whose Lazar House is put back to permanent bars at six turns (`Fixture.CopyContentAsIs`); Chat's 4311 replays on the sample at six turns (`Fixture.LazarHouseHeldSampleContentDirectory`). The sample stays in `docs/samples` as the map those plays were made on. 670, 1590, 4204 and 1259-held are history and not replayed.

## What the first play showed

Code's 1259 script, unchanged to the win (`2026-10-07-the_lazar_house-1259-limit5`): **won at the end of turn 5.** Teodor fell holding north on turn 3, one Recall spent, Maud 2/17 on the fort after the turn-5 brigand's 36 percent missed. `east3` walked in and never swung.

Sim, one read of the file (`--full`; the cast captain, and its chairs never bar, so it measures the door line only): gate 1 154/200, no losses, 46 timeouts; the old map on the same read 61/200.

## Next

A cold chair on the shipped map with an ally who counters at 1 (Teodor, Wren or Dunstan): the bar line needs a turn-1 bait that counters (round 425). If that play bars and the decision shows up, the pick reading as a choice is the map working; if not, look at the pick screen.
