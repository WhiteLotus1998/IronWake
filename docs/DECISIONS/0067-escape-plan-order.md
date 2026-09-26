# 0067 — On Escape the rear plans first

Date: 2026-09-26. Issue 332, from the fifty-fifth round on the Design Table (#322): gate 4 on Brackwater Cut at `dusk: 5` read Dunstan as dead weight because the Sim's heuristic never attacks with him. Code's lean, built under the issue's own order (heuristic first, content only if the heuristic is fixed and he still fails). Provisional: Chat may argue it on the PR or the Table.

## The trace

Dunstan's line is the same on every seed traced (1 to 13): `move dunstan 13,3`, `move dunstan 17,3` (the bank wakes by proximity), `exit dunstan` on turn 3. It is not sight, the spotter rule, his lance's range or his staging. He planned first in id order and started nearest the exits, so he walked the lane ahead of everyone. On his own turn nothing was ever in reach. On turn 3, with the brawler and the shieldbearer beside him, the Escape rule sent him out ahead of any attack. His walk woke the bank next to the exits, and Wren and the captain were left to meet it there. Seed 10 lost the captain that way.

## Decision

On an Escape map the heuristic plans the captain last, as before, and the rest farthest from an exit first, by straight distance to the nearest exit tile (`HeuristicPlayer.PlanOrder`). Other maps plan in the units' own order, unchanged. The exit rule itself is unchanged.

## Arms measured (200 seeds, Brackwater Cut at dusk 5, two-roll average)

| arm | gate 1 | survivors p50, captain alone | gate 4 median | Dunstan drop (atk) |
|---|---|---|---|---|
| main (5d4056e) | 91 % | 2 of 4, 0 | 0.075 FAILED | -0.095 (0) |
| every recruit attacks before exiting | 84 % | 0 of 4, 168 | 0.022 FAILED | -0.160 (227) |
| a recruit above half HP attacks first | 90 % | 1 of 4, 30 | 0.085 FAILED | -0.105 (56) |
| the same, and holds on the exit while the captain cannot reach one | 90 % | 1 of 4, 18 | 0.085 FAILED | -0.105 (56) |
| any recruit holds while the captain cannot reach an exit | 84 % | 0 of 4, 96 | 0.022 FAILED | -0.160 (211) |
| **rear plans first (shipped)** | **95 %** | **2 of 4, 0** | **0.103 FAILED** | **-0.045 (2)** |

Every arm that makes a recruit fight on its way out costs gate 1 and the survivors count. The plan order is the only arm that improves the whole row: Pell 0.430, Rook 0.120, Wren 0.085. Wren now absorbs nearly twice what she did (781 against 365).

## What is left

Dunstan still fails, and no content change follows, because the remaining drop is not his. His benched arm wins all 200 seeds in every arm above, and in 95 of them the captain leaves alone (`benched survivors p50 1 of 3, captain alone 95`). Gate 4 pairs wins, and an Escape win is the captain leaving, so a bench that lets the captain slip out alone scores as well as a party that got out. DESIGN section 11 already says Escape maps are tuned on the survivors count. Whether gate 4 on an Escape map should pair survivors rather than wins is the Table's call. Until then Dunstan's row reads with its survivors column.

Revert if the plan order makes a replayed heuristic line worse on a later Escape map, or if the Table writes gate 4's Escape outcome on survivors and the id order measures better on it.
