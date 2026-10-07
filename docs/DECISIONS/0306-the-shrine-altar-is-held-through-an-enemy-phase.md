# 0306 — The First Shrine's altar is held through an enemy phase (`seize_hold: 1`)

Date: 2026-10-07. Issue #1274; Design Table #1251, rounds 428 (Code) and 429, Chat's answer between them. Provisional.

## Context

Under 0304 the loft archer wakes when the door soldier falls. Code's warm Teodor chair (1610) won on turn 3 without the archer acting: the ally killed the door before Maud moved, and she reached the altar in the same phase. Maud is three tiles from the altar on 7,3 and her Mov is 4, so any ally can use that order, and it skips 0304's surprise every time.

## Decision

- A general header, `seize_hold: 1`, valid only with `win: seize`. On such a map the step onto the seize tile wins nothing. The map is won when a player phase begins with the captain still on the tile, so it is held through one enemy phase (`BattleState.AtPlayerPhaseStart`).
- A hold through the last turn's enemy phase still wins, so a step on the last turn counts and the limit means what it says.
- The objective line adds "and hold it through an enemy phase". `help`'s rule says when the hold is read. The step prints `Maud stands on the altar. The map is won if Maud still stands there when turn N's enemy phase ends.` The Shrine card's rules line says the same.
- Only the First Shrine uses it. The archer stays at 5,1, deaf until the door. The 6,0/8,0 placement is not built.
- The plays journaled before this replay on a fixture copy without the header (`ShrineSeizedOnTheStepContentDirectory`).

## Measured

- Sim, one read (`--full`, not a gate): Maud with Pell 53/200 (54 before), with Teodor 2/200 (3).
- Code's 1610 order replayed under the hold (`docs/transcripts/2026-10-07-the_first_shrine-1610-hold`): the step on turn 3 wins nothing. The woken archer kills Teodor in the door (2 HP), the yard comes through, a brigand hits Maud for 10, and she wins at 8/18 when turn 4 begins. Teodor falls for good.
- Chat's 4426 under the hold: the step on turn 9 holds through an empty enemy phase and wins when turn 10 begins. The margin is unchanged.
- During the held phase `threat maud` read 5 against 18 (the archer alone), because Teodor still corked the door. The yard's swings reached her only after he fell, which no `threat` line prices (one wave deep, no chains; 0281). Maud at full HP on 7,0 was never in lethal-if-all-land on this board. The hold's price here was the ally in the door.

## Kill criterion

If a cold chair's door sprint under the hold costs nothing (nobody falls and Maud takes no swing in the held phase), the hold only charges a toll, and the brigand-1 move to 4,4 (round 424) becomes the next lever. If a cold chair finds the hold unwinnable with a non-corking ally, revert to the step.
