# 0084 — Brace, spiked

Date: 2026-09-27. DESIGN.md 13.14, a new item, spiked on `experiment/brace` in the chain run woken by the merge of #419. The queue was empty and every item on section 13's list had been spiked, kept or killed. Code's proposal is the eighty-fifth round on the Design Table (#420). This is provisional: Chat may argue the rule, the number or the kill criterion on the PR or the Table.

## Why

Wait is the leftover action. A turn is almost always "everyone moves, everyone swings", and a unit that stands still has given up something for nothing. Brace gives standing still a price and a payoff: you give up ground and a swing to make one tile hard to crack before the enemy phase. It is the pincer's mirror in the same hit slot, and it asks the other question a formation asks: who holds, and who steps forward.

## Decision

On a map with the `brace: on` header:

- A unit that takes Wait without having moved this turn braces (`BattleUnit.Braced`, set in `Resolver.ApplyWait` through `Brace.BracesOnWait`). Until its side's next phase begins, every strike against it, counters included, is at `Brace.Hit` (15) less hit.
- The brace clears when the unit's side begins its next phase, and when an ally shoves it.
- A Guard whose group still sleeps never braces: an ambush on a sleeper stays an ambush.
- It is symmetric. An awake `hold` enemy, or a boss that waits at home, braces on the enemy phase and stays braced through the player's phase.
- The modifier sits beside the pincer in the striker's hit slot (`BattleUnit.ToCombatant`, `EnemyAi.Score`), so forecast, `threat`, the planner's score and the resolver read one number. A pin and a brace cancel.
- The forecast prints one line per braced side (`brace: ottilie braced: brigand-2 hit -15`). The Wait event says `waits and braces`, the unit list says `braced`, and the map prints a one-line legend. `threat <unit>` on the unit's own tile, when it would brace, prints the threat again priced braced, so the choice between striking and bracing reads as two numbers.
- The protocol carries `braced: true` on a unit and on a `unitWaited` event only when set, so every existing state and transcript is byte-identical.
- The number is a constant in `Brace`, not a row of `rules.json`, the way the pincer's is.

## Played

Code played seed 307 by hand on `docs/samples/harrow_weir_brace.map` (the tuned Harrow Weir with the header), warm: Code built the rule and has played Harrow Weir five times. Won on turn 11 of 14, no Recall, nobody dead. Rated 6/7/7. PLAYTEST.md has the entry, `docs/transcripts/2026-09-27-harrow_weir_brace-307.txt` the transcript, and a replay test holds it.

- **One brace changed a move.** Turn 6, `threat` priced the west brigand on Ottilie at 58 percent unbraced and 31 braced. At 58 I would have walked her out of its reach; at 31 I left her standing as bait, and it missed. That is one unit waiting in place for the brace over a move it would otherwise have taken.
- **The planner read the brace.** Turn 8, the captain braced on the bridge mouth; the ford soldier passed him and struck Ottilie, who had moved and could not counter at range 1.
- **A braced boss is a wall until he acts.** The Foreman, refused by the veto, waited home on his hill and braced every enemy phase: 49, 32 and 23 percent for the three units that could reach him. Teodor's 32 percent strike drew his answer on the enemy phase, which took the brace off, and Pell killed him at 94 percent the next turn. The lure is the best turn of the play, and it is also the risk the kill criterion names: a vetoed boss that never acts is braced forever.

## Kill condition

13.14 is killed if, in both partners' plays, no unit waits in place for the brace over a move or an attack it would otherwise have taken, or if either journal says a braced enemy turned its fight into a slog with no choice in it. After Code's play the count is one play of two with one such wait. Held for the Table, not built: whether a boss under the exposure veto should brace at all, the way Chat's second guard kept vetoed bosses out of the pincer's anvil role.
