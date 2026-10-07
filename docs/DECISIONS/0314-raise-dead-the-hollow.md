# 0314: Dark's raise dead stands a foe's body up as a Hollow for three of its side's phases

Date: 2026-10-07. Issue #1284. Source: Lotus's #1247 rulings and names (#1251; DECISIONS/0307), 0313. Provisional.

## Context

Lotus ruled the third dark spell: a fallen enemy rises on your side as a **Hollow**, "cooler than a zombie, not too strong", and the dark-mage mini-boss's Hollows crumble when he dies. Code's leans on the issue: enemies only, temporary, once a map per caster.

## Decision

- **The kind is `hollow`**, borrowed from dark's `drain` as Sunder and Armor borrow earth's `raise`; never a school's own rider. Not `raise_dead`: earth's rider is already `raise`.
- **Bodies are board state.** Every death that passes the Resolver's one death path joins `BattleState.Bodies` as it fell; Recall restores them.
- **The cast**: the Item action on a body named by the fallen id or its tile, in the tome's range, the tile empty, **the caster's foe's body only**. A use and the action, no EXP, once a map per caster (`raiseSpent`). The learned gate is not read: a primer never teaches dark (0313), so no learner can hold the tome.
- **The Hollow**: the fallen unit's class and stats, its equipped weapon alone, **half its max HP rounded up**, risen having acted. It acts in its side's **next three phases** and crumbles as the third ends, or the moment its raiser is off the board, whichever is first. No death event, no body, no keepsake, no EXP, rank or mastery, no group. It moves, strikes and waits; every other command it names is refused.
- **The enemy never casts it here.** #1286 brings the mini-boss and its AI.

## Play

Code 1284 warm on the Tollgate with a fixture tome; see PLAYTEST.

## Open

Under this rule an enemy raiser's foes are the player's fallen. Whether the mini-boss may stand a dead recruit up against the company, or raises only his own side's dead, is #1286's question for the Table.

## Kill criterion

Revisit the clock (three phases) if two journals show a Hollow either deciding a map alone or never drawing a strike.
