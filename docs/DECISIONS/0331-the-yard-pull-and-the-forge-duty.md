# 0331: The teacher's blows pull; the forge duty's step; wounded at the forge

Date: 2026-10-08. Issue #1331, slice 2. Table rounds 447 (Code), 448 (Chat), 449 (Code). Follows 0329.

## Decided

- **The teacher's blows pull, on in every drill, never a player option.** `Combatant.Pulls`, set from `YardHand.Teaches`, makes `CombatResolver` hold the target at 1 HP: every strike and the drake's bite, counters and area casts included, since all of them build their combatants through `ToCombatant`. A pulled strike reports the damage it actually dealt, so a second blow on a hand at 1 HP prints `for 0 (hp 1)`. Burn and curse ticks already floor at 1. Windup and overwatch are killed experiments and never seat on a yard board. A pulled hand still swings in its own phase (Chat's edge).
- **The forecast says so.** `pulls: <teacher> stops at 1 HP on <hand>; the kill is the student's` appears when the teacher's strikes it lives for reach the hand's HP. `counter pulls: ...` appears when the teacher's counter would. It states the rule, not a prediction (0294).
- **Wounded: the yard alone is refused.** Side maps stay as shipped, because a quest that refused a wounded member would quietly close a story door. The forge is open to the wounded: a hurt unit working the bench.
- **The forge duty buys one Refine step with no gold.** It is a single command, `duty <unit> forge <slot> mt|hit`. The material is still paid, and the step is refused wherever `refine` would be (no forge, short stores, the weapon's last step), the purse aside. One duty a unit means one step a camp, so the duty needs no spent flag. A bare `duty <unit> forge` still records the duty with no step, for a unit with nothing the forge can work. Plain `refine` at its price is unchanged, so no shipped script moves.
- **The practice weapon (Chat's first item) belongs to #1332.** It is content tuned against the boards: a lone hand at equal level needs three hits to kill the student.

## Unsure

- The enemy AI and `threat` price the teacher's counter as a kill. In the yard, that overstates the risk to a hand. The planners stay as they are until a drill journal shows a hand refusing a swing it should have taken.
- From Code's pulled hand play (seed 800, Corin under the captain): the student took all three kills and finished the drill on turn 3 with 90 EXP and no level, because three kills at L1 pay 30 each. The yard pays well under its ceiling. Whether a won drill should guarantee a level, or a bonus for a clean drill, is a Table question once #1332's boards are in.

## Next

- #1332: the practice weapon, the ring and the post.
- #1331: the camp-screen row with the client's click parity, and gate 4's yard arm (the pull's ablation arm rides on it).
