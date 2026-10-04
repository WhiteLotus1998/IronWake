# 0229 — Escape maps print the count

Date: 2026-10-04. Issue #928, round 313 (Chat's cold Long Count).

## Context

Since issue 377 a unit exits only from an exit it began its turn on. So the real deadline on an Escape map is standing on an exit at the end of turn `limit - 1`, and every fight costs a turn of walking. Nothing on screen said so. Two chairs on The Long Count (Code 91 warm, Chat 91 cold) found out on turn 6 that the map was already lost, and both spent a Recall to undo it.

## Decision

The fix is visibility, not a longer limit (round 313). `EscapeCount` in Core:

- **N** is the fewest player phases of walking to stand on a free exit over the current board. It uses the section 4 step costs, the unit's movement type and Mov now, and every enemy tile blocks: dusk draws an unseen enemy on its tile, so where it stands is already on screen. An ally's tile is crossed, but no phase ends on it.
- A unit that can still move this phase leaves on `turn + N`. Otherwise it leaves on `turn + 1 + N`. Under `exit_after_move` it leaves one turn sooner. Its **last start** is `limit - N`, or one later under `exit_after_move`. A unit on an exit leaves this turn if it may exit now, else the next.
- **The board** prints `count: Ottilie 2 turns to an exit (last start: turn 6), Teodor on an exit (leaves by turn 8)`, with `cannot leave by turn N` or `no way to an exit`, under the exits legend while the battle runs.
- **`end`** prints `Count: after this phase <unit> cannot reach an exit by turn N` beside the lethal lines, for each unit that could leave by walking this phase and cannot from the next. An ally's line adds that the captain's exit leaves them behind.
- **The captain's `exit`** prints `Exit: leaves Teodor behind` before it resolves, and in a campaign `(left behind counts as fallen)`.
- **The protocol** has a `count` query, and `end` carries `countPassed`.

The issue's example (2 turns, last start 7, limit 8) was one turn late under the rule. The build prints the rule's number.

## Measured

No board changed, so the Sim is unchanged. 13 journaled escape transcripts gain the count line and nothing else. Chat's 91 cold script replays on main now that the save records Rook's quest 1 as won. Code's warm play with the count (91, Teodor) changed the opening: on turn 1 the count read 5 turns and last start 3, so two turns of fighting were spendable, and they were spent on the bridge.

## Next

If a cold chair with the count on screen still reads The Long Count as unwinnable, the limit is a tuning question for the Table. The Long Count's held archer (11,3, never acted on two chairs) is a separate lever.
