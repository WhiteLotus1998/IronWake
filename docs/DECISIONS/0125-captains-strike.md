# 0125 — The captain's strike: Full Measure, once a map, costs his next phase

Date: 2026-10-01. Issue #636. The Builder is building what the Table agreed (rounds 186 to 188, 0121, DESIGN 14): one art per map that costs the captain his next phase, measured with Recall, with a keep round that shows the cost biting. The shapes below are Code's leans. Chat can argue them on the PR.

## The rule

- An `art` effect takes two optional fields. `perMap` is how many times a unit may declare the art in one battle, refused below 1. `costsNextPhase` makes the attack cost the unit its side's next phase.
- A capped art is recorded on `BattleUnit.ArtsDeclared` when it is declared. `Resolver.ChooseArt` refuses it once the cap is met ("is once a map and is spent"), so the menu greys the row with the same reason. The record is board state, so Recall restores the charge.
- The cost is `BattleUnit.Spent`, set to 1 by the attack. At its side's next phase start the unit begins moved and acted (`Spent` 2, `UnitRested`, printed "is spent from the strike and cannot move or act this phase"), so every command for it is refused with that reason. It cannot Wait, so it cannot brace. The phase after that, it is itself again. The planner and `threat` see it where it stands; nothing prices the rest specially.
- The console's art line under a forecast prints the uses left this map and "costs the next phase: no move, no act". A rule the player pays for is printed where it is chosen.

## The content (provisional)

- `full_measure`, Full Measure: sword, rank E, cost 2, +8 Mt, +30 hit, +20 crit, `perMap` 1, `costsNextPhase`.
- The captain knows it from the start in `cast.json` until his quest exists (#635's quest 1 shape), which is when the quest takes it over. This is a lean, reversible, and it lets the art be played now. Neither the Sim's heuristic nor the enemy planner declares an art, so no gate number moves.

## Open

- Code's warm play on the Tollgate (seed 636) took the strike on turn 3 and the cost never bit, because nothing could reach the captain on his rest phase. 18 x2 at 97 is also overkill. The keep round needs a board where the rest phase is dangerous, and the numbers may come down: +8 Mt to +6, or crit back to 0.
- The Recall measurement (a run that spends both against one that spends neither) is a hand play, since nothing in the Sim declares an art.
