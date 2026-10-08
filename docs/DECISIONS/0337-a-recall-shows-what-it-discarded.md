# 0337: A Recall shows the first strikes it discarded

Date: 2026-10-08. Issue #1359. Design Table #1334, rounds 460 (Code), 461 (Chat) and 462 (Code). DESIGN 7.

## Context

Rolls are keyed by (turn, phase, striker, target, strike), so a Recall changes the plan, never the dice. On Code's raid floor read (3100) a whole-turn rewind made a player re-swing Keziah at the same brigand on the same turn: the same miss, the same counter, a second charge. The rule was on screen (#1352), but after a rewind of a whole turn it was a memory test, and forgetting is not a decision worth pricing.

## Decided

- The battle keeps the first strike of each side of every combat a discarded line resolved (`BattleState.Seen`), from every Recall in the battle, never one that was not rolled. The line's own first strikes are `Struck`, restored with the board; a Recall moves the discarded part to `Seen`.
- The forecast, and each `threat` row for the coming enemy phase, print `Seen before the recall: <striker> hits|misses, <counter> hits|misses` under the combat, each half only where known. Names, not "you" and "it", since a threat row's striker is the enemy.
- **Round 462's narrowing:** a seen strike prints only while its outcome still follows at today's chance (a miss at or under the chance it missed at, a hit at or over the chance it hit at). Both schemes are monotone in the chance, so the row is never wrong.
- Only first strikes: a double's second strike or a second round waits on what came before it. Area casts and the enemy phase's own forecast lines print nothing.
- The protocol state carries `struck` and `seen` (PROTOCOL.md), written only when non-empty.

## Kill criterion

From Chat, round 461: if a chair journals that enemy phases feel solved because of the `threat` rows, cut them back to the forecast alone (one call in `ThreatText`).
