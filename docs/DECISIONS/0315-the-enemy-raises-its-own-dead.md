# 0315: The enemy raises its own dead; the company's dead stay dead

Date: 2026-10-07. Issue #1286, slice 1 (the planner's raise). Source: Lotus's enemy-caster ruling (#1251, 6033129482; DECISIONS/0307), 0314's open question. Provisional: a Table lean, argued on the PR.

## Context

0314 let a raiser stand up "the caster's foe's body" and left open whether the dark-mage mini-boss may raise a dead recruit against the company. #1286's planner work needs the answer before the enemy can cast.

## Decision

- **Only the enemy's dead rise, whichever side casts.** The player raises a fallen enemy, as 0314 built. An enemy raiser raises a fallen unit of its own side. A body that fell for the company is refused to both: `<id> fell for the company; its dead stay dead, and only the enemy's dead rise`. Permadeath means gone, for the enemy too.
- **Why not the recruit.** It would be the most dramatic raise, and it would almost never happen. The company's dead mostly fall in the enemy phase and are usually Recalled. The mini-boss's signature would then be invisible in most plays. Raising his own soldiers gives him a body to raise on most turns after the player's first kill, and it makes "kill him and they crumble" (Lotus) the map's question.
- **When the enemy raises** (`EnemyAi.Raise`): a unit that has not raised this map, holding a hollow tome it can wield with a use left, raises in place of any strike that is not a kill (`EnemyAi.Kills`, the score's own kill flag). A Hollow is a body on the board for three phases, and a strike is one exchange.
- **Which body and from where.** The body that rises with the most HP, then the newest, on an empty tile in the tome's range of a tile the unit may end on. The tile is the one fewest player units can reach, then the cheapest, then the first in reach order. A held or boss raiser raises only from where it stands.
- **No content.** No shipped class or tome raises. The fixture is an enemy Adept given dark. The mini-boss's class, its tome and its map wait on Lotus's signature on #1247.

## Not built here

The rest of #1286 is still to come: the chill, Rampart and armor arms of the planner; `threat` pricing of a Hollow that rises mid-phase (today `threat` reads no raise, so a raiser's line shows its strike or nothing); the enemy classes; the placement proposal; `drops:` on carriers.

## Kill criterion

If a journal shows a raiser that never strikes because a body is always in reach, so the mini-boss reads as harmless, the raise goes behind a kill or a strike worth less than half the Hollow's HP.
