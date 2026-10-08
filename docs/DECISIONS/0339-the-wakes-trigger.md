# 0339 — The `wakes` trigger; Saltmarsh's pair on the fort's wake stays on samples

Date: 2026-10-08. Issue #1365; Design Table #1334, rounds 468 (Code), 469 (Chat) and 470 (Code). Provisional.

## Context

Every Saltmarsh read since 0093 (547, 571, 1560, 3200) has the pair arriving after the leader is dead, so turns 8 to 12 are a cool-down. Chat traced it in round 469: 0090 coupled the pair to the fort's wake, then 0091's brace tiles and 0093's north cut decoupled it, and the cut's "when to call" was one answer played four times. Both partners agreed lever 1: spawn the pair on the fort group's wake through a real trigger, not enter tiles. The gate 1 floor is 0095's 40/200, and the agreed fallback is a spawn one phase late, not dropping the lever.

## Decision

- Map events take a `wakes <group> [late]` trigger (`WakesTrigger`). Without `late` it fires in the resolution that wakes any member of the group, after the `GroupWoke` event (`MapEvents.AfterWake`, called at the end of `Resolver.WakeGroups`, which runs after every accepted command). With `late` it fires at the start of the first player phase after the wake (`MapEvents.AtPhaseStart`). A group with no guard among the placements is refused on load, naming the line. The console's event line reads `when fort wakes` or `at the start of your next phase after fort wakes`. DESIGN 10 says so.
- **The shipped Saltmarsh is unchanged.** Both shapes fall under the floor:

| File | Gate 1 | Losses (timeout / captain) | Gate 4 median |
|---|---|---|---|
| shipped (north cut) | 50/200 | 133 / 17 | 0.075 |
| `docs/samples/saltmarsh_ford_wakes.map` | 36/200 | 153 / 11 | -0.005 |
| `docs/samples/saltmarsh_ford_wakes_late.map` | 39/200 | 147 / 14 | 0.020 |

  Measurements are in `docs/measurements/saltmarsh-wakes-1365.txt`. The late shape is one win short of the floor, which is inside the noise, but the floor is the line both partners set in advance, so it is not shipped on a reading of the noise.
- Both shapes are kept as samples, identical to the shipped file except for the pair's two event lines, which a test holds.

## Open (for the Table)

The losses are almost all on the clock (147 of 161 on the late sample), not deaths. Two ways forward, one lever at a time: a chair reads the late sample at the floor and, if the overlap is the map's best turn, the Table weighs one more turn on `turn_limit` as a second lever, measured alone; or the cut stays and the row says the pair is the cool-down. Shipping the change would also rewrite the full-campaign script and about twenty replays (24 tests failed on the trial build), which is expected work once a shape passes.
