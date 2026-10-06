# 0270 — The strike-then-finish chair: rank stays short with Teodor striking

Date: 2026-10-06. Issue #1167, Design Table #1151 round 392. Provisional (a measurement; nothing ships).

## Context

At levy floor offset three (0269 amended) the paying line fails round 389's bar on rank: Teodor p50 L6 with main-weapon rank points p50 11 (C is 80, about 27 strikes or 16 kills, DESIGN 126). Round 392 asked for a chair that feeds strikes, not only kills, and let its read decide whether rank takes its own lever.

## Decided (the Table)

- A fourth line in `--levels --focused`: each player phase, before another unit's planned plain attack, Teodor strikes the same target if the paying guard allows it, kill or not; a kill he can take is still handed to him by the paying rule; the price lines plus his strikes per map.

## Decided (the Builder)

- **`FocusedPlayer.Guard.Striking`.** It plays the paying line first. Where that hands nothing and the heuristic's plan is a plain attack by another unit, Teodor strikes that target from the best-scoring tile (`StrikeFrom`): weapon in range, target seen, ledger not refusing, his strike landing (a hit chance above 0), no no-crit forecast death, at most one more enemy in reach than the planned tile. No kill-chance gap is asked, since the strike need not kill. The planned attacker is re-planned next against the chipped target.
- **Every planned attacker, not only the captain.** In practice it is mostly the captain's plan: the heuristic plans in id order, the captain first, so Teodor has usually acted on his own plan before a later unit's attack is planned.
- **The bar is read on the striking line** from here on; the paying line stays printed. The price adds his combats per map (every attack he made, whoever planned it) and the chair's handed strikes per map.

## The read (`docs/measurements/levels-strike-1167.txt`, 200 runs)

- Even (the ceiling): unchanged from #1166, p50 0 at L7+C and p50 0 at L7 alone after map 8. Holds.
- Striking: 15 kills handed; refused no tile 403, kill chance 377, exposure 10, forecast death 94. p50 0 at L7+C after map 8; the bar fails.
- Price after map 8 (82 runs): captain p50 L7; Teodor p50 L6, rank points p50 12 p75 26 (paying: 11, 19); per map, combats p50 2, handed strikes p50 0 p75 1; p50 69 HP lost in enemy phases, 218 falls (paying 251).

## What it means

Feeding strikes barely moves rank: p50 one point, p75 seven. The chair finds a safe extra strike less than once a map, and Teodor already attacks about twice a map on his own plan. Round 392's condition is met: rank C at map 8 is out of reach with him striking every safe turn, so rank takes its own lever, its own issue and its own read. One gap to trace before that lever is sized: two combats a map over eight maps would pay about 48 points, yet rank reads 12; falls (a fallen unit returns as it began the battle, 0268) and maps he sits out are the likely sinks, unmeasured here.

## Kill criterion

Revisited if a trace shows the chair's strikes are being lost to a Sim artefact rather than to the board (the combats-to-points gap), in which case the chair is re-read before any rank lever.
