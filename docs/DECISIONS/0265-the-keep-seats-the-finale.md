# 0265 — The campaign keep seats the finale whole

Date: 2026-10-06. Issue #1149 (rounds 386, 387). The seating and the acceptance reads are the Table's; the wall, the menu's tiles, the raid's ground, the clock and the Sim flag are the Builder's, and all are provisional.

## Decided (the Table, restated)

- `content/keep/ironwake_keep.map` is the #692 finale: `fronts:` (north, gate, south), the `falls` waves, the sworn van, Marrit's hunt, hold-then-boss with Hask arriving on turn 6. No wave scales to the company (0151). No lines; any text waits on Lotus's story.
- The read that counts is `--finale` on the campaign map at level 8: full and depleted over gate 1, random under a quarter, gate 4's median drop above zero, the floor as data, 0151's time and length lines.

## Decided (the Builder, provisional)

- **The side breaches are two tiles wide** (`north 10,1 10,2`, `south 10,9 10,10`). The gate stays `10,5 10,6`. On the sample, a one-tile breach could take a wall only by being shut, and the keep's menu exists so no spend shuts a breach (`Keep.cs`).
- **The menu sits on the fronts.** Walls go at 10,2 and 10,9, each narrowing a side breach to one tile. The gate cannot be walled. Ditches go at 9,4 and 9,7, the flank tiles before the gate wall, leaving the gate's straight approach. With every edit made, every front stays open to foot (`EveryEditOnTheMenuAtOnceLeavesEveryFrontOpenToFoot`). The old tiles (10,3, 10,8, 9,5, 9,6) were wall already or shut the gate.
- **The clock is 12**, one past the sample's 11. It is the lever 0151 named, and the only one pulled. At 11, depleted read 110/200 (55 percent).
- **The raid keeps its wall.** The finale is fought on the wall the raid broke. The raid test now holds the ground off column 10, the raid's slots among the keep's, and its spawns on the west edge. The raid's hand plays and its "kept, never tuned" status do not move.
- **The old keep is pinned** as `docs/samples/ironwake_keep_1139.map`, with its menu tiles, for #288's journaled keep campaign, as `saltmarsh_ford_0030.map` is for its own. One script command moved deliberately: `2026-10-01-campaign-690-barracks.script`'s `build wall 10,3` became `10,2` (the same buy). The synthetic 644 save's wall moved the same way.
- **`--finale --gates`** runs gate 2 and gate 4 on the full company at the level, against its own heuristic games.
- **Full-campaign parity** moves to variant 18, which wins the new keep. Variant 24 lost it.
- Issue 287's "the van reaches both breaches on turn 1" held for the survive keep only. The finale's van reaches the gate on turn 1 and the side waves come on turn 2, so that test is retired.

## Found (`docs/measurements/keep-1149.txt`, 200 seeds, level 8)

Full 167/200 (83 percent). Depleted 136/200 (68 percent). Floor 0/200 (data). Random 0/200. Gate 4 median drop 0.095, with Pell carrying 0.72. Median length 10 to 12, slowest game 80 ms. Standalone `--full` at the cast's own levels reads 0/200 heuristic, so it no longer measures this map.

## Kill / revisit

0147's clause stands: if both chairs say the three fronts play as three small maps, they merge to two. Code's warm chair (seed 1149) argues against it. Once two fronts fell, the inside fight was one battle, and the hunter crossed from the south to the north. The cold chair is Chat's.
