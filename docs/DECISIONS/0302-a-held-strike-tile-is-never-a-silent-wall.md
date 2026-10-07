# 0302: A held strike tile is never a silent wall in `threat`

Date: 2026-10-07. Issue #1256 (`bug`), found on Code's Lazar House warm play, seed 1590 (round 420). Lean as filed in the issue; provisional.

## Context

`threat dunstan` on turn 3 read 8 against 23. The arriving hexer's only strike tile on Dunstan, 5,1, was held at phase start by brigand-2, which stepped off it to 4,1 to swing; the hexer then took 5,1 and hit for 11 at 99. The planner's `StrikeOn` refuses a tile another unit stands on, so the hexer had no line at all, not even a `(not counted: ...)` note. The true worst case was 19.

## Decision

- **A stepper frees its tile, one wave deep.** When `StrikeOn` refuses an enemy on the phase-start board, and a side-mate on one of its strike tiles has a line of its own from another tile, the enemy is asked again with that side-mate moved to its strike tile. The strike is a normal line, counted in the total, the seat and the lethal ask, and carries `FreedBy` (console `(once Brigand 2 steps off 5,1)`, protocol `freedBy`). Only side-mates with a direct line step; a strike freed by a freed strike is not chained, as #1191 prices a tile a counter frees.
- **A holder that stays keeps the tile, and the striker still prints.** Otherwise, a side-mate on a strike tile that stays (no line, or a line from where it stands) keeps it: the enemy is asked with the holder lifted, and a strike from that very tile prints with `(not counted: 5,1 held by Brigand 2)` (protocol `heldBy`, `countedFrom` null). It is out of the total, the seat, the lethal ask, the counter-spend row and the client's striker count.
- **The planner, the exposure sum and the Sim are unchanged.** This changes the screen and the lethal ask, nothing the AI reads.

## What it moved

Four journaled transcripts print the new lines (287, 419, 695, Sallow 1580), regenerated with `tools/rejournal.py`. Two scripts now meet a lethal ask the old screen missed and were edited by hand, `end` to `end !` at that one line, so the play is the same: Chat's 3971 cold chair (the raid, turn 3: Archer, Brigand 2 and Hexer, 24 against 24, a striker the old screen left out) and Harrow Weir 473 (turn 5: a freed Soldier on Teodor, 18 against 11). Their `.txt` records are left as played.

## Kill criterion

If a hand play journals a `once ... steps off` line that the planner never makes good on (the stepper keeps its tile and the freed striker never comes), the step is reading a seat the planner does not choose, and the line goes back to `(not counted)`.
