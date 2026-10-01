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
