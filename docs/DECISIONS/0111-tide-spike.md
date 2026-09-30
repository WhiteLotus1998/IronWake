# 0111 — The tide (13.21): a spike in content, provisional

Date: 2026-09-30. Design Table #592, round 176. Code's lean, recorded as provisional; Chat argues it on the Table or the PR.

## Why

Every experiment since the pincer has been a number in the hit slot or a player action, and 0099 and round 155 rule out protection as an action: defence comes from tiles. Wildfire changes terrain off a strike; nothing yet changes it on a clock the player can read in advance. Chat's cold play of Cinder Copse (round 175) named the channels as that sample's best idea: an enemy that arrives in pieces at fords. The tide makes the ford itself the clock.

## The build

No new rule. Map events (DESIGN 10, 0036) already change terrain on a turn, and a change that would leave its occupant on ground it cannot enter does not happen and is spent. One console change: an announced terrain change to a terrain some movement type cannot enter now says so, `4,5 becomes water, unless one who cannot enter water stands on it.`, because the held tile is a rule the map depends on (rules go on screen). A change to ground every type can enter prints as before.

Sample `docs/samples/ebb_ford_tide.map`: rout, limit 10, one river across row 5 with a four-tile road ford (4,5 to 7,5). The ford floods at the start of enemy phase 3, drains at the start of player phase 6, floods again at enemy phase 8, all announced. An aggressive van (soldier, brigand, rider) north; a guard rear (archer, soldier, hexer) behind it. A flyer crosses water and so never holds a ford tile.

## Cost

No action is added. Holding a ford tile costs the unit its end tile: a road at avoid 0 in the river's middle, reachable from both banks.

## Kill criterion (agreed before Chat's play)

Kept, as an authoring tool for a map, if in either chair's play a unit ends on a ford tile, or refuses one, or crosses or refuses to cross, because of the printed schedule, with the journal saying so. If neither play shows that, the tide is prose: a river that floods is scenery, and no map is drawn for it.

## The first play

Code, warm, seed 653: won the rout on turn 6, Dunstan dead, all three Recalls spent, 6/6/5 (PLAYTEST.md). On turn 3 the party crossed to fight on the north bank before the flood, because waiting would have left the rear across closed water until turn 6 of 10. Wren ended on 5,5 (she went there to dress Dunstan's wounds, and a Field Dressing heals only its user, so she waited there), and `event flood2 is blocked: its tile is held` left one lane open, which Pell crossed on turn 4 while the rest of the ford was water. The hold was an accident this time; the lane was used on purpose. Dunstan, sent north to take the soldier, was cut off and killed by the hexer. The schedule changed turn 3 and turn 4; the drain on turn 6 came after the fight was decided.
