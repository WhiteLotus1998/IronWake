# 0389: the Sim's take of the hill's shard

Date: 2026-10-09. Issue #1386, slice 3b. Builds on 0387 (the shard under the hill) and 0388 (the re-take at half). Planner fidelity only. No rule changes.

## Decided

- **The take.** A Sim unit that hasn't acted takes a lying shard (`take <unit> shard`) from a tile it can end on, either the shard's tile or one beside it, ahead of any attack (`HeuristicPlayer.TakeTile`). The keep's take ends the map, so it is exempt from the veto. This one ends nothing, so it isn't: the unit takes only from a tile whose no-crit exposure stays under its HP, choosing the lowest exposure, then the nearest, then the first. With no such tile it plans as before. This is Code's lean, provisional, and it is a planner choice, not a rule.
- **The walk.** A unit with nothing to strike walks toward the lying shard's tile and the tiles beside it, under the key `Approach` already uses for a running boss.
- **The read**, `--full docs/samples/under_the_hill.map` (200 seeds): gate 1 stays 0/200, with 89 losses and 111 timeouts, before and after. With the change, the shard is broken in 30 of the first 40 traced seeds. The wins are still missing because the sample's placeholder Kin is the stand-in Hask holding his post at level 1. That is 3b' and 3c's board to size, not a planner fault.

## Not in this slice

- 3b': the Kin's own boss (its Frozen Iron on the hill), which needs a placement rule for a boss that starts swallowed.
- 3c: the hill in the campaign, a route to the bearer by turn 2 (0388), Reseal or Fight, and Marrit.
