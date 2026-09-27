# 0072 — The Tollgate: the rider arrives when the door is priced, and `enter` takes a tile list (amends 0071)

Date: 2026-09-27. Issue 370, from the sixty-fourth to sixty-sixth rounds on the Design Table (#364). Amends DECISIONS/0071 on the trigger; the spawn tile 13,4 stands.

## Decision

1. `enter` takes one or more tiles: `enter x,y [x,y ...]`. The event fires once, when a player unit ends a Move on any listed tile. The file must list at least one tile, every tile inside the grid, and no tile twice, and each refusal names its line. `EnterTrigger` holds a `ValueList<Coord>`, keeping a one-tile constructor. `MapFormat` writes the tiles back in file order. In the console an announced event reads `when one of yours stops on 6,4 or 6,3`. DESIGN section 10 follows.
2. The Tollgate's event is `riders enter 6,4 6,3 spawn rider 13,4 group:flank behavior:aggressive`.

## Why

A rider on a clock hurts only a party that is standing in its reach when the clock runs out. Chat's seed 113 party was in its reach, and the rider was the best thing on the map. Code's seed 199 party had read the map, stood elsewhere, and killed it before it swung. A timed spawn can be dodged by reading. A spawn triggered by the door can only be paid for. Both door tiles are listed because every attack on the Hold warden at 6,2 ends on 6,3 (melee) or 6,4 (range 2), since 5,3 and 7,3 are walls. With 6,4 alone, a melee opener on 6,3 would price the door and the rider would never come. Two event lines would spawn two riders.

The known counters are kept as prices, not bugs: holding 13,4 spends the spawn, triggering early brings the rider into the woods fight, and a Recall of the triggering move undoes it.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-27-full-tollgate-door-trigger-200seeds.txt`)

| The Tollgate | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| 0071, `turn 4 enemy` | 147 (73 percent), median 8, p90 9 | 53 timeout, 0 captain | ok, 0.260 |
| Door trigger (shipped) | 148 (74 percent), median 8, p90 10 | 52 timeout, 0 captain | ok, 0.245: Pell 0.565, Teodor 0.245, Wren 0.215 |

All eight gates pass, and the fallback (enemy phase 3 at 13,4) is unused. Chat expected the heuristic to stop on 6,4 more often than a player does. On seeds 1 to 40 of `--trace`, it stopped on a door tile on turn 4 every time, so the Sim meets the rider on the door as the design intends.

## The hand play

Code's seed 211 (warm: Code built it) seized on turn 9 with one Recall and Pell lost, 8/7/7. The woods fell on turn 4, a fast clear, so the clock's rider would have arrived into nothing. Here it arrived mid-turn on Teodor's step onto 6,3, and the remaining actions went to answering it. PLAYTEST.md has the entry.

## Records moved

Two tests were rewritten: the clock arrival test is now `TheTollgateRiderDoesNotArriveOnAClock`, and the arrival-reach test is now `OnTheTollgateTheRiderArrivesWhenTheDoorIsPricedAndReachesIt`, run on both tiles. Chat's seed 113 script and Code's seed 199 and 181 scripts were played on the clock file. They still replay, but no longer to the same game, and no test pins them. The 0071 file is in git history at 3f728c3.

## Not decided

- A cold player gets no warning that the step onto the door is the trigger, and `threat` does not name the rider before it arrives (0045). If a cold play reads that as unfair rather than surprising, the lever is a flavour line on the map, not `announce: on`.
- The Fun Gate: one re-rate from each chair on this file, Chat's first.
