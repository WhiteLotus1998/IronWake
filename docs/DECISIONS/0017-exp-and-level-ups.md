# 0017 - EXP: player units only, keyed level-ups, the cap, and the HP convention

Date: 2026-09-18. Ruled by: Code, while building issue 8. The review was posted on the issue before the code (comment of 2026-09-18 08:29). Argue on the PR if any of it reads wrong.

## The issue body's draw order is gone
Issue 8 was written before issue 31: "independent growth rolls using the injected RNG (draw order: HP, Str, ...)". Section 3 keys growth rolls on (unit, new level, stat) and nothing else, and the engine has no draw order anywhere, so the level-up test is written against a scripted keyed RNG and lists the keys it consumed. `Stats.All` order is the order the event lists gains in, nothing more.

## Only player units earn EXP
Section 6 said "a unit". Enemies are templates scaled to the map's level (DECISIONS/0005) that do not persist past it, and a brigand that levels up mid-battle would change numbers the player priced from a forecast. So `Resolver` awards EXP to the player-side participant of a combat only, whether it attacked or countered. If a later system needs enemies to grow (reinforcements arriving stronger, issue 78), it is a content dial on the template, not EXP.

## Once per combat, from the best outcome, in one function
`Experience.ForCombat(unitLevel, enemyLevel, landed, killed, boss)` is section 6 exactly: strike line if any strike landed, kill line added on a kill, 20 more on a boss. The table test carries the doc's numbers at level differences -10, 0 and +10 (strike 1, 10, 30; with a kill 6, 30, 80). `Experience.ForHeal` exists for issue 9's caller so the healer numbers of section 5 live beside the rest.

## The cap discards, and the HP gain heals
At level 30 no EXP is awarded and no event is emitted, rather than parking a unit at 99, so a capped unit's transcript says nothing rather than something that reads as progress. A level-up that reaches 30 discards the remainder. An HP gain raises current HP by the gain, so a level-up never leaves a unit further from full than it was; it is the convention every game in this family uses and the one a player expects without being told.

## Not decided here
Whether the enemy phase's rolls and the player's share one seed is unchanged (they do; the keys differ). Whether a unit that dies in the combat it would have earned EXP from should still level up: it does not, since there is no unit left to carry it, and no event is emitted.
