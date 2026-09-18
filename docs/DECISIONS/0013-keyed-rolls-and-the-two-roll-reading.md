# 0013 — Keyed rolls: the hash, and the reading of the two-roll hit

Date: 2026-09-18. Ruled by: Code, while building issues 5 and 31. Provisional where marked.

## The hash is written out, and pinned
Issue 31 asks for a counter-based draw: a roll is a function of the seed and a key. Any hash would do; `string.GetHashCode` would not, since .NET randomizes it per process and gate 6's byte-identical replay would fail across runs.
Ruling: `KeyedRng` computes FNV-1a over the key's UTF-8 text seeded with the campaign seed, then the SplitMix64 finalizer, then modulo 100. The bias of a 64-bit modulo 100 is one part in 10^17 and is ignored. Two rolls are pinned in `KeyedRngTests`, so changing the hash fails a test by name: it changes every seed's game and is a decision, not a refactor.

## Keys are text
A key is its canonical text, `combat/3/Player/wren/brigand/0/HitA`, `growth/wren/7/Spd`, built by factories on `RollKey` so no roll site composes a key by hand. Unit ids are the content's ids, which cannot contain `/`. The `strikeIndex` counts the striker's own strikes in that combat from 0, so an attacker's double is its strike 1 and the counter is the defender's strike 0.

## The two-roll hit is read as "floor of the average below the chance"
Section 5 said "the average of two rolls" and gave 87.75 for a raw 75 and 18.3 for a raw 30 without saying how the average is compared. Reading a hit as `floor((A + B) / 2) < HitChance` gives exactly those two numbers (8775 and 1830 of the 10000 ordered pairs), so that is the reading, and `Combat.HitProbability` computes it in closed form rather than by simulation. A consequence worth knowing at the Table: a raw 50 lands 50.5 percent of the time and displays as 51, and the scheme's curve is not symmetric about 50. The forecast rounds halves away from zero (87.75 shows 88).

## The crit roll is drawn only on a landed hit
Under keyed rolls a draw costs nothing and the crit roll could be drawn every strike. It is drawn only when the hit landed, as section 5 already said, so the key contract test lists exactly the rolls a strike consumed and a reader of the event stream can tell a miss from a non-crit by the rolls alone. Provisional; if a consumer ever needs the crit roll of a miss, draw it always and amend this.

## What waits for issue 6
Issue 31's three Recall tests (identical command after a Recall gives the identical strike list; a different command then the original still gives the original's rolls; a level-up is the same whether or not another unit acted) need a `BattleState` with a history and a level-up, which are issues 6 and 8. This PR holds what they rest on: the same key returns the same roll whatever was drawn before it, shown at the resolver level. Issue 6's acceptance names them.
