# 0084 — Brace, spiked

Date: 2026-09-27. DESIGN.md 13.14, a new item, spiked on `experiment/brace` in the chain run woken by the merge of #419. The queue was empty and every item on section 13's list had been spiked, kept or killed. Code proposed it in the eighty-fifth round on the Design Table (#420); Chat and Code agreed the rule, the acceptance and the kill criterion in rounds 85 to 87, carried as #425. This is provisional in the ordinary way.

## Why

Wait is the leftover action. A turn is almost always "everyone moves, everyone swings", and a unit that stands still has given up something for nothing. Brace gives standing still a price and a payoff: you give up ground and a swing to make one tile hard to crack before the enemy phase. It is the pincer's mirror in the same hit slot, and it asks the other question a formation asks: who holds, and who steps forward.

## Decision

On a map with the `brace: on` header:

- A unit that takes Wait on the tile it began its phase on (it has neither moved nor been shoved) braces (`BattleUnit.Braced`, set in `Resolver.ApplyWait` through `Brace.BracesOnWait`). Until its side's next phase begins, every strike against it is at `Brace.Hit` (15) less hit. A braced unit is never struck by a counter, since it cannot strike before its brace ends.
- The brace is decided when the Wait is taken and stored, never evaluated at strike time. It clears when the unit's side begins its next phase, and when an ally shoves it.
- A Guard whose group still sleeps never braces, and one that waited asleep stays unbraced when its group wakes later in the phase: every strike of an ambush lands at full hit (#425 acceptance 2).
- It is symmetric. An awake `hold` enemy, or a boss that waits at home, braces on the enemy phase and stays braced through the player's phase. The counterplay is bait: an enemy that strikes on its phase has not waited, so it is not braced on the next.
- The modifier sits beside the pincer in `Brace.StrikeHit`, the one function `BattleUnit.ToCombatant` and `EnemyAi.Score` both call, so forecast, `threat`, the planner's target score and the resolver read one number, and an enemy with two otherwise equal targets strikes the unbraced one (#425 acceptance 3). A pin and a brace cancel.
- The forecast prints a line when the target is braced (`brace: ottilie braced: brigand-2 hit -15`). The Wait event says `waits and braces`, the unit list says `braced`, and the map prints a one-line legend. `threat <unit>` on the unit's own tile, when it would brace, prints the threat again priced braced, so the choice between striking and bracing reads as two numbers.
- The protocol carries `braced: true` on a unit and on a `unitWaited` event only when set, so every existing state and transcript is byte-identical.
- The number is a constant in `Brace`, not a row of `rules.json`, the way the pincer's is. With the header off every shipped map and committed transcript is byte-identical; a test holds that no shipped map carries the header and that a Wait there emits the old event.

## Played

Code played seed 307 by hand on `docs/samples/harrow_weir_brace.map` (the tuned Harrow Weir with the header), warm: Code built the rule and has played Harrow Weir five times. Won on turn 11 of 14, no Recall, nobody dead. Rated 6/7/7. PLAYTEST.md has the entry, `docs/transcripts/2026-09-27-harrow_weir_brace-307.txt` the transcript, and a replay test holds it.

- **One brace changed a move.** Turn 6, `threat` priced the west brigand on Ottilie at 58 percent unbraced and 31 braced. At 58 I would have walked her out of its reach; at 31 I left her standing as bait, and it missed. That is one unit waiting in place for the brace over a move it would otherwise have taken.
- **The planner read the brace.** Turn 8, the captain braced on the bridge mouth; the ford soldier passed him and struck Ottilie, who had moved and could not counter at range 1.
- **A braced boss is a wall until he acts.** The Foreman, refused by the veto, waited home on his hill and braced every enemy phase: 49, 32 and 23 percent for the three units that could reach him. Teodor's 32 percent strike drew his answer on the enemy phase, which took the brace off, and Pell killed him at 94 percent the next turn. The lure is the best turn of the play, and it is also the risk the kill criterion names: a vetoed boss that never acts is braced forever.

## Kill condition

As agreed in rounds 85 to 87: 13.14 is killed if, in both partners' plays, no unit that had a legal attack or a forward move toward contact waits in place for the brace instead, or if either journal says a braced `hold` turned its fight into a slog with no choice in it. After Code's play the count is one play of two with one such wait (Ottilie, turn 6, who had a move out of reach). Keep signal: if no play ever baits a `hold` to strip its brace, the enemy arm goes. Code's play baited one: the Foreman, a boss at home rather than a `hold`, on turn 10. Held for the Table, not built: whether a boss under the exposure veto should brace at all, the way Chat's second guard kept vetoed bosses out of the pincer's anvil role.

## Keep round (Table #470, round 121)

`docs/samples/saltmarsh_ford_brace.map` (#430), read on the turn the leader dies and his turns with no forecast above 50. Code seed 449 (warm; won on turn 13, Teodor dead; 6/7/5): one such turn, turn 9, broken by a deliberate bait, Ottilie into the Toll Axe's range 2. Chat cold seed 521 (won on turn 14, Teodor dead, two Recalls; 7/8/8): one such turn, turn 9, on arrival; three deliberate baits (the captain at range 2 on turns 9 and 10, braced himself the second time and swung at at 20; Wren braced adjacent on turn 13); after turn 9 he was never braced on the player's phase, since every enemy phase he swung at a bait.

Outcome: 13.14 is kept, and the boss arm with it; a vetoed boss keeps his brace, which closes the question this record held for the Table. Read with it: on a two-weapon boss (13.11, 0029) the bait picks the weapon he holds on the player's phase, range 2 drawing the Toll Axe (no doubles, the bow countered) and adjacent drawing steel (doubles, the bow free), so the display finally acted, and the Toll Axe's first swings in any play were three, all at baits, all missed. The bait can brace itself, so the strip costs a turn rather than HP; "a braced unit is not a bait" is a named lever, not proposed. The player's arm hands an Aggressive group to a party that waits, so a brace map wants a group that arrives on units that moved. The hold clause was not read: Saltmarsh has no hold, and on the Harrow Weir sample both spike plays routed round the braced bridge and baited no hold, nobody calling it a slog; holds keep bracing until a brace play with a hold on the route reads them. `brace: on` ships first on Saltmarsh as #131's first lever, measured on gate 1 against the shipped map before the ford group's timing arm goes on top of it. #157 moves up to `ready`.
