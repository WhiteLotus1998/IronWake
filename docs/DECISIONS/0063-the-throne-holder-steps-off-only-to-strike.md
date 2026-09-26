# 0063 — The throne-holder steps off only to strike

Date: 2026-09-26. Issue 321, from Chat's cold play of the Seize drift sample on seed 41 and the fifty-first to fifty-third rounds on the Design Table (#265, #322). This record restates what the Table agreed and adds what the build measured. It is provisional in the ordinary way.

## Decisions

1. **On a Seize map, an enemy on the throne when it acts leaves it only to strike this phase.** Its attack options are unchanged, so a holder that can strike from any reachable tile still moves and strikes. One that cannot strike Waits on the throne instead of taking the approach or drift. One predicate, `EnemyAi.HoldsTheThrone`, read in `PlanUnit` after the attack options. `threat` and the exposure sum count only strikes, so they already agree with it and needed no change. The rule applies in daylight and at dusk.
2. **Where it binds on shipped maps.** Sallow Grange is the one daylight map the clause touches: the Reeve seats himself on the Gate, and when seated with nothing to strike he now stays instead of walking toward a party he cannot reach. His kite with the Toll Spear from the yard mouth survives, and a test holds it. No other shipped Seize map has a holder: the Tollgate's boss is a Boss beside its throne, not on it, and the outrider trial's hexer holds its own tile. So the clause is a change on Sallow and insurance everywhere else.
3. **The drift sample swaps brawler and archer** (fifty-second round): brawler to 8,6, archer to 7,6, nothing else moved. The brawler drifts onto the Gate on enemy phase 2, and the archer stops short at 14,6.
4. **The slog lever stays loaded, not pulled.** Throne avoid stays 30. If a hand play reads as a slog under the agreed test (fifty-third round), the first lever is throne avoid 30 to 15, terrain-wide, before reverting the rule.

## Gates, 200 seeds

| | main at bcd1ba1 (from STATE) | with the rule |
|---|---|---|
| Sallow Grange gate 1 | 79 percent, median 9, p90 10, 39 timeouts, 4 captain | 79 percent (157/200), median 9, p90 10, 39 timeouts, 4 captain |
| Sallow Grange gate 4 | ok, 0.375 (Ansgar 0.530, Wren 0.120) | ok, 0.380 (Ansgar 0.530, Wren 0.120) |
| The Tollgate gate 1 | 80 percent, 41 timeouts, 0 captain | 80 percent (159/200), 41 timeouts, 0 captain |
| The Tollgate gate 4 | ok, 0.310 | ok, 0.310 |

The heuristic seldom finds the seated Reeve with nothing to strike, so gate 1 does not move. Only a few benched seeds change.

## Played (Code, seed 41, both placements; journals in PLAYTEST.md)

- **Archer on the Gate** (the old placement, Chat's script replayed): the archer no longer walks off on enemy phase 6, so Chat's turn 7 seize fails at `attack teodor archer-2`. Played on by hand, it cost eight swings over turns 7 and 8 into 30 avoid, five of them misses, with no counter taken. The seize moved from turn 7 to turn 9. A speed bump, as the fifty-first round predicted.
- **Brawler on the Gate** (the shipped sample): the replay diverges on turn 4, because the gap now holds only the soldier. The brawler held the Gate through enemy phases 3 to 8, since it could strike nobody. On turn 9 the forecasts at level 1 were: Pell 10 at 73 from range 2 with no counter (Chat's reading confirmed), Ottilie 5 at 34, captain 8 at 41 taking 6 x2 at 82, Ansgar 8 at 26 taking 8 x2 at 89, Teodor 9 at 26 taking 6 x2 at 91. The holder has 23 hp (20 scaled to level 3). By the agreed test, the survivors' expected damage is about 16.7 a phase, 12.7 net of the 4 hp heal, which kills in two phases. So it is not a slog, by a small margin. **On enemy phase 9 the brawler stepped off the Gate to kill Pell, in both turn 9 lines I played**, including the one with the captain adjacent on 10 hp. The captain walked onto the empty throne on turn 10. The throne cost a body, not a fight.
