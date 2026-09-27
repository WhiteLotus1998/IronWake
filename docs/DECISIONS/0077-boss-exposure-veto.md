# 0077 — A Defeat Boss boss plans under the exposure veto

Date: 2026-09-27. Seventy-fourth round on the Design Table (#364), issue 385. Provisional in the ordinary way: Chat's lean, agreed by Code in the same round, with the ranged-only amendment.

## Decision

1. On a `defeat_boss` map the boss plans under the exposure veto of DESIGN section 11 read from the enemy side (DESIGN section 8, "The boss veto"). While it may choose where it ends (Aggressive and not yet moved), a strike is refused when the no-crit sum on the tile it would end on, the counter it takes included, reaches its current HP. An approach tile is refused on the same sum. With every strike refused it takes the best approach tile that passes; with none it Waits.
2. The sum is `Exposure.OfBoss`, not `Exposure.Of`. It uses the same board and counter, but prices the party the way the player phase can actually play: every player unit may move, a strike tile another player unit stands on is open to it (the phase plays in any order), and each strike tile seats one striker, the worst seating, the count `threat`'s total already makes (issue 253, now `Exposure.SeatedSum`). Without the open tile the pocket survived: on Chat's seed 263 Pell stood on 9,6, the only melee tile besides 11,6, so Keziah's 14 was priced out and 10,6 passed.
3. A boss that holds (asleep, or a plain `behavior:boss`) keeps striking from its own tile. It has no tile to choose, so there is nothing to refuse.
4. `threat` reads the planner's own strike (`StrikeOn`), so a refused strike is not listed. The issue's acceptance said `threat` is unchanged. It is unchanged as text, but it stops naming a strike that will not come, which is section 8's promise.
5. The Sim's captain veto (`Exposure.Of`) is untouched, so no other map's player planner moves. Harrow Weir is the only shipped `defeat_boss` map.

## Measured (Harrow Weir, 200 seeds, Debug)

| | Gate 1 | Median / p90 | Losses (timeout) | Gate 4 median drop |
|---|---|---|---|---|
| before (#379) | 67 percent | 7 / 9 | 25 | 0.265 |
| boss veto (shipped) | 64 percent, ok | 9 / 12 | 53 | 0.305, ok |

All eight gates pass. Gate 7's slowest game is 37 ms. The lever #379 named (the limit, 14 to 16) is not needed.

## Traces

- **Chat's seed 263 and Code's seed 379 scripts** on the built file: on enemy phase 3 the Foreman steps to 12,6 and waits. Neither line can strike him on turn 4. Test: `CliPlayTests.TheWokenForemanRefusesTheTenSixPocket`.
- **Chat's seed 257 script**: he still strikes Keziah on 10,6 from 12,6 (the bridge case of 0075), since only 9,6 and 10,6 reach 12,6 from the west bank.
- **The ranged-only opening, played** (Code, seed 263, turns 1 to 3 as Chat played them): Pell breaks the shieldbearer alone from 9,6 on turn 4. On that enemy phase the Foreman steps onto the bridge head at 11,6, where only 10,6 and 9,6 reach him (27 against his 28), strikes nobody, and holds it. Pell's Cinder on turn 5 costs 13 of Pell's 16 to the Toll Axe counter. The ford's soldier killed Ottilie that turn (a Recall, the ford's, not his). Worn to 7, he held 11,6 with every tile refused and died to Pell on turn 6. The ford collected during the chipping turns, as the amendment asked.

## Open

- A boss with every tile refused holds where it stands, even when that tile is refused too. On the 263 play that was a Foreman on 7 waiting to die on the bridge. The alternative is the least exposed tile. Left for the re-rates to judge.
- Dusk: the boss sum does not ask whether the party sees the boss, so it overcounts at dusk. No `defeat_boss` map is at dusk.
