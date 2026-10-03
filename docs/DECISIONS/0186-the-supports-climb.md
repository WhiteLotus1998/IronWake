# 0186 — The supports' climb: the Sim's floor reaches no tier (#77 slice 4)

Date: 2026-10-03. Built by the chain Builder. A measurement; no number moves. The lean below goes to the Design Table.

## What is built

`--supports [--seeds N]` on the Sim. It runs the same campaigns as `--levels`: the heuristic player through all nine maps, each won map's record carried into the next, up to the usual tries a map, permadeath off. Since 0185 the record carries rapport, so the climb is read off it after each won map. Only support pairs count. Per map it prints how many pairs stand at C, B and A, and the points of the best pair, the best recruit pair and the best captain pair, each as p25, p50 and p75. Then, per pair in file order, over the runs that finished the campaign, it prints the final points (p50 and p75) and how many of those runs reached each tier. The 200-seed read is `docs/measurements/supports-77.txt`.

## What it read

- **No pair reaches a tier in the median run, on any map.** After Brackwater (map 8, 82 runs won it) the best pair p50 is 12. After the keep (12 runs finished) it is 12, with p75 16. Over the 12 finished runs, three reached C on any pair (Wren and Pell once, Ottilie and Wren twice). None reached B or A.
- **The captain's pairs** top out at Wren p50 10, then Teodor and Ottilie at 8. Six of the ten finish at 1 or below.
- **Pairs joining late, or not deployed by the fill order,** stay at 0: Brannock's, Rook's, Keziah's, Maud's and Pell's except with Wren.

## Why it reads so low, and why it is a floor

- The heuristic player stands nobody together on purpose, and accrual is bought under fire: only a phase where an awake enemy could strike one of the pair counts (0042).
- The rates come from `rapportRate`: Cha 0 gives 1, Cha 3 gives 2, Cha 5 gives 3, Cha 8 gives 4. A recruit pair at Cha 3 to 5 earns 4 or 5 a threatened phase together, so A at 72 asks for 15 to 18 such phases. That is a whole campaign of deliberate pairing.
- A captain pair accrues at the recruit's rate alone (0185), 2 for most recruits. A at 72 then asks for about 36 threatened phases beside one partner, which is close to every threatened phase the campaign has, and the captain has ten partners. 0184's own reasoning ("C within a map, B a few maps in") assumed both rates for every pair and a pair that stays together, and the floor shows neither holds.

## The lean (to the Table, not built)

- **A captain pair should be reachable at A by a player who commits to it.** Today that is out of reach by arithmetic, and the captain pairs carry the romances.
- **The lean:** keep C at 16, the overwrite. Lower B to 28 and A to 48. Leave the captain's rate alone (0185's hub argument stands). A captain pair at 2 a phase then needs 24 threatened phases for A, and a recruit pair 10 to 12, so B lands a few maps in and A by Brackwater for a pair that has been kept together.
- **What decides it:** the Sim needs a pairing player before the numbers move. That is a heuristic that, given one named pair, ends its moves beside it where the tile is no worse, which reads the deliberate ceiling beside this floor. Until then, play is the measure: a chair's campaign through Brackwater names the pair it raised and where it stood.
