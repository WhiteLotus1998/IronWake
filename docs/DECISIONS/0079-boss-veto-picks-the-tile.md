# 0079 — The boss veto picks the tile, not the swing

Date: 2026-09-27. Seventy-sixth round on the Design Table (#364), issue 389. Amends DECISIONS/0077. Provisional in the ordinary way: sentence (1) of Chat's issue, agreed by both partners; sentence (2) not built.

## Decision

1. A boss under the veto whose every strike is refused still ends somewhere: the approach tile the veto passes, or its own tile when none passes. Once that tile is chosen it takes the best strike it can make from that one tile by the ordinary score, grudge and keepsake precedence included, with the veto off for that swing. It Waits only when nothing is in reach from the tile. It never moves to a tile the veto refused, so no pocket opens.
2. `threat` reads the same swing (`EnemyAi.StrikeOn` through `SwingFromEnd`, one `Destination` helper shared with `PlanUnit`), so it names the strike from the end tile and still omits every refused strike.
3. Sentence (2) of issue 389, a kill leaves the sum, is not built. `Exposure.Plan` already removes a target killed with certainty (DECISIONS/0024), and counting a probable kill out makes the veto bet on a die. It is looked at again only if the runner survives (1), and then with a hit floor.

## Measured (Harrow Weir, 200 seeds, Release)

| | Gate 1 | Median / p90 | Losses (timeout) | Captain deaths | Gate 4 median drop |
|---|---|---|---|---|---|
| before (#385, 0077) | 64 percent | 9 / 12 | 53 | 19 | 0.305 |
| veto picks the tile (#389) | 70 percent | 8 / 12 | 43 | 17 | 0.310 |

All eight gates pass. Ten of the 53 timeouts were the runner the rule removes.

## Traces

- **Chat's seed 293 script**: on enemy phase 10 the Foreman, on 13 at 14,9, swings at Keziah on 11 (25 percent, miss) instead of Waiting. The rest of the script replays to the same win on turn 11. Test: `CliPlayTests.AWornBossSwingsFromTheTileTheVetoLeftHimOnSeed293`; transcript `docs/transcripts/2026-09-27-harrow_weir-293-swing.txt`.
- **Chat's seed 263 and Code's seed 379 scripts**: unchanged, he steps to 12,6 and Waits; 10,6 stays closed (`TheWokenForemanRefusesTheTenSixPocket`).
- **Code's ranged-only seed 263 script**: on enemy phase 4 he steps onto the bridge head at 11,6 as before and now swings at Pell (13, Pell on 3); Pell's counter crits for 36 and he falls there. 10,6 stays closed (`TheVetoedForemanSwingsFromTheBridgeHeadOnSeed263`).
- **Code's seed 389 hand play**: he swung whenever the tile he kept was his own, and ran whenever the party's staging refused it (enemy phases 7 to 9, to the mountain at 15,0).

## Open

- The runner survives where the staging refuses the boss's own tile: the approach then takes him to a tile the party cannot reach. Rule (1) cannot fix that, because it never changes the tile. The levers left are 0077's own open item (with every tile refused, hold and swing rather than walk) or (2) with a hit floor. Neither is built; the re-rates decide.
