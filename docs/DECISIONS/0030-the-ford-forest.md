# 0030 — The ford forest: Saltmarsh Ford's west crossing opens into forest

Date: 2026-09-25. Issue 131, target 1 (issue 13's Saltmarsh slice). Spec from the Fun Gate entries (eighteenth round) and #131's comments: the forest arm first, the timing arm second, both measured on gate 1 before either is played. Built by Code on what ships after DECISIONS/0029.

## Decision

`3,4` and `4,4`, the two tiles south of the west crossing, become forest (row 4 reads `...^^.........`). The bridge, the one-tile east crossing, the start line and every unit are unchanged.

Why it works: section 8's approach rule breaks ties by terrain avoid, so the brigand, which cannot reach the party on enemy phase 1, stops in the forest at 3,4 and waits, with the soldier behind it on the bridge. The ford group used to walk onto open ground and die on the party's counters; now it holds the crossing mouth, and turns 2 to 4 are a fight with forecasts in them: who takes the other forest tile, whether to strike a forested brigand from open ground at a 51 counter, and whether to let the soldier come out. A test on the map as shipped holds the stop (`OnSaltmarshFordTheBrigandsApproachStopsInTheForestSouthOfTheWestCrossing`) and fails on the old map.

## Measured (200 seeds, `docs/measurements/2026-09-25-full-fordforest-200seeds.txt`)

| Saltmarsh Ford | Gate 1 | Losses | Gate 4 median |
|---|---|---|---|
| Before, two rolls | 33 (17 percent), p90 11 | 154 timeout, 13 captain | 0.065 FAILED |
| Forest at 3,4 and 4,4 (shipped), two rolls | 22 (11 percent), p90 13 | 169 timeout, 9 captain | 0.030 FAILED |
| Forest at 3,4 and 4,4, one roll | 18 (9 percent), p90 15 | 160 timeout, 22 captain | 0.040 FAILED (before: 13 percent, 0.075 ok) |
| Forest at 2,4 and 4,4, two rolls | 22 (11 percent), p90 14 | 160 timeout, 18 captain | 0.045 FAILED |
| Timing arm: the ford group as a sleeping guard, two rolls | 20 (10 percent), p90 12 | 174 timeout, 6 captain | 0.005 FAILED |

Gates 2 and 3 pass on every arm; gates 5 to 8 pass on the shipped map. All three arms read about the same on gate 1, a drop of five to seven wins in 200 (about two standard errors) with the extra losses timeouts. The map's gate 1 was already failing on the heuristic's stall at the boss (DECISIONS/0029, fifteenth round), and the opening now costs the baseline a turn or two of that clock.

## Not taken

- **The timing arm** (the ford group as `guard`, waking with proximity): it turns the group into a pair the party can charge and kill on its own tiles before either moves, so the opening stays free in a different way, and it measured no better.
- **Forest at 2,4 instead of 3,4**: the same gate 1 with more captain deaths; the brigand then stands on the road square beside the forest rather than in it.

## Reading, provisional

The forest is the lean because it gives the opening a decision without adding a unit or a rule, and the one hand play on it (Code, seed 71, PLAYTEST.md) found three turns of choices where the last four entries found none. Under one roll gate 4 goes from passing to failing; two rolls is what ships and was already failing there. The cheap reversal is one row of the map.
