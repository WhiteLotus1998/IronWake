# 0372: The race stage: the turn limit ends no map while Hask stands swallowed

Date: 2026-10-09. Issue #1395. Chat's round 514 lean 1, agreed by Code in round 515: with Frozen Iron uncapped (0371), the dose is the clock, and the map's turn limit is a second one. Lotus's principle from the same day ("one clock per fight", relayed on #1368) points the same way. Provisional until Lotus sees it, since it changes what the finale's `turn_limit: 12` means.

## Decided

- **Built.** A template's `swallow` block takes an optional `race` (`KinStage.Race`; content, content writer and protocol `kin.race`). While a swallowed unit whose stage is a race stands (`BattleState.Racing`), the turn limit ends nothing (`BattleState.PastLimit`): the phases run on past it, the Frozen Iron keeps landing, and the header reads `turn N, past the limit: the race`. Stage 2 ends when he falls (a win) or the company does. Before the swallow the limit binds as on any map. `hask_warden` carries it, so it plays on the Warden sample alone. The card says so: "from then the turn limit ends nothing" before the swallow, "no turn limit while he stands" after.
- **The read** (`docs/measurements/keep-1395-race.txt`): **39 / 33**, from 38 / 28. The 92 / 79 stage-2 timeouts become deaths to the dose nearly one for one. Scratch screens, not shipped: stage-2 HP 16 reads 85 / 53, HP 12 110 / 70, against 85 / 52 and 110 / 68 with the limit.
- **So the limit was not the binding constraint.** The races it cut off were already lost: a company of 5 with its HP spent cannot out-hit 24 plus a 4 heal at Def and Res +3, and the dose finishes it at landing 4 to 7. The race stage stays for its rule (one clock per fight), not for its numbers.

## Open, for the Table

- The gap is his stage-2 bar against the company the swallow finds, in Chat's lever order: HP, then the heal, then the step. HP alone turns the race into a one-phase burst (HP 12: 70 of 110 kills in 1 player phase), so the heal at HP 24 is worth a screen beside the HP walk.
- The clock-death gate on won games only (round 514's lean 2): with no limit every stage-2 loss is a dose death, so the gate now has to read won games to mean anything. Won games' clock deaths at HP 24: 22 / 11 / 6 (0, 1, 2+) on full.
