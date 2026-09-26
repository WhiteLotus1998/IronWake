# 0066 — Grudges, second pass: the sworn strike costs

Date: 2026-09-26. Issue 331, from the fifty-fifth to fifty-seventh rounds on the Design Table (#322). This record restates what both partners agreed there. It is provisional, like 0065, until the replays below decide the arm.

## Decision

Both first plays of 0065 (Code seed 65, Chat seed 23 cold) met the free-lure condition: the grudge's decisive act pulled an enemy off a killable captain onto the unit parked on the fort to tank. Four changes:

1. **A sworn strike costs.** A player unit an enemy is sworn against fights that enemy at -20 crit avoid (`Grudges.SwornCritAvoid`), on every strike between the two, the enemy's counters included, on either phase. It is in rivalry's crit avoid slot (`Combatant.CritAvoidModifier`), so one resolved crit prints. The resolver, `Queries.Forecast`, `threat`, the planner's score, the exposure plan and the heuristic player all read it from `BattleUnit.ToCombatant`/`Answering`, which now take the opponent. The console prints `sworn: <enemy> on <unit>: <unit> crit avoid -20` under any forecast between them. The avoid clause was withdrawn and is not stacked (fifty-seventh round).
2. **The keepsake outranks the grudge.** A sworn enemy's precedence is: a keepsake strike on the sworn unit, then any keepsake strike, then any strike on the sworn unit, then the best strike by score. For a unit already carrying a keepsake this is what it did before, since its arms were keepsakes only. The change is the unit that would take a stack by moving onto it: it now takes the keepsake strike over the grudge. An unsworn unit's choice is unchanged. `StrikeOn` agrees.
3. **A grudge strike logs its alternative.** `EnemyAi.GrudgeChoice` names the sworn unit, the strike's score, and the best strike the unit had on anyone else. The console prints it under the enemy's forecast, and the Sim's `--trace` prints it as a comment. `--trace` also takes a map file's path now, so the sample traces.
4. **At dusk only witnesses swear.** A group-mate swears only if it knows of the killer (`Dusk.Knows`) on the board the dead unit has left. The dead unit's own eyes do not count, or every mate would always witness. 0065's strike-time rule, that a sworn unit counts only when the enemy knows of it, stays.

The -20 is a constant in Core, not a `rules.json` number. It is an experiment's number and moves to content if the arm is kept.

## Kill criterion

13.4 is killed with its header, events and planner line if, in both replays (seed 65 and seed 23), the crit never changes a decision: no kill reassigned away from the tank, no grudge-holder killed out of the planned order, the tank never leaves the fort because of it. It is kept for the full 13.4 build if it changes one in either. A crit that lands and kills the tank counts only if the player saw the number and took the bet; the journal says which.
