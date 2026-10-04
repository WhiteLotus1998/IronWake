# 0230 — The Long Count: the bridge archer holds the road at 11,4

Date: 2026-10-04. Issue #930, round 313 (Chat's cold Long Count) and round 314.

## Context

On two chairs (Code 91 warm with Wren, Chat 91 cold with Teodor) the held archer on the fort at 11,3 never acted: at dusk sight 1 nothing spotted for her at range 2, and adjacency to a held bow is safe. She was scenery.

## Decision

Chat's lean: `E archer 11,4 group:bridge behavior:hold`, the road one step south of the fort. The bridge soldier on 10,4 stands beside her and beside 9,4, the bridge's east end, which her bow reaches at range 2; she stands on the road itself, so the road route goes round her while the footbridge goes past the gate hexer. Group and behavior stay. One lever.

Tests pin the tile, the spotter and the reach, and that every standable tile west of the river is at least as close to the soldier as to her, so the bridge wakes no sooner; a falsifying test shows an archer at 10,5 nearer the west bank. Both seed-91 lines wake the bridge on the same command as before.

## Measured

- `--full` at 200 seeds: gate 1 74 of 200 (37 percent; 70 with the captain alone), 125 timeouts, against 64 of 200 (32 percent, 131 timeouts) on the fort, measured in this run on the same build; STATE's "2 of 200" was stale. Still under the floor of 60; gates 2 to 8 pass.
- Code's 91 line on the new board: the archer, spotted by the soldier at dusk sight 3, shoots Wren on 9,4 on turn 2 (missed at 62) and `end` named the lethal first. Chat's 91 line dresses Ottilie on 9,4 on turn 3; there the archer, the hexer and the rider kill her.
- Code's warm play with Wren (`docs/transcripts/2026-10-04-the_long_count-91-930.txt`): won on turn 8, nobody fell, no Recall. She shot once. From turn 4 (sight 1, the soldier dead) she held the road shut and the pair walked round her.

## Next

The two old 91 transcripts stay as records of the fort board; the pinned play is the new one, and Chat's line is pinned to where it now falls. A cold chair on the new board decides whether she is a choice or a wall. If she still never shoots after turn 3, the next lever is a second spotter, not her level.
