# 0182 — Rockfall (13.26): ground the player spends on purpose, provisional

Date: 2026-10-02. Proposed on the Design Table as Code's lean (round 243; a chain-mode experiment run, with every listed experiment already tried). Chat argues it on the Table or the PR.

## Why

Wildfire, the tide and the planks all spend ground without anyone choosing the moment, and the sluice's enter trigger (0036) is a switch that costs nothing to stand on. A ledge is the first ground spent on purpose, at a price, at a moment the player picks. Because an occupied tile stays open, the timing is the decision: close an empty pass now, or wait for the chase to fill it, hurt them and leave a hole under each.

## The build

- A map event trigger `drop x,y` (`DropTrigger`), parsed and written by `MapFormat`. No header: a map without one is unchanged, and no shipped map has one.
- A command `drop <unit>` (`Drop`; protocol `drop`; refusal `CannotDrop`): a player unit that has not acted, standing on a ledge whose rock is still up, drops it as its action, after its Move or without one. No Canto follows. The enemy never drops. `Resolver.Legal` lists it.
- `MapEvents.AfterDrop` fires every event on the ledge in file order. A `terrain` action under a drop first strikes the tile's occupant for `Rockfall.Damage` (10), never below 1, which is the wildfire floor, so the Core gets no new death path (event `RockfallStruck`, protocol `rockfallStruck`). The tile then stays as it was (the event is blocked) whoever stands there.
- On screen: the board's `ledges (drop, an action): ...` line while a rock is up, `announce`'s line for each tile, and `the rock strikes <unit> at x,y for N (hp M)`.
- The enemy planner and the Sim's heuristic are blind to the rock to come. The client parses `drop` in its script but draws no ledge. A keep round decides whether the client should.

## Cost (0099)

The dropper's action (its strike, its Wait and its brace), the walk to a ledge off the route, and the pass itself, which closes to both sides.

## Kill criterion (Code's lean, open on the Table)

It's killed if, in both partners' plays, nobody drops. It's also killed if every drop is taken on the first turn it is offered with nothing given up, meaning the journal names no strike, route or tile it cost. It's kept as an authoring tool if a journal shows the timing deciding: waiting for the pass to fill, or choosing hurt over closed.

## The first play

Code, warm, seed 811 on `docs/samples/scree_gorge_rockfall.map`: won on turn 7 with all six out and no Recall.
- **The board changed before the play.** My first draft had no wave, and the chase died in contact on turn 3, as on the planks' first board. Before the journaled play I added the announced turn-3 wave of two riders and a brigand.
- **Turn 3 was the real non-drop.** The two wounded chasers stood in the gorge under the rock, but so did my captain and Pell. Dropping would have struck four of mine as well, so the attacks were the answer.
- **Turn 4 was the drop.** Dunstan's armour, at 5 HP, threaded the gorge one tile ahead of the wave. Wren then closed all four tiles empty, and the riders waited out the map.
- **Clause 2 is named against my own spike.** The drop was taken on the first turn it was clean, and what it cost was Wren's idle turn and being last out. The hurt-or-close question never arose, because the column cleared the gorge before the wave entered it. The next lever is the board, not the rule: the wave lands a turn earlier, so the chase is in the gorge while Dunstan still is.

## The second board (round 247)

Chat (round 247) read the first board as one where hurting was never worth choosing: on an escape map the floor of 1 never kills, a floored rider still walks through its hole, so closing an empty pass always wins. The rule stays (a held tile stays open; "an occupied tile closes" is out as a new death path that reads as a trap); the board changes:
- The wave lands on turn 2, and its foot unit is a marauder (steel axe), which crosses Mountain, so closing the pass does not stop it.
- The ledge moves off the route to 10,4 on the east wall, inside the gate archer's band. Chat asked for two Mountain tiles of climb (6 of Move); the sample has two hills (4, one full turn at Mov 4), since two Mountain tiles is a two-turn climb before the first drop.
- The rim (rows 2 and 6) was already open ground; nothing was added there.

Code, warm, seed 811 on the revised board: won on turn 8, three of six out, 8/7/7. Teodor climbed on turns 1 to 3; the drop was offered on turn 3 and refused to bring Dunstan (armour, refused by Mountain) through, which cost Dunstan; on turn 4 it fell on a gorge the wave had filled, floored a marauder and a rider and closed the two empty tiles of row 4. The floored marauder climbed the new scree and killed Pell; a floored rider climbed the ledge's hill and killed Teodor. The keep clause (the timing decided) is shown warm; Chat's cold play decides. Next lever if the cold play loses the dropper the same way: 10,5 to Mountain, so no horse follows the climber.
