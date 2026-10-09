# 0387: the shard under the hill (the bearer, the drop, the take, the pickup, the re-take)

Date: 2026-10-09. Issue #1386, slice 3a. Builds on 0382 (the keep's take), 0385 (the secret path's flag). STORY's Under the Hill, Fight (Chat's lean, round 487, in the issue body): the Kin's shard is carried by a sworn, dropped on his fall, taken with the same verb, picked up by the nearest sworn if nobody takes it by the next enemy phase, and while whole the Kin re-takes one sworn a turn. Engine only, on a sample.

## Decided

- **`kin_shard: x,y`** names the enemy placement that carries the shard, as `freed:` names its bound enemy: neither a boss nor the messenger. `BattleState.Shard` is null until the first bearer leaves the board, then says who carries it, where it lies, or that it is broken (`ShardHold`). Canonical text and the protocol state (`shard`) carry it, so Recall and a suspend restore it.
- **The drop** is found after every accepted command (`KinShard.After`, beside `Freed.After`): the bearer gone from the board by any way drops it on the tile he fell on (`shardDropped`). The six death sites stay untouched.
- **The take is the keep's verb:** `take <unit> shard`, `TakeShard` naming `shard` in place of a boss. A company unit on the tile or orthogonally beside it, not yet acted, after its Move or without one; no Canto after (`kinShardBroken`). A carried shard is refused with "it falls where he falls".
- **The pickup** happens at an enemy phase start: the nearest sworn (not a boss, not a Hollow) whose Move reaches the tile, then the lowest id, walks onto it and spends that phase holding it, moved and acted (`shardPicked`). Under a company unit or out of every reach, it lies another phase.
- **The re-take (Code's lean, provisional until the Table or Lotus says otherwise):** at each enemy phase start while the shard is whole, carried or lying, the oldest enemy body that was neither a boss nor a Hollow stands again where it fell, or on the free tile nearest, on full HP under its own id, moved and acted, so it acts from the next enemy phase (`swornRetaken`). "Re-takes one sworn" read as the oath taking back the fallen: it keeps the Kin fighting only through the sworn, and it makes breaking the shard the objective it is meant to be. Kill EXP repeats on a re-taken sworn; on the last map that is cheap.
- **The sample** is `docs/samples/under_the_hill.map`: a 14x9 hall, six sworn at level 1, the bearer a Sworn Captain at 4,4, the stand-in `hask` as a held placeholder boss for the Kin. A first cut at level 2 with eight sworn wiped the company by turn 4: the re-take snowballs on a board the company cannot out-trade.

## Not decided here

- The Kin as a boss (its Frozen Iron, its wave, tiles still chained), the hill as the campaign's map after the keep with the Reseal/Fight card, the Sim's take of a lying shard, and whether the keep's take frees Marrit.
- Whether the re-take should cost the Kin something or skip a phase; the warm play below says it bites hard enough at one a phase.

## Next

- Slice 3b: the Kin's own boss (its Frozen Iron on the same clock, then the wave), and the Sim's take of a lying shard.
