# 0096 — Cover, spiked

Date: 2026-09-30. DESIGN.md 13.19, issue 531, proposed by Code in round 141 on the Design Table (#503) and amended by Chat in round 142 (the issue body carries the agreed spec). Provisional in the ordinary way; nothing here touches a shipped map.

## Decision

On a map with the `cover: on` header:

- `Cover(unit, ally)` is a command: an action in place of Attack, Item or Wait, after a Move or without one, for a player unit orthogonally beside an ally (`CoverRule.Refusal`, `RejectionReason.CannotCover`). It sets the ally's `BattleUnit.CoveredBy`, emits `CoverTaken` with the tile the ally lands on and the best legal strike the coverer passes up (`Overwatch.PassedUp`, reused), and opens no Canto. It is not a Wait, so it never braces.
- The swap lives in `ApplyAttack`, after range, sight and a windup raise are read and before the combatants are built: when the target's coverer is alive and beside it, `CoverRule.Swapped` moves the coverer onto the target's tile and the target onto the coverer's, clears the cover, and the strike resolves against the coverer on that board. So a counter, a pin, a brace, rivalry and terrain all read the swapped board. `CoverFired` carries whether the strike would have killed the ally (the ally's forecast, every hit landing, no crit, over the strikes the attacker lives to make) and whether the coverer can counter. A cover ends unfired when its side's next phase begins.
- Edges as round 142 leaned: one cover per ally; no chains; a range-2 strike swaps and a melee coverer cannot counter it; a pin reads the swapped board. Watch shots and burns are not Attacks.
- The planner: `EnemyAi.Score` scores a strike on a covered ally against the coverer on its tile. Nothing else in the planner changes; the boss veto reads the unswapped board (PR Unsure).
- Readouts: `threat` prices a covered ally's lines against the coverer and says the first strike swaps them, the rest unpriced (`ThreatLine.CoveredBy`); `CoverRule.PassedLine` compares each enemy's first command's plan with and without the covers and prints `<enemy> passed <ally> (covered by <coverer>)` in the console enemy phase and the Sim's trace. It is a console line, not an event, like the grudge line.
- The enemy never covers. The Sim's heuristic covers in place of an idle Wait, the lowest-HP threatened ally beside it, never over a strike it takes; gate 1 prints per-game means of covers taken, taken with a strike passed up, fired, and passed lines.

Sample: `docs/samples/the_tollgate_cover.map`, the shipped Tollgate plus the header. Gate 1 at 200 seeds: 149/200 against 148/200 plain, timeouts 46 against 52; the heuristic's covers 2.6 a game, 1.3 fired, 0.2 passed lines, 0.1 taken with a strike passed up (a strike from its end tile the heuristic's own veto refused).

## Deciding plays

Code warm, seed 113 (won on turn 8, nobody dead, one Recall): two covers, both fired, both by a unit with no strike that turn, no `passed` line. Chat cold on seed 113 decides. The kill criterion is round 142's, in DESIGN 13.19.
