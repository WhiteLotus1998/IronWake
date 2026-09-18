# 0016 - Enemy AI: a planner over a working copy, wake state on the board, rules.json

Date: 2026-09-18. Ruled by: Code, while building issue 10. The review was posted on the issue before the code (comment of 2026-09-18 07:23). Argue on the PR if any of it reads wrong.

## The AI plans, the resolver applies
`EnemyAi.Plan(state, content)` returns the enemy phase's command list and mutates nothing. It walks the enemies in ascending id, plans each on a working copy, and applies that unit's commands to the copy through `Resolver.Apply` before planning the next, so "each on the board as the one before it left it" is literally true and the caller (the CLI in issue 11, the Sim) replays the same list against the real state through the same function. A rejected command inside the planner is a programming error and throws; the planner never emits one. The list ends with `EndPhase` unless the battle was decided during the phase, in which case `EndPhase` would be refused and the caller reads the outcome instead.

## Wake state is a fact of the board
`BattleState.AwakeGroups` is a sorted list of group names, printed by `Canonical()` on its own line and restored by Recall with everything else. `EffectiveBehavior(unit)` reads a sleeping Guard as Hold and a woken one as Aggressive; the unit's own `Behavior` never changes, so the map's authoring is still visible in the state. The check runs in the resolver after every accepted command on either phase, not only after player commands as section 8 first wrote it, because a Guard member that attacks in Hold mode on the enemy phase and dies to the counter must wake its group by the death rule, and a check that runs only on player commands would miss it. On the enemy phase only noise and a death can fire, since nobody the rule watches moves. Section 8 says so now.

## The radius lives in rules.json
Section 8 said the wake radius lives in content and no file held it. `content/rules.json` now does, as `wakeRadius: 4`, read into `GameContent.WakeRadius` with `NoiseRadius` derived as radius plus 2; the loader refuses a negative radius naming the file and the field, and the serializer writes the file back so the round trip still holds. Rule constants that belong to no map go here from now on.

## Scoring, and the three amendments taken from Chat
The score is a double throughout. Expected damage is `Damage * (1 + 2 * CritChance / 100.0) * strikes`, capped at the HP that side could remove (Chat's cap, taken). The kill flag reads deterministic damage times strikes against remaining HP and never the expectation. Both hazards Chat named are tests: a 15 percent crit target scores exactly 1.3 times the damage term of an otherwise identical 0 percent one, and an attack lethal only on a crit scores under the kill bonus and loses to a guaranteed larger chunk. The score is taken per (target, tile) rather than per target, because the mover's terrain changes the counter it takes; section 8's tile rule then applies among the tiles tied at the best score.

## Not decided here
Whether a group woken mid enemy phase should act in the same phase is a lean, not an agreement: it falls out of the planner's working copy and it is the "woke up at the wrong time" story section 13 asks for, but it makes a debug prediction harder. Argue it on the PR or on 11's debug pair; the alternative (a group woken on the enemy phase acts from the next turn) is one flag in `PlanUnit`.
