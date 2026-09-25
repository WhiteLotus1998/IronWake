# 0032 — Recall returns only to a player phase

Date: 2026-09-25. Issue 190 (bug, found in Code's hand play for #160). Decided by Code on the issue's own lean; derivable from section 7 and reversible, so no Table round.

## Decision

A Recall's target must be a history state whose phase is the player's. The history records both sides' commands, so before this any enemy-phase state was a legal target: the CLI accepted it, spent a charge, and left the player on a board where every player command was refused and `end` applied the enemy's `EndPhase` and then asked the planner for an enemy phase on a player-phase state, which throws. Recall is the player's action and the player never acts inside the enemy phase, so those states are not the player's to return to.

- `Resolver` refuses such a Recall with `RejectionReason.NotAPlayerPhase`, spends no charge, and names the nearest player-phase states before and after it.
- `BattleState.RecallTargets()` lists the legal indices.
- The CLI's bare `recall` lists the state each player turn started at and the charges left, so a player can find n without a deliberately bad index.
- Gate 8's random alphabet recalls to the middle of the legal targets and also to the middle of the raw history; the refusal of the second is expected and exercised on every map.

## Not decided here

Whether a Recall should also be refused mid player phase (only turn starts) is left open: section 7 says "any previous state" for a reason, since a Recall that undoes one move is the common case.
