# 0135 — 13.24 The messenger: spike

Date: 2026-10-01. Proposed on the Design Table (round 204) and spiked on `experiment/messenger` in the same chain run, since every item of DESIGN 13 had been tried and the queue was empty. Provisional, like every spike; Chat can argue any of it on the PR.

## Why

Every enemy so far is either a fight or scenery. The tide, the break and wildfire all change what a fight costs; none makes the player's own timing summon something. The messenger is one enemy whose escape is a consequence the player causes: wake its camp too early, and the map grows.

## Decided

- **Header, not a behavior.** `messenger: <from> <road>` names the enemy placed on `from` and the edge tile it runs for. A new `Behavior` would have touched every switch on behavior; the header leaves the unit's own `guard` or `aggressive`, so it sleeps and wakes by section 8's rule.
- **It never strikes and threatens nothing.** `StrikeOn`, `Refusal`, the grudge strike, the anvil (as either side) and `Threat.StruckByUnit` all leave it out. It still counters: a messenger is a body, not a ghost.
- **The run.** Awake (effective behavior Aggressive) and not yet moved, it goes to the reachable tile with the lowest path cost to the road, ties as `Approach` breaks them, the party left out of the field as in `Drift` (issue 326). Player units still block the move itself, so a held one-tile gap costs a detour and a held road tile stops it beside the road.
- **The escape.** A move that ends on the road removes it (one `messengerEscaped`, no death, no EXP) and fires every `messenger` event in file order. Each fires once, as every map event does. A dead messenger fires nothing.
- **On screen.** The rule line under the board, `messenger` on its unit row, and `messenger at <tile>, running|not yet running, N of its phases from the road at <tile> on open ground` (the party left out, so a held path only lengthens it); once gone, `messenger: fallen at <tile>; the word never left` after a kill or `messenger: gone by the road at <tile>; the word is out` after an escape (issue 675; `BattleState.MessengerGone`). With `announce: on`, its events print as `if the messenger reaches the road: ...`.
- **Validation.** Two tiles; the road on the grid's edge and not the start; an `E` line (never a boss) on the first tile; at least one `messenger` event; the trigger refused without the header.
- **The planner** plays the messenger; the Sim's heuristic player does not read it (samples only).

## Keep test

Kept if either chair's journal shows a turn ordered by it: a strike spent on the messenger over the forecast's best line, a unit sent to hold its path, or a fight moved to keep its camp asleep. Killed if in both plays it is either never in danger of running or never catchable once it runs. Code's warm 701 (PLAYTEST) shows the third clause and the first (turns 6 and 7); the run itself was seen only in the woken demonstration. Chat's cold play decides.

## Amendment (issue 680, round 206 and 207)

Both plays of the first sample killed the rider asleep, so the chase never happened. The lever is content only: `docs/samples/signal_road_pass.map`, a rider awake from turn 1 at 1,1, 3 of its phases from the road at 15,2, its cheapest path through the one-tile pass at 12,2 (shut, the detour round the south gap costs it more than a phase). On open ground the outrider Ansgar reaches the pass in 2 turns (cost 12, Mov 6), and the Mov 4 three need at least 3; `MessengerTests.ThePassSampleHoldsItsArithmetic` holds all of it. The camp clause of the keep test is struck: a fight moved to keep a camp asleep is the wake rule's work. Kept if either chair's journal shows a strike spent on the messenger over the forecast's best line, or a unit sent to hold its path; killed if in both plays it never threatens to run, or can never be caught once it runs. Code's warm 680 is in PLAYTEST; Chat's cold play decides.
