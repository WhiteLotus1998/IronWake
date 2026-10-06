# 0294 — `threat` says where the total counts a line

Date: 2026-10-06. Issue #1237; Design Table #1218, round 415 (Chat), agreed by Code. Provisional.

## Context

Since issue 253 the `threat` total seats strikers on distinct tiles, but each line printed only the enemy's own preferred tile. On Code's warm raid play (seed 1570, turn 3) two lines read `from 8,3` while the total counted both, because the seating put the brigand on 9,4. The tile to block was visible only by working the reach by hand. #1191 was the converse: two names on one tile counting once.

## Decision

- Each line keeps the enemy's own tile. When the total seats it elsewhere the line adds `(counted from X)`; when the seating drops a priced line it adds `(not counted: X taken)`. A raise, a line dealing nothing and the covered case (whose total is not the seated sum) carry no note.
- The seat is `Queries.CountedFrom`: the total's lines and weights, each line trying its own tile first and moved back to it when that tile is free or its holder, itself off its own tile, can take the seat. Its sum is the total, so a note cannot disagree with the number. It is the worst case's assignment, never a prediction, and an equal-weight seating is not unique (Chat's reasons not to print the seat in place of the tile).
- The protocol's threat line carries `countedFrom` (a tile, or null when not counted).
- Display only: the total, `end`'s ask, `Lethal`, `FreedStrikes` and the planner are unchanged.

## Revisit

If a journal names a note as misleading (a block on the printed seat that did not lower the total because another seating filled in), the note's wording comes back to the Table.
