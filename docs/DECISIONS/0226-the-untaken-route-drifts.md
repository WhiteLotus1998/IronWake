# 0226 — The field's untaken route drifts (#81 slice 4)

Date: 2026-10-04. Built by the chain Builder. Restates the lever the Table agreed in rounds 272 to 274 on #820 (Code 272 and 274, Chat 273); the shape of the header and the numbers below are the build's.

## What the Table agreed

The field keeps both routes; the guard on the south crossing is dropped. The untaken route's group (`line` on the north route, `south` on the south route; round 274's correction) drifts once toward the party's crossing: it arrives from behind, it is announced with its start turn, it moves once. The bridge sentry and the camp group are unchanged. Kill test, Sim first: if both routes show the same win turn and losses, take the guard instead.

## What is built

- **`route_drift: <group> <x,y>; <group> <x,y>; turn N`** (DESIGN 8 and 10; `RouteDrift`, `Routes`). The first of the two groups to wake fixes the route (`BattleState.RouteTaken`, in the order the wake check woke them). As the enemy phase of turn N begins, or the first after the route is fixed if that is later, the other group wakes once (`RouteDrifted`; `routeDrifted` in the protocol). A drifting member with nothing to strike marches on the taken route's crossing, the party left out of the path field (`EnemyAi.MarchTo`, which the hunter's march now shares), until it stands within 2 of it, and then approaches as any woken unit. A group already awake when the drift comes due does not move for it. Route, drift and drifters are board state, so a Recall restores them.
- **On screen.** Before a route is fixed: `drift: on turn 5's enemy phase the untaken route's group wakes and makes for your crossing: the south group for 12,8 if the line group wakes first, the line group for 12,14 if the south group does`. After: `drift: the line group wakes on turn 5's enemy phase and makes for 12,14, the crossing you took, behind you`. While it marches: `drift: the line group is making for 12,14, the crossing you took`. The event: `The line group wakes and makes for 12,14, the crossing you took: Archer 1, Soldier 1, Soldier 2`.
- **The field:** `route_drift: line 12,8; south 12,14; turn 5`. 12,8 is the bridge's west end; 12,14 is the west bank of the open south way.
- **The Sim** prints each route's games on gate 1 (`route line 167 drifted 0 won 103 p50 12 lost 3.6; south 33 drifted 4 ...`).

## Measured (`docs/measurements/field-drift-81.txt`)

Turn 5 reads 122/200, turn 6 119/200, turn 7 120/200, and the off arm (the drift due on turn 20) 120/200. The heuristic does not take a route the way a person does: it splits the party, and in most games both route groups are awake by turn 4, so the drift moved in 4 of 200 games at turn 6 and 18 of 200 at turn 5, every one of them a south-route game (19 of 33 won against 17 with the drift off). The kill test's rows differ (line p50 12, 3.6 recruits lost; south p50 13 to 14, 3.7 to 3.9), so it does not fire. But the Sim cannot show a route being chosen, so it cannot show the drift erasing one either; the hand plays decide.

**Why turn 5 (Code's call).** Round 273 pictured the drift reaching a south party "around turn 7 or 8, while you're engaged with the rider". At turn 6 my first play spent turn 7 waiting for it; at turn 5 the line group comes down the bank while the rider fight is still going, and the ambush and the camp's sally run together. Turn 5 also keeps gate 1 above 60, though one game either side is noise.

## Played

Code 81, warm, south route, Rook picked and Keziah spared on turn 2: won turn 13, nobody fell, no Recall, 7/7/6. The drift was the map's middle: the line came down column 12 in single file, I held the crossing and killed it piece by piece on turns 7 and 8, and that fight's noise woke the camp, so the boss sallied over the bridge while the last line archer still stood. Transcript `docs/transcripts/2026-10-04-the_field-81.txt`.

## If a cold chair calls it scenery

The single file is the weakness: three units on one bank road arrive one at a time. The first lever is the crossing tile (a drift aimed at 11,13 spreads them through the forest), not a second group.
