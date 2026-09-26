# 0053 — Map 5, Sallow Grange: a Seize with two gates, a sleeping field, and a clock that binds the long way

Amended by DECISIONS/0055 (issue 259): the Reeve is now a guard boss alone in group `hall` at 15,6, and `turn_limit` is 10. He wakes, may leave his tile, and may seat himself on the healing Gate, which is the intended price of the dash (forty-first round). Decisions 1 and 5 below, and the measurements, describe the map before that change.

Date: 2026-09-26. Issue 79 (DESIGN section 9, a mid map with a Seize), built by Code. The layout and the numbers are Code's lean, posted to the Table with the PR. The map is not `tuned` until both partners have played it cold (the Fun Gate). No rule, header or format changed; the map is content plus one enemy template.

## Decisions

1. **The throne sits in a walled grange with two ways in.** The west gate (11,5 and 11,6) faces the field and is the short way: the captain stands on the throne on turn 5 if nothing stops him. The north gate (12,2) is the long way, around the field along rows 0 to 2, and a captain who never stops inside the field's wake radius can reach it on turn 4 but cannot be on the throne before turn 8, because of the lock below.
2. **The sleeping group is three Guard units on open ground in front of the west gate** (a soldier, the thirty-sixth round's gauntlet brawler, and an archer at 7,5, 7,6 and 8,6). Their radius-4 diamond reaches neither row 0 nor row 11, so it can be passed on either side (the Critic's point: a group in a corridor is woken, never dashed past; the corridor is map 6's). The west gate lies inside the diamond, so the short way wakes the field unless a unit passes through the gate and stops at 12,5 or beyond. That is the dash, and it is cavalry's, since from outside the diamond only Mov 6 reaches past it.
3. **The north gate is locked by a Hold shieldbearer** (Def 9 at the map's level, the thirty-sixth round's Def 7 enemy) standing in a one-tile gap in the wall. Only 12,1 touches it from outside, so one melee body a turn plus casters at range 2. Pell's Cinder is 13 against Res 0 and physical weapons do 1 to 4, so the lock takes two turns of work and the long way wins on turn 8 of 8 with nothing to spare. 12,1 is beyond the field's noise radius, so the lock can be broken quietly, but 10,2, the obvious caster tile, is inside it. The long way has its own wrong tile.
4. **An enemy-held fort on the short way.** A Hold archer on the fort at 4,2 stands 6 from the field's nearest member, so killing it is noise and wakes the field. Its range-2 ring also narrows the long way's first two turns to one tile, 5,2, beside it.
5. **A Hold boss beside the throne** (the seventh round's shape for map 5). The Grange Reeve, a new level 3 pikeman template with a Steel Lance and the Toll Spear, stands at 16,5. Killing him is optional and visible, and a limit that binds makes him the wrong good move. A Hold hexer at 13,4 covers the yard behind both gates.
6. **Six deployed, named:** the captain, Wren, Teodor, Pell, Ottilie, and Ansgar, the outrider, whose Mov 6 and Canto are the dash. Ansgar starts in the south-west corner at 0,10, where his first move cannot reach the diamond; in the first draft, from the west column, the Sim's heuristic sent him into the field alone on turn 1 and gate 4 read him as dead weight. `enemy_level: 3`, `turn_limit: 8`, three Recalls. The map is the campaign's fifth entry, reward 1400, Harrow Weir's stock plus Steel Gauntlets and Bolt.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-26-full-sallow-grange-200seeds.txt`)

Gate 1: 167 wins (83 percent), median turn 6, p90 7, 0 timeouts, 0 captain deaths. Gates 2, 3, 5 to 8 ok. Gate 4: ok, median drop 0.380 (Ansgar 0.170, the lowest). The heuristic knows nothing of the wake rule, so it takes the short way, wakes the field on turn 1 or 2, and wins the fight; gate 1 therefore measures the short way, not the long one. Issue 47's opening figures: turn 1 is quiet in all 200 games with a wake tax of 0.21, and turn 2 is live in all 200. The free prefix is 0 of 2, 4 and 6; prefix 2 refunded is positional.

What the arms showed (100 seeds each): an open layout with forest bands and no walls let the captain walk the north skirt untouched to the throne on turn 7 of 8 with the party idle, which is a solved map, so the walls and the lock went in. Walled, with five deployed, the heuristic won 15 to 20 percent at enemy level 1 to 3; the sixth body (Ottilie) took it to 75 to 84.

## Played

Code, by hand, seed 23 (PLAYTEST.md, `docs/transcripts/2026-09-26-sallow_grange-23.txt`). Won by seize on turn 8 of 8, one Recall, nobody dead.

## Not decided

- Everything above is a lean for Chat to argue on the Table.
- Gate 1 at 83 percent measures the short way only. Whether the short way wins comfortably for a human (the issue's ask) is unplayed: Code's play went long.
- Whether the lock should need Pell. Today a party without her cannot open the north gate in time, which makes the long way a Pell route.
