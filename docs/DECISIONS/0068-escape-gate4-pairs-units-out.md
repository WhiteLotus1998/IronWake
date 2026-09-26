# 0068 — On an Escape map gate 4 pairs units out, not wins

Date: 2026-09-26. Issue 338, from the fifty-eighth round on the Design Table (#322). Chat proposed the definition in answer to Code's lean in the #332 follow-up, and Code agreed, with a correction on the standard error. #339 was a duplicate filing of the same round and is closed. This record restates what the Table agreed. Provisional in the ordinary way: the Monday review may reopen it.

## Why

An Escape win means the captain left, so gate 4's paired win drop scores a bench that strands the party the same as a party that got out. On Brackwater Cut at dusk, the heuristic with Dunstan benched wins 200 of 200 seeds, and in 95 of them the captain leaves alone. Section 11 already tunes Escape maps on survivors, so gate 4 should read the same variable.

## Decision

1. Per seed, a loss scores 0. A win scores the captain plus the recruits out (`Gates.UnitsOut`), so it scores at least 1. A captain death or a protected recruit left behind is a loss, as it is in gate 1.
2. The benched recruit is left out of the baseline's count too, so both arms count the same units. A recruit's own exit is reported by the survivors column, not scored as contribution.
3. The drop is the mean paired difference (`Gates.PairedUnitsOut`). The SE is the paired-difference one, the sample standard deviation of the per-seed differences over sqrt(n) (`Gates.PairedDifferenceError`), because McNemar's `sqrt(b + c) / n` holds only for 0/1 outcomes. Maps that are not Escape keep the old formula. A test shows the two agreeing to three places on a 200-seed 0/1 table. The cast verdict, the per-recruit rule `drop + 2 * SE < 0.5 * median`, the margin, the three action mixes, losses by cause and the survivors column all apply unchanged. The header names the scale, `units out, 0 to N`, where N is the deployed units minus one.
4. Each row prints the win-pair drop beside the new drop, `win drop X se Y`, unjudged.

Gate 1 is unchanged, and maps that are not Escape pair wins as before. `GameResult.Out` now carries the ids that `RecruitsOut` counts.

## Measured (Brackwater Cut at dusk 5, 200 seeds, two-roll average)

| recruit | units-out drop (se) | win drop (se) | benched survivors |
|---|---|---|---|
| dunstan | -0.035 (0.028) DEAD WEIGHT | -0.045 (0.015) | p50 1 of 3, captain alone 95 |
| pell | 1.425 (0.082) | 0.430 (0.046) | p50 1 of 3, captain alone 0 |
| rook | 0.295 (0.049) | 0.120 (0.024) | p50 1 of 3, captain alone 0 |
| wren | 0.225 (0.052) | 0.085 (0.021) | p50 2 of 3, captain alone 0 |

The median drop is 0.260 (it was 0.103 on wins), so the cast verdict passes. Gate 4 still fails, on Dunstan alone. Chat predicted that his drop would turn clearly positive once gate 4 paired units out. It did not. His baseline attacks number 2 in 200 games, and the others get out as well without him as with him. Under the terms of the fifty-eighth round this is a content finding for Brackwater's layout (a corridor unit with no corridor worth holding), not a finding against Dunstan or against the gate. The heuristic never fighting with him is still part of the reading.

Revert if a later Escape map shows the units-out drop and the win drop disagreeing in sign on a recruit whose play reads the win drop as the truer one.
