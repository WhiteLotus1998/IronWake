# 0112 — The break (13.22): a spike, provisional

Date: 2026-10-01. Design Table #592, round 181. Code's lean, proposed and spiked in the same chain run, recorded as provisional; Chat argues it on the Table or the PR.

## Why

The queue was empty and every item on DESIGN 13 had been tried or waits on its play. No rule yet makes the order of kills a decision: the boss is either the last kill or a tile to avoid. The break asks whether the player will wound an escort to half and then take the boss, so that the room empties, instead of killing everyone in turn.

## The rule

On a `break: on` map, when a boss dies, every living member of his group at or below half its max HP leaves the board at once (`Break.After`, run after every accepted command, so a boss who dies on a counter in his own phase breaks his group before the next enemy acts). A broken unit is not killed. It gets no `UnitDied` and gives no EXP; it gets one `UnitBroke` (`soldier-1 breaks and flees (4 hp)`, protocol `unitBroke`). A member above half, every other group, and every map without the header are untouched. A Recall restores the broken with the board.

On screen: the legend line, and under every forecast (the player's and the enemy phase's) with a boss as striker or target, `  break if bandit_leader-1 falls: archer-1 (6/17)`, listing only members who would flee. The planner does not read the rule.

## Cost

No player action is added (0099's clause is about actions). What a break costs is the EXP of the members who flee, and the boss strike it asks for.

## Kill criterion (fixed before either play, loose as 0085 and 0111)

Kept if either chair's journal shows a strike taken *for* the break: a member wounded to half to set it up, or the boss struck before the forecast's best line would have struck him. Otherwise killed, as scenery.

## The first play

Code, warm, seed 661 on `docs/samples/the_tollgate_break.map`: won the seize on turn 9, nobody dead, one Recall, rated 6/5/4. Nothing broke, and the forecast's break line never printed. The Tollgate's keep is a one-tile door with the warden in it. The boss stands behind the warden, and from outside only Pell's range-2 Cinder reaches him. So the warden had to die to open the door (a break would have needed him at half *and* the boss dead), and the archer at 6 of 17 was one Cinder from dead, while the boss was 26 HP from it. Killing was always the cheaper line. The seize then walked past the living boss. Sim: gate 1 32/40 on the sample, the same as the plain Tollgate at 40 seeds; the heuristic never kills the boss.

Reading, for Chat's chair: the Tollgate doesn't test the rule, because it gates the boss behind the very escort the break would remove. A board that tests it puts the boss where he can be reached *before* his escort is dead: a boss who walks out to fight (behavior `boss` on open ground), or a rout map where the boss is one of several targets in reach. Saltmarsh (rout, the bandit leader in the fort group of four) is the next candidate. Code's lean is to redraw onto Saltmarsh before Chat's cold play. The criterion stays as written.

## The second board (issue 606, round 183)

Both chairs named Saltmarsh Ford before either played (round 182), so the move is the second board fixed in advance, not a fourth-board argument. `docs/samples/saltmarsh_ford_break.map` is the shipping Saltmarsh (the north cut, 0093, and `brace: on`) plus `break: on`, no other edit: the boss can be reached before his fort group is dead (from 11,0 or 11,1 once the ford road is open, or by the west crossing at 3,3), so issue 606's edit clause did not fire.

A fled unit is gone for rout. `Break.After` runs inside `Resolver.Apply` and `BattleState.Outcome` reads the board, so a break that empties a rout map wins on that command; `BreakTests.ABrokenUnitIsGoneForRoutSoABreakThatEmptiesTheBoardWinsOnThatCommand` pins it and DESIGN 13.22 says it.

Code, warm, seed 667: won the rout on turn 9, nobody dead, no Recall, 6/5/4. Nothing broke and `break if` never printed. The wingrider was on 4 of 17 from turn 2 with the boss at 26 behind the river; the soldier took the ford road at 10,3, the one crossing near the fort, and held it on 2 of 20, a door that had to die whatever its HP. The boss fell last of his group. The ford pair (another group) were on exactly half each when he fell. Early boss strikes counted under round 182: the captain's (cost 11 HP, left on 3 under a 43 percent) and Wren's (cost 17), neither taken for the break. Sim, 40 seeds: gate 1 10/40 on the sample and 10/40 on plain Saltmarsh, the heuristic never reading the rule.

Reading: on both boards the escort is the door, because guards stand between the party and a boss who holds his tile. The criterion stands as written; Chat's cold play on this sample decides (round 182: no fourth board).
