# Debug pass prediction: Old Mill Road, seed 7

Written by Code on 2026-09-18 before `2026-09-18-old_mill_road-7-debug.script` was run, from DESIGN.md sections 4, 5 and 8 at the head of `issue/11-playable-cli` (issue 10 merged as #94). The script is committed with this file; the run lands as `-debug.txt` in the next commit. What was run before this was written: the two-turn standing-still script in `CliPlayTests` (the section 8 named case, brigand to 3,6) and a script with the captain at 1,4 and Wren at 2,6 on turn 1, which this script deliberately does not repeat. Nothing below has been observed.

**Seed.** 7. Up to the first strike the moves are section 8 arithmetic and do not depend on it. The seed starts to matter at Wren's attack on turn 2 (player phase), and from there HP and the scorer's kill line depend on it; positions on enemy phase 3 do not, because the captain's move to 8,3 is scripted and the mill group's tiles follow from it.

## Turn 1, player phase
Captain 1,8 to 1,4 (cost 4, plain all the way), waits. Wren 2,8 to 1,6 (2,7, 2,6, 1,6, cost 3), waits. No player unit is within 4 of 9,1, 10,2 or 10,1: the mill group sleeps.

## Enemy phase 1
Archer (id first) and the boss hold: nothing in range. Brigand at 6,5, Mov 4, axe range 1, can attack nobody: Wren's attack tiles are 0,6, 1,5, 2,6, 1,7 (2,5 is water); the nearest by path cost is 2,6 at 5 (5,5, 4,5, 4,6, 3,6, 2,6), over its budget. The captain's are 1,3 (7, along the road), 0,4 and 1,5 (9, because 2,4 and 3,4 are water and 1,6 is Wren's). Target: Wren (5 against 7). Destination: the reachable tile with the lowest remaining cost to one of Wren's attack tiles is 3,6 (cost 4, remaining 1 to 2,6); 4,6 has remaining 2, nothing else is closer. **Brigand moves to 3,6 and waits.** Soldier holds. No wake: no combat happened, and no player unit is within 4 of a member.

## Turn 2, player phase
Wren 1,6 to 2,6 (cost 1), attacks the brigand at 3,6. Forecast: Wren dmg 10 (7 + 5 - 2), doubles (attack speed 4 against -3), hit 100 (98 raw against avoid -3 clamps), crit 4 (5 - 1). Brigand counters: dmg 11 (8 + 7 - 4), hit 90 displayed (77 raw: 83 - 6), crit 0. **This is where the seed matters.** Strike 1 Wren 10 (brigand 22 to 12), unless a 4 percent crit ends it at once; the counter lands 90 percent of the time (Wren 20 to 9); strike 2 Wren 10 (brigand to 2). Expected: brigand alive at 2, Wren at 9 or 20. Captain 1,4 to 4,3 by the road (1,3, 2,3, 3,3, 4,3, cost 4), waits. Noise check: 3,6 and 2,6 are 11 or more from every mill member; the group sleeps.

## Enemy phase 2 (if the brigand lives, at 2 HP)
It can attack Wren from 3,6, 2,7 (cost 2) or 1,6 (cost 4), and the captain from 4,4 (cost 3). Against Wren at 9 HP the kill flag is set (11 deterministic damage) and the score is above 100; at 20 HP it is about 11 x 0.896 minus the counter (Wren's 20 expected, capped at the brigand's 2 HP, times 0.5) = 8.9, against the captain's 10 x 0.888 minus 1 = 7.9. Either way the target is Wren. Tile: all three are Plain; exposure (player reach sets containing the tile, computed before the brigand moves) is 2 for 3,6 (Wren adjacent, captain from 4,3 via 4,4, 4,5, 4,6) and 1 for 2,7 and 1,6 (the captain cannot reach past the brigand's own tile), so the tie goes to cost: **brigand moves to 2,7 and attacks Wren.** If Wren is at 9 and the strike lands (90 percent), Wren dies and the brigand lives; otherwise Wren's counter (10 at 100 percent) kills it. Both are seed outcomes, not arithmetic. The mill group holds.

## Turn 3, player phase
Captain 4,3 to 8,3 by the road (cost 4). 8,3 is 3 from the soldier at 9,1: **the mill group wakes on this move, cause proximity**, one `GroupWoke` line. Captain waits. Wren (if alive, at 2,6) to 1,3 (1,6, 1,5, 1,4, 1,3, cost 4), waits; if she died on enemy phase 2 the two Wren commands print `no living unit` errors and nothing else changes.

## Enemy phase 3
Enemies act archer, boss, soldier (ascending id), each on the board the last one left.
- **Archer** at 10,2 (Mov 4, bow range 2) attacks the captain at 8,3. Its reachable attack tiles: 10,3 (cost 1), 9,2 (hill, 2), 8,1 (hill, 4, crossing the boss and the soldier), 9,4 (3); 7,2 is 5 away. The hill tiles score best (the counter does not exist at range 2 either way, so the hill's avoid decides nothing in the score; both hills score the same). Tie-break: exposure counts the captain's reach on the board before the archer moves, and both 9,2 (via 9,3) and 8,1 (via 8,2) are in it, so cost decides: **archer moves to 9,2 and attacks the captain**, no counter (+10 in its score). Dmg 5 (5 + 2 + 5 - 5), hit 87 raw (94 - 7), crit 1.
- **Boss** at 10,1 is 4 from the captain: **waits.**
- **Soldier** at 9,1 (Mov 4, lance range 1): attack tiles 8,2 (hill, cost 4 either way round) and 9,3 (cost 3 through the archer's tile); 7,3 and 8,4 are 5 away. 8,2 scores higher (the captain's counter loses hit to the hill's 10 avoid and a point of damage to its def), so **soldier moves to 8,2 and attacks the captain.** Dmg 8 (7 + 6 - 5), hit 83 raw (90 - 7), crit 0; captain counters 8 (13 - 4 - 1), hit 100 displayed? no: 98 - (soldier avoid: AS 5 - 1 = 4, + 1, + 10 hill = 15) = 83 raw.
- End of phase. The captain is at 22 minus whatever landed (worst case 9). Seed decides the hits, not the tiles.

## What would count as a divergence
A different brigand tile on either enemy phase; the brigand choosing the captain; the mill group waking at any other moment or by any other cause, or acting before turn 3's enemy phase; the archer or soldier choosing a different tile or target; the boss moving. Numbers in the forecast lines are also predictions and are checked.
