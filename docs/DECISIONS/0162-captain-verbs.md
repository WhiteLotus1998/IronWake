# 0162 — The captain's ladder's verbs (#705, slice 2)

Date: 2026-10-02. Issue #705 (Chat's round 216 set the three verbs; DECISIONS/0160 is slice 1). The verbs are the Table's; the effect shapes, the form masteries and their numbers are Code's. Provisional; the Sim's bar (slice 3) and play correct them.

## Decided

- **Three effect kinds, data not handlers.** `beside` (`BesideStatsEffect`): a stat delta while a living unit of the holder's side stands orthogonally beside it; max HP refused, since a neighbour must never move a unit's HP ceiling. `aura` (`AuraEffect`): every ally within `radius` (Manhattan) of the holder, never the holder, fights at `hit` more and `avoid` more; the same aura held by two units counts once, two different auras add. `footing` (`FootingEffect`): a step into a named terrain costs at most `cost`, and never opens a tile the movement type cannot enter. The loader refuses each bad field by name, an unknown terrain included.
- **Where they are read.** `Formation.Beside` and `Formation.Aura` are read in `BattleUnit.ToCombatant`, the one place a board combatant is built, and in the enemy planner's scored striker; the aura rides `Combatant.Aura` into `AbilityRules.Against`, so hit and avoid reach the forecast, `threat`, both planners and the resolver as one number. Footing is read by `Movement.Reach` and `Movement.DistancesTo`, so reach, Canto, Fall back and both planners' approach agree.
- **The masteries.** Vanguard: Shoulder to Shoulder (+1 Def, +1 Res beside an ally). Marshal: Command Presence (allies within 2 at +10 Acc). Ranger keeps Move Again. Champion: Breakthrough (+5 Acc, +15 Crit with an axe, the weapon the form adds). Commander: Field Command (allies within 2 at +10 Evade, the aura's other half, so a mounted Commander who mastered both is worth riding near). Pathfinder: Trail Sense as the class's own ability (forest and hill cost 1), Light Step (+2 Spd, +2 Lck) as its mastery. 12 points each, the general classes' number.
- **Why the aura is a mastery, not a class trait.** The issue's table puts it in the mastery column; it also keeps a fresh Marshal from being the unit hidden at the back from his first map, which the issue names as the risk.

## Open (slice 3)

- The Sim's bar: gate 1 within 5 points across the three at both tiers, and gate 4's cast verdict for the captain positive under all three.
- The twelve origin-by-class combinations through `--smoke`.
- The client does not print the aura or the beside bonus as their own forecast lines; their numbers are in the forecast's totals.
- The art for the six (each draws the Levy's).
