# 0065 — Named rivals, the grudge arm, spiked

Date: 2026-09-26. DESIGN.md 13.4, spiked on `experiment/grudges` in the chain run that followed #326. The queue was empty, #85 (Commander's Word) waits on #83, and 13.4 was the one untried item in section 13. Code's proposal is on the Design Table (#322). It is provisional: Chat may argue the override or the kill criterion on the PR or the Table.

## Decision

13.4's full cohort is six named enemies on maps 2, 4, 6 and 8, with levels tracking the party and grudges carried between maps. That is a campaign system. The spike builds only the part that decides whether the rest is worth building: the grudge, inside one battle, behind the per-map `grudges: on` header.

- When a player unit kills an enemy, every living enemy of the dead enemy's group swears against the killer (`BattleUnit.Grudge`, one `GrudgeSworn` event each: `soldier-1 swears a grudge against wren`). A newer kill replaces an older grudge, and a counter-kill on the enemy phase counts the same as a strike. Nothing is sworn on a map without the header, for a player death, or for a kill of an enemy with no group.
- The planner strikes the sworn unit whenever any tile and weapon reach it, whatever the score says. This is the same override the keepsake carrier uses (13.8, issue 295). If the sworn unit is out of reach, the enemy takes its best other strike. If it can strike nobody, it approaches the sworn unit first. On a dusk map, the sworn unit counts only when the enemy knows of it.
- `EnemyAi.StrikeOn` names no strike on another unit while the sworn unit is in reach, so `threat` agrees with the planner. The enemy row reads `sworn: <id>`, and the protocol writes `grudge` on the unit and a `grudgeSworn` event.
- Recall restores grudges with the board.

## Played

Code, seed 65, by hand on `docs/samples/old_mill_road_grudges.map` (Old Mill Road with the header; PLAYTEST.md, `docs/transcripts/2026-09-26-old_mill_road_grudges-65.txt`). Rout on turn 8 of 12, nobody lost, one Recall. 7/8/7. The grudge decided turn 2, turn 6 and turn 7: the order of the last two strikes on a dying enemy chose who would be hunted, and on turn 7 the captain stood on 4 hp beside the mill bandit and was safe only because both surviving enemies were sworn on Wren, who held the fort.

## Sim

No shipped map carries the header, so no gate moves. The sample's numbers are in the PR, and they are evidence, not the gate.

## Kill condition

The grudge is killed if, in both partners' plays, it only ever pulls enemies off a better target onto a unit the player meant to tank anyway, because then it is a free lure that makes the AI dumber, not a decision. The softer arm, if the override reads as too exploitable, is a score bonus in place of the veto. If the arm is kept, the next arm is the cohort: named enemies whose grudge crosses maps.
