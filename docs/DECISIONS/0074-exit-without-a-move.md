# 0074 — An exit is taken without a Move (Brackwater's bank collects)

Date: 2026-09-27. Seventieth and seventy-first rounds on the Design Table (#364), issue 377. Provisional in the ordinary way: Chat's lean, in the wording the seventy-first and seventy-second rounds agreed.

## Decision

1. On an Escape map, `exit` is taken without a Move. Only a unit that began its turn on an exit tile leaves; one that moved this turn is refused with `MovedBeforeExit` ("it moved this turn; a unit exits without moving, from an exit it began its turn on"). `Resolver.Legal` offers `exit` only to such a unit. This amends DECISIONS/0056, which allowed the exit after a Move.
2. The wording is the seventy-first round's amendment, agreed in the seventy-second, of Chat's "began the player phase on an exit tile". The toll is the same, it reuses the moved flag, and no start tile is stored. What is lost is a unit on 19,3 sliding along the exit column to 19,6 and leaving in one turn.
3. The exits legend and the objective line say it on screen: `a unit that starts its turn on one may exit as its action, without moving` and `a unit that starts its turn on an exit may leave`.
4. The header `exit_after_move: on` keeps the older rule and its older wording. It is refused off an Escape map, and it sits only on the sample maps whose journaled plays were made under the older rule: `brackwater_cut_daylight`, `_dusk`, `_keepsakes`, `_shove`, and the new `brackwater_cut_exit_after_move`, which is the shipped map before this record. The shipped dusk plays (Chat 17, Code 23, 41, 53, Chat 241) now replay on that sample.
5. The Sim's heuristic stands then leaves. A unit that began its turn on an exit leaves ahead of any attack; one that can reach an exit moves to the cheapest and waits. The captain leaves last, holding its exit while another unit stands on an exit or can still reach one this turn, except on the last turn, when it leaves regardless.

## Measured (Brackwater Cut at dusk, 200 seeds)

Gate 1 64 percent (95 before), median 6, p90 6, 72 timeouts, 0 captain losses; survivors p50 0 of 4, captain alone 81. Gate 4 ok at a median of 0.158 on units out; Dunstan +0.020 (it was -0.035), Pell 0.370, Rook 0.185, Wren 0.130. Gates 2, 3 and 5 to 8 pass. Gate 1 stays above 60, so the issue's fallback (moving a bank member) is not used.

## Why

Chat's seed 241 re-rate showed the bank dodged by reading: four units staged outside the bank's wake radius, one move from 19,3, and left in the same phase that woke the bank. Every exit is within 4 of a bank member, so under this rule no exit can be stood on through an enemy phase with the bank asleep. The exits become a door that can only be paid for, the Tollgate's principle (DECISIONS/0072).

## Records

Code's play from Chat's staging on seed 241 under the rule: `docs/transcripts/2026-09-27-brackwater_cut-241-stand.*`, and its PLAYTEST.md entry. Tests: `ExitTests`, `HeuristicExitTests`, `MapHeaderExitsTests`, `CliPlayTests.ChatsSeed241WalkOutReplaysOnlyUnderTheOlderExitRule` and `CliPlayTests.TheJournaledStandOnTheExitsPaysTheBankOnSeed241`.
