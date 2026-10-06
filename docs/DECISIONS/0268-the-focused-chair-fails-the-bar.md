# 0268 — The focused chair is read: the bar fails, and the fed unit never leaves level 1

Date: 2026-10-06. Issue #1157 (round 389). The Table's bar and lever order restated; the read is the Builder's; the lever question goes back to the Table.

## Decided (the Table, restated)

- `--levels --focused` reads three chairs over the same seeds: the even chair (the ceiling), a focused guarded line (the even chair's guard, every kill to one unit) and a focused paying line (at most 15 points less kill chance, at most one more enemy in reach, still kills on a hit, no no-crit forecast death). The bar is p50 1 non-captain at L7 and rank C after map 8 on the paying line; the even chair stays at p50 0. The paying line prints its price (captain's level, kills given up, the fed unit's enemy-phase HP lost and falls).
- If it fails, the levers in order: the joins' levels and ranks, then the kill line's base (with `--ladder` re-read), enemy levels untouched, each its own issue.

## Decided (the Builder)

- **The fed unit is Teodor** (`FocusedPlayer.Fed`, a constant). The whole levy is in the roster from map 1 and Maud, the healer, arrives on map 2; Wren comes first in cast order but her sword has no door (round 389).
- **Every refused offer names its clause**, first to fail: no tile (none in range, seen and killing on a hit at any chance), kill chance, exposure, forecast death. Without this the read could not say which guard is binding.

## The read (`docs/measurements/levels-focused-1157.txt`, 200 runs)

- Even: 281 handed, p50 0 at L7+C after map 8, identical to `levels-even-1150.txt`. The ceiling holds.
- Guarded: 21 handed; refused 449 no tile, 560 kill chance, 2 exposure. p50 0.
- Paying: 28 handed; refused 464 no tile, 443 kill chance, 22 exposure, 100 forecast death. p50 0. The bar fails.
- Price after map 8 (81 runs): captain p50 L7, Teodor p50 L1 (p75 2), rank points p50 11; 0 kills given up; Teodor p50 72 HP lost in enemy phases, 251 falls (about three a run, Recalled under permadeath off).

## What it means

The paying guard is not the cap. Teodor is offered roughly 1,000 kills and can take almost none, because a level-1 pikeman cannot kill level-6 enemies on a hit or comes nowhere near the captain's chance. Feeding him is not possible on this curve: there is nothing he can take. That is the lever's case. But **lever 1 as written does not reach him.** "A main joiner from map 3 on arrives at about one level per map" covers Maud, the claimants and Ansgar. It does not cover the levy, which is seated at level 1 on map 1 and is never a joiner. The Table decides between a lever that seats the levy along the curve (for example, the levy's level at each camp floored at the map number less two), and feeding the claimant instead (seated at L4, rank 30, on map 6).
