# 0328: Still Water's freeze, on or beside water (engine and fixture)

Date: 2026-10-08. Issue #1330, from Lotus's round-3 spell rulings (Table #1287, relayed in round 443). Builds on 0322.

## Decided

- **A new tome rider, `freeze`,** borrows ice's chill rider as an ember borrows fire's burn. The loader refuses it as a school's own rider. A learned caster's gate binds it.
- **Where it fires.** A hit from it on a unit that survives freezes it if the unit stands on a Water tile or orthogonally beside one. A diagonal never counts. Only Water counts, read from the board as it stands now, so rime ice is not water. Only a flier stands on Water, so "on" reaches fliers and "beside" reaches everyone.
- **What it does.** The unit has Mov 0 through its side's next phase, on the chill's clock (`BattleUnit.Frozen`). A boss keeps Mov 1. The unit still strikes, counters and uses items. A second freeze refreshes the clock and never stacks. A miss or a kill freezes nothing. A counter with the tome freezes as a strike does.
- **Off water the hit is only a hit.** The strike is not refused, and the forecast says ` no water: no freeze`. Lean: a Still Water cast at a dry target is a weak hit the player chose with the line in front of them, and refusing it would need a new rejection for no gain.
- **Light's cleanse clears it** (Chat's lean on the issue).
- **The word is freeze, never root, in every line** (Lotus). Forecast: ` freezes`, ` freezes: boss Mov 1`. Card: `frozen: cannot move next phase` or `this phase`. Event: `unitFrozen`. Protocol: `frozen` on a unit.
- `threat`, both planners and the Sim read the Mov through `ReachOf`, as they read a lock and the drake's hold. Canto, the escape count and Kinsbane's reach read it too. Fixture only (`test_still_water`), so no transcript moves.

## Unsure

- **0323 and this freeze use the same word for different things.** 0323 makes the whiteout's freeze the stun: the unit skips its phase, does not counter, a hit ends it, and bosses are spared. This issue's freeze is Mov 0 with act and counter, bosses Mov 1, and no hit-break. 0323 also says the hit-break "binds anything else that freezes." This record builds the issue as filed and does not add the hit-break. Before either reaches content, the Table should settle one status or two names.
- Still Water's first use, a Water tile frozen walkable until the caster's next phase ends, is unbuilt. The drake's rime (issue 805) is the engine it would reuse.
