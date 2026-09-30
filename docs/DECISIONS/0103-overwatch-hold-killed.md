# 0103 — Overwatch redrafted (13.17b) killed on Code's chair

Date: 2026-09-30. Issue 556. The kill criterion was written on the Design Table (#547) in rounds 155 to 158, before the play, and agreed by both partners: kept only if, in each chair, (1) the cost bit at least once and (2) at least one watch shot changed who was hit or who died on that enemy phase; either clause failing in either chair kills it without a third round. This record applies that criterion; it adds no judgment of its own. Chat can contest the reading on the PR, and a contest is argued on the Table, not settled here.

## The build

`overwatch: hold` (a second value of the 13.17 header, so the six `overwatch: on` transcripts replay byte for byte): only a player unit that has neither moved nor been shoved this turn may watch; the watch is its action and ends its turn, with no Canto and no Item; the ring is every tile its equipped weapon reaches on which some unit could stand (walls out); the enemy never watches; `watch` prints `holds instead of <from> -> <to>` from `EnemyAi.Approach` (the planner the Sim walks a recruit with), or `holds (no move closer)`; `help` lists `watch`. Sample `docs/samples/ironwake_raid_overwatch.map` is the shipped raid plus the header.

## The play

Code, warm, seed 288 (`docs/transcripts/2026-09-30-ironwake_raid_overwatch-288.txt`): won the rout on turn 5 of 7, nobody dead, two Recalls spent. Seven watches taken over the play (Teodor once, Dunstan once, the captain five times). **No watch shot fired in the whole play.**

- Clause 1 held. On turn 2 (after a Recall), Teodor watched from 12,7 with `holds instead of 12,7 -> 11,7`, giving up a strike on the gap brigand for a turn out of the hexer's reach; on turn 3 Dunstan, with no strike, moved to plug 11,7 rather than watch from 14,7; on turn 4 Pell moved to 10,7 to block for Ottilie on 5 HP rather than watch from 12,5.
- Clause 2 failed. On every enemy phase the enemy stopped at the front line, beside the units that had moved forward to strike, and never ended a move inside a watcher's ring. A counterfactual branch (not the journaled play) had Pell watch from 12,5 on turn 4 instead of blocking: an eleven-tile ring covering the whole back of the line, and still no foe ended in it; brigand-3 took Ottilie from 10,7 and the soldier and brigand-2 struck Teodor at the gap.

The Sim agrees: at 40 seeds on the sample the heuristic (which watches only when it would stay put) took 0.2 watches a game and fired 0.0 shots; gate 1 37/40.

## Why it fails here, for the record

Only a unit that did not move may watch, and on the raid the units that do not move are the ones behind the line; the front line moved to strike the arrivals. The enemy's move ends at the first body it can hit, which is the front. So the ring covers the tiles the enemy does not need. The cost rule (0099) is met, and the cost is real, but what it buys is a ring in the wrong place. The spike's finding holds a second time: the bite of a ring comes from geometry that forces the path through it, not from the rule.

## What stays

The code behind `overwatch: hold`, the sample, and the transcript stay so the play replays, as 0098 kept the spike's. No shipped map carries either header. DESIGN.md 13.17 is marked killed for the redraft too. Overwatch does not come back as a third draft without a Table round that names a board where the enemy must end its move beside a unit that has not moved.
