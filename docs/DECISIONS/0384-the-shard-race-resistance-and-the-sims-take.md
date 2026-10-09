# 0384: the shard race's resistance (`race <k>`), the inner tile at the keep's heart, and the Sim's take

Date: 2026-10-09. Issue #1386, slice 2a. Lotus's race (relayed 2026-10-08: forgiving, about five turns, "a straight walk takes 3, the fight makes it 4", a held guard on the one lane and a sworn wave on the countdown's second phase), Chat's shape (round 487), slice 1 (0382). Chat's round 542: the race's engine slices proceed; no race number is tuned against the art-less Sim alone.

## Decided

- **A new event trigger, `race <k>`** (`RaceTrigger`): fires k of the runner's side's phases into a shard race, `race 0` with the run itself (after `ShardRaceBegan`), `race k` at the countdown's k-th tick, after it. k is 0 to the race's phases less one; a map without `shard_race:` refuses it. Its spawns may land off the edge, as `falls` spawns do (the shard calls sworn already inside). The objective lines announce it ("if a beaten boss runs with the shard, 2 of his phases into the run: ..."); on a map with no race it never fires.
- **The sample's inner tile moves from his hold (0,6) to the keep's heart (15,6)**, behind the gate where the company started, so a fall by his hold leaves the company a walk. Resistance on the sample: `race 0` a soldier holding the gate lane at 11,6; `race 2` a brigand at 15,0 and a soldier at 15,11. These are placeholders for the tuning pass, not tuned numbers.
- **Slice 1's board is kept** as `docs/samples/ironwake_keep_shard_race_1424.map`, where its replay (`2026-10-09-ironwake_keep_shard_race-1424`) still plays.
- **The Sim's take** (`HeuristicPlayer.TakeTile`): a unit that has not acted and can end beside a runner takes the shard, ahead of every other plan. Runners leave the Sim's target list (the resolver refuses an attack on one). A unit with nothing to strike walks toward the tiles beside the runner, by the approach's key (the veto still covers the captain). Nothing changes on a board with no runner.
- **`--finale` prints `shard race:`** (`ShardRaceRead`, `FinaleRun.ShardLine`): races run, taken by phases left, run out, ended while he ran, and the company's falls in the run.

## Read (`docs/measurements/keep-1386-race.txt`; baseline only, art-less)

- Slice 1's board: the take is free (164 of 164 full at 5 or 4 left). Inner tile only: a walk, a median 2 left, 2 of 164 run out; 162 / 126 / 9. Guarded (as shipped): a fight, 33 of 164 full and 39 of 127 depleted run out, units fall in the run; 132 / 91 / 4. Lotus's "forgiving, the fight makes it 4" sits between the two. Which, and where the guard and the wave go, is the tuning pass, after #1453 and against a warm and a cold chair.
- Code's warm hand play (seed 1424, guarded): Rook flew over the held guard and took the shard on turn 11 with 4 left; the wave never came. A flier makes the race short; that is a tuning question, not a fault.

## Next

- Slice 2b: the campaign's flag from Under the Hill's four conditions, the coma in `ending.json`.
- The race's tuning (board, guard, wave, phases) after #1453's read, with a cold chair.
- Slice 3: under the hill.
