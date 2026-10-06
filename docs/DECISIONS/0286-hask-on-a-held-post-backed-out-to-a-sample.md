# 0286 — Hask on a held post: built, measured, backed out of the keep to a sample

Date: 2026-10-06. Issue #1204, lever 3 of 3; Design Table #1187 (Chat's cold 2210, round 407). Provisional.

## Context

On the campaign keep Hask arrives on turn 6 as `spawn boss hask 0,6 group:lord behavior:boss`. A Boss behavior never moves, so he struck only from his own tile at lance range 1. In Chat's cold 2210 Teodor stood at distance 5 and then at distance 2, `threat` was silent both times, and the strike team staged for free: "a boss acting only in his reach is scenery". Chat's lean was to give him 0279's guard reach: strike anything within his Move plus 1, then walk back to his post. The issue's kill criterion was to read `--finale` after the lever and, if the depleted roster drops under gate 1's 60, soften or back the lever out.

## Built

- **An arriving boss may be Aggressive.** `spawn boss ... behavior:aggressive` is accepted, because a boss who arrives has no sleep to wake from. A placed `B` line stays `boss` or `guard`, and an arriving boss is never Hold. Guard: `OnlyAnArrivingBossMayBeAggressive`.
- **`holds:` counts spawned members.** The group may be placed or spawned, and every spawn tile must lie on the ground. It needs a Guard or Aggressive member, because a group of Hold or Boss members never moves to hold anything. Guards: `ASpawnedMemberCountsAndMustSpawnOnItsGround`, `AGroupWithNoMemberThatMovesIsRefused`.
- **A one-tile ground is a post.** The board prints it as `holds: the lord group leaves 0,6 only to strike, then goes back`.
- Together these give Hask the reach as `behavior:aggressive` plus `holds: lord 0,6 0,6`, with no new planner code. Because he is Aggressive, the boss veto (0077, 389) binds him, and `threat`, `end` and the exposure sum read the same `End` and `Choose` they read for any held member. Test: `ASpawnedBossOnAOneTilePostStrikesInReachAndWalksBack`.

## Measured (`docs/measurements/keep-1204-lever3.txt`)

`--finale --seeds 200 --gates`, level 8:

| Roster | Lever 2 (main) | Hask held, Aggressive | Hask held, Guard | Guard, limit 13 |
|---|---|---|---|---|
| Full | 174 (87) | 169 (84) | 170 (85) | 170 (85) |
| Depleted | 140 (70) | **117 (58)** | **117 (58)** | 120 (60) |
| Gate 4 drop | 0.070 | 0.150 | 0.155 | 0.140 |

On the depleted roster, timeouts went from 11 to 25 and losses from 46 to 58. The Sim's party can no longer stage outside his reach, so he is harder to reach and kill, and the units that step into his reach are struck.

## Decision

- **Backed out of the campaign keep, kept on a sample.** The depleted roster lands at 58, under the issue's floor of 60. Two softenings were tried. The first made him a sleeping Guard, woken within 4 or by noise, on the same post: it read the same 58. The second added a 13th turn to that, which reached exactly 60, but it moves the keep's clock, and that is not this lever's to move. So the campaign keep and its finale sample keep `behavior:boss`, and nothing a player meets changes.
- `docs/samples/ironwake_keep_hask_holds.map` is the campaign keep with only Hask's line and the `holds:` header changed, and a content test pins that. Play it with `play docs/samples/ironwake_keep_hask_holds.map --level 8`.
- **The engine pieces stay.** They are small, tested and inert on every shipped map.

## For the Table

The Sim's player is a lower bound on the keep (0277), and Chat's cold chair won the old keep with nobody lost. So 58 may describe the Sim more than the map. Three ways forward: (a) ship the lever with a softer Hask, which costs HP or Def under the content tests that hold the finale lord at level 8; (b) ship it with a 13th turn; (c) leave Hask as scenery, and let the fresh keep chair (owed after lever 3) decide whether his tile is still free once levers 1 and 2 have landed. Code's lean is (c), with that chair's partner also playing the sample, because a choice between (a) and (b) is better made after a hand play.
