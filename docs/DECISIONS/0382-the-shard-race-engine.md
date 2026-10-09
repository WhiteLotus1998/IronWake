# 0382: the shard race at the keep, engine slice (the secret path's flag, the run, the countdown, `take`, the coma)

Date: 2026-10-09. Issue #1386, slice 1. Lotus's rulings relayed on 2026-10-08 (the secret path skips stage 2; the company takes and breaks the shard; Hask lives in a coma; a forgiving race of about five turns, gated by a warm and a cold chair and the Sim), shaped by Chat in round 487 (the move-back to the inner tile, `swallows in 5`, take as one action, the resistance). Builds on 0362 (the swallow).

## Decided

- **The flag is a map header for now:** `shard_race: <inner x,y> <phases>`, Defeat Boss only, the inner tile passable, 1 to 9 phases. The campaign setting it from Under the Hill's four conditions is slice 2; no record carries them yet.
- **Every route to the swallow already ends in `Swallow.Take`**, so the race is one branch there (`ShardRun`): on a race map the first fall runs him to the inner tile (or the free tile nearest it) on 1 HP, holding, with the countdown set.
- **While he runs** he cannot be attacked (the refusal names `take`), the planner gives him `Wait`, `StrikeOn` and the line-strike read skip him, he starts his phases moved and acted, and a stray hit to 0 leaves him on 1. The turn limit ends nothing while he runs (`Racing`), the same rule as stage 2's race: a hidden-condition path is never lost to a clock the player could not see coming. Lean, for Chat to argue.
- **The countdown** ticks at his side's phase start after Frozen Iron's slot; at 0 he swallows there (the normal stage 2, first landing at his next phase start).
- **`take <unit> <boss>`** (`TakeShard`, protocol `takeShard`): a company unit orthogonally beside him, not yet acted, as its action; no Canto; events `shardRaceBegan`, `shardCountdown`, `shardBroken`. He leaves the board alive and joins `BattleState.Coma`; not a kill, so Defeat Boss is won.
- **Text is placeholder** (the run, the break, the coma line), waiting on Lotus's story pass; his waking line is his.

## Found in the hand play (seed 1424, warm)

- On the Warden sample the inner tile is his hold, 0,6, and the fall happens beside it, so the move-back is no move at all. With every wave already dead, the race was one enemy phase of nothing: the captain took it on turn 11. Tension came only from not having an unspent unit within reach on the falling turn. Slice 2 needs both of Chat's pieces: an inner tile the company is not already standing on, and resistance on the way.

## Next

- Slice 2: the campaign flag from the four conditions, the coma in `ending.json`, the resistance (a held guard on the lane, a wave on the countdown's second phase), the inner tile's placement, the Sim's `take`, and the gate (a warm and a cold chair, the Sim).
- Slice 3: under the hill (the Kin's Frozen Iron, the sworn bearer, the same verb, the re-take), on the hill map.
