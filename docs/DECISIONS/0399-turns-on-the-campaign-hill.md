# 0399: `shard_breaks: turns` on the campaign hill; `stills` removed

Date: 2026-10-09. Issue #1481 (#1386 slice 3f). Restates DECISIONS/0395 (Table rounds 556 to 558) as built; the one builder's call is marked.

## Decided

- **`content/hill/under_the_hill.map` carries `shard_breaks: turns`.** The header stays a header rather than becoming the rule wherever `kin_shard:` and `swallowed:` stand (builder's call, which #1481 left open): the earlier hill samples (`under_the_hill.map`, `_kin`, `_campaign` under `docs/samples/`) are the records of 0387 to 0391 and replay their journaled plays only without it, and a map with a shard and a swallowed boss but an untouched frost stays expressible. With the header the hill is the `turns` sample line for line, so Code's warm 1553 (9/7/7) and Chat's cold 1561 (8/8/7) were played on this board.
- **`stills` is gone:** the header value (it now fails to parse, `shard_breaks names 'turns', got 'stills'`), `ShardBreak.Stills`, `Swallow.Stilled`, the step guard in `KinShard.Take`, the `frozenIronStilled` event and its PROTOCOL row, `docs/samples/under_the_hill_stills.map`, its tests and `docs/measurements/under_the_hill_stills-1386.txt`.
- **The `stills` transcripts stay as history** (`docs/transcripts/2026-10-09-under_the_hill_stills-1553.*` and `-1561.*`), as killed boards' plays have before (0075, 0208). They no longer replay: the file they were played on is `docs/samples/under_the_hill_stills.map` at 09fd59a.
- **Code 1552 re-journaled** on the campaign hill (`docs/transcripts/2026-10-09-under_the_hill-1552.txt`): the same 71 commands, the same 7 rejections, the same loss on turn 5. The frost now names Hask: he takes the turn-3 landing of 3 (to 17) and heals to 19, and the swallow and shard rows say the frost lands on him. The transcript also picks up #1446's `again` wording (0397), which it had missed because no test replays it.
- **`--finale content/hill/under_the_hill.map --level 8`:** 185/146/44 (full/depleted/floor), identical to the `turns` sample's 0393 reading but for timings (`docs/measurements/under_the_hill-1481.txt`). Against 0391's header-less hill, 164/122/17.

## Not changed

The Kin's stage HP (20), `race` keeping the limit off, the keep. 514's exemption on the hill is with Lotus (#1482); this is the lean built meanwhile.
