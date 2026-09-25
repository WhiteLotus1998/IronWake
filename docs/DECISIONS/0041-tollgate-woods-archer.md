# 0041 — The Tollgate: the woods archer stands inside the brigand's band

Date: 2026-09-25. Issue 208, from Chat's cold re-rate of DECISIONS/0035 (seed 139, 7/7/6, twenty-ninth round). Built by Code; Chat argues on the PR if it disagrees.

## Decisions

1. **The woods archer moves from 5,6 to 5,5, and 5,5 becomes forest.** Its neighbours are 4,5 (hill), 5,4 (plain), 5,6 (forest) and 6,5 (the toll brigand), each at distance 1 or 2 from 6,5, so a melee strike on the archer eats the thrown axe while the brigand lives. At 5,6, the tiles 4,6 and 5,7 lay at distance 3 and the archer died on turn 3 of every play without firing.
2. **Forest, not Chat's plain candidate.** The forest's 20 avoid keeps the archer from being a second free kill once the brigand is down; the woods group keeps its name. A side effect, kept: 6,4 is at range 2 of the archer, so the hill is now covered by both woods units.
3. **Two tests hold it.** Every passable neighbour of the woods archer is within 1-2 of the brigand, and some passable tile at range 2 of the archer is outside the brigand's band, so a bow or a tome still reaches it and the group is not a wall. A second test pins the boss's choice from Chat's seed 139: with the warden dead, the captain at 6,2 and Teodor at 6,3, the boss throws the Toll Axe at Teodor.
4. **The limit stays at 10.** Gate 1's p90 is 9, as before.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-25-full-woods-archer-200seeds.txt`)

| The Tollgate | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| DECISIONS/0035 | 137 (69 percent), median 8, p90 9 | 63 timeout, 0 captain, tail 0.3 | ok, 0.330: Pell 0.455, Wren 0.330, Teodor 0.250 |
| Archer at 5,5 in forest (shipped) | 144 (72 percent), median 8, p90 9 | 56 timeout, 0 captain, tail 0.3 | ok, 0.225: Pell 0.495, Teodor 0.225, Wren 0.085 |

All eight gates pass. Wren's drop falls from 0.330 to 0.085: much of her share was the free kill. She still clears the gate.

## The hand play

Seed 131, seize on turn 9 of 10, no Recall, Teodor lost. Turns 1 and 2 repeat the seed-127 line; from turn 3 the line is new. The woods took three turns instead of one: Pell answered on the brigand, then five misses at 41 to 58 on the archer in the forest, and Pell's 86 from range 2 at 3 HP killed it with a 56 percent counter waiting. The seed-127 replay test is retired, since its turn 3 was the free kill; its transcript stays as the record of the build it was played on. PLAYTEST.md has the entry.

## Not decided

- Whether a three-turn woods on a bad seed makes the limit too tight. It bit here (seize on turn 9), and the old line had one turn more of slack. Chat's cold re-rate decides; the rider is the next lever if surprise stays at 6.

## Decided after (thirty-second round, 2026-09-25)

Chat's cold re-rate is seed 151, 8/7/7, seize on turn 9 of 10 with no Recall and Teodor lost at the door. Kept, both partners: the forest at 5,5 is right (the archer cost three attacks and a shot where the old one cost one and none), the limit stays at 10, and Wren's 0.085 is not a problem. The map passes the Fun Gate from Chat's chair and fails only on Code's surprise of 6; the rider arriving by map event during the woods fight is #222, and the enemy phase's unreadable numbers are #217.
