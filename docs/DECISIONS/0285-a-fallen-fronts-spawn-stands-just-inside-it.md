# 0285 — A fallen front's spawn stands just inside it

Date: 2026-10-06. Issue #1204, lever 2 of 3; Design Table #1187, round 407 (Chat), answered by Code the same day. Builds what the Table agreed; content and a Sim data line.

## Context

On the campaign keep the inside spawns a front's fall fires stood a tile off the breach: north 11,0, gate 11,4 and 11,7, south 11,11, none beside a front tile. In Chat's cold 2210 (round 407) Pell and Brannock parked on 11,4 and 11,7, the gate fell on turn 4, and `Reinforcements are blocked` twice: the fallen gate cost nothing. Code's condition (round 407): moving the spawns onto the tiles a gate defender already holds may make the ordinary hold block the wave for free; if every gate fall in the Sim and in the next hand play is blocked, the wave is decoration, and the delayed blocked spawn goes to the Table (open since 408), not into #1204.

## Decision

- The inside spawns move to the tile just inside each front: north 11,1, gate 11,5 and 11,6, south 11,10, on `content/keep/ironwake_keep.map` and its measured sample `docs/samples/ironwake_keep_finale.map` together (the campaign keep seats the finale whole, #1149). 11,1 and 11,10 sit beside the front tiles the menu's wall never covers (10,1, 10,10) and beside the sample's one-tile breaches. `ironwake_keep_pair.map` keeps its old tiles so its journaled 695 play replays. Guard: `AFallenFrontsSpawnStandsJustInsideThatFront`.
- `--finale` prints a `falls:` data line per company: each fall event's games arrived, blocked and unfired, so the decoration read is a number, not a guess.
- The lever-1 replay of Chat's 2210 (turns 1 to 4) is the same play with the new tiles; `rejournal.py` rewrote its transcript (six lines, tiles only).

## The Sim

`--finale ironwake_keep --seeds 200 --gates`, level 8 (`docs/measurements/keep-1204-lever2.txt`): full 174 to 174 (87), depleted 143 to 140 (71 to 70), floor 0 and 0, gate 4 0.070 both. Blocked share of fired gate spawns: depleted 5 of 50 to 8 of 52 (gate_in1 and gate_in2 summed), floor 18 of 228 to 35 of 228. Inside noise for the win rate; the depleted floor of 60 holds.

## The decoration read

Not decoration in the Sim: most gate falls there let the wave in, before and after. But the one hand play on the new tiles, Chat's 2210 replayed to turn 4, falls the gate with defenders on 11,5 and 11,6 and blocks both spawns. The blocker now stands where the breachers strike, which was the lever's point; whether that is a price or still a free hold is the fresh keep chair's read after lever 3. If that chair's gate falls are all blocked too, the delayed blocked spawn (408) comes to the Table.
