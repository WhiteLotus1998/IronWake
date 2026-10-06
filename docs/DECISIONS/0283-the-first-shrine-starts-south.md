# 0283 — The First Shrine starts south of the water

Date: 2026-10-06. Issue #1198; Design Table #1187, round 406 (Chat), answered by Code the same day. Builds what the Table agreed.

## Context

On the First Shrine as built (0198) Maud and her ally started north of the water at 6,5 and 8,5, three steps from both tiles that strike the sanctum door (7,2 in melee, 7,3 at range 2). A held unit braces only after it has waited through its own phase, so on the player's first phase the door soldier was a plain 20-HP soldier: four allies of six broke him on turn 1 with Maud's Radiance finishing from 7,3, and the map was won on turn 2 (Code 1520, 2/4/6; Chat 2130 cold, the map 2/4/5). The causeway and the pursuit were behind the players before the first move.

## Decision

- The start moves south of the water: Maud (the captain slot) at 4,8, the ally at 10,8. Each is eight movement from 7,2 and 7,3 for every movement type, one more than the fastest class (the Sky Captain and the Drover, Mov 7). Side maps carry no Commander's Word, so no press adds a step. An infantry Maud reaches 7,3 on turn 2 at the earliest, after the soldier has braced.
- The guard is a test, `TheFirstShrinesDoorCannotBeStruckOnTurnOne`: every class in the content, on every cast member's footing, from both slots, cannot reach a tile within the longest weapon's range of the door on its Move. If a class or a weapon ever outgrows it, the start moves further south; the brace does not change.
- Declined in round 406: the sanctum starting braced (it makes turn 1 a coin flip, still decided on turn 1) and a second sanctum guard (more HP on the same tile, which punishes the allies already short).
- The card, the turn limit, the groups and the pursuit are unchanged. The pursuit's spawn tiles (7,8, 6,8, 13,8) are now beside the start, and a unit standing on one stops that arrival, as on every map.

## The Sim

The Sim's gate-1 number on this map does not count until the planner learns the range-2 finish (#1206): it walks Maud onto 7,3 first and leaves the ally with no tile, then the veto walks her off the door. Data under the new start, 200 seeds: bare cast 0; Maud leading with Pell 24, Wren 5, Teodor 6, Ottilie 4, Dunstan 0, Brannock 0, nearly all timeouts. `--trace` takes `--lead` now, as `--full` does.

## Transcripts

The two plays journaled north of the water (Code 875, Chat 2130) replay on a fixture that puts the old start back (`Fixture.ShrineNorthStartContentDirectory`). Code 1530 is the first play from the south.
