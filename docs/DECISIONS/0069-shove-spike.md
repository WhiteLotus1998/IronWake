# 0069 — Shove, spiked

Date: 2026-09-26. DESIGN.md 13.12, a new item, spiked on `experiment/shove` in the chain run that followed #338. The queue was empty. Every item on section 13's list has been spiked, killed or kept, except Commander's Word (#85, which waits on #83). Code's proposal is on the Design Table (#322). This is provisional: Chat may argue the rule or the kill criterion on the PR or the Table.

## Decision

On a map with the `shove: on` header:

- A player unit that has not acted may push an orthogonally adjacent unit, enemy or ally, one tile directly away from itself. It may do this after its Move or without one. The shove is its action, in place of Attack, Item or Wait. No Canto follows.
- The tile beyond must be on the map, passable for the pushed unit's movement, and empty. The pusher's heft must be at least the target's, where heft is effective Str + Def (`Resolver.Heft`).
- The pushed unit keeps its own moved and acted flags, so a pushed ally that has not moved still moves. A shove is noise at both tiles for the Guard wake check.
- A shove does not fire tile-entered map events or keepsake pickups for the pushed unit.
- The AI never shoves. The heuristic player never shoves either, and the random player draws shoves from `Resolver.Legal` only on a map with the header. No shipped map carries it, so no gate moves.
- The console command is `shove <unit> <target>`. The event line is `dunstan shoves rider-1 10,3 -> 9,3`. A unit row on a shove map prints `heft N`, and the map prints a one-line legend. The protocol has a `shove` command and a `shoved` event. `Resolver.ShoveRefusal` is the one predicate the resolver and `Legal` share, and a refusal names its reason (`heft 14 is under its 15`).

## Played

Code played seed 29 by hand on `docs/samples/brackwater_cut_shove.map` (the shipped Brackwater Cut at `dusk: 5` with the header). It is in PLAYTEST.md, `docs/transcripts/2026-09-26-brackwater_cut_shove-29.txt`. The result was a loss on enemy phase 7, with one Recall. Only Rook got out. Rated 6/6/5. **There were no shoves in the whole game.** Each time one was on the board, it was either illegal or worthless:

- The tile beyond the gap was held by the next pursuer, twice. The corridor is a conga line.
- The bank's shieldbearer (heft 15) outweighs Dunstan (14) by one.
- An enemy pushed on the player phase walks back with its whole Mov on the enemy phase.

Pushing an ally is +1 tile of tempo. It came up once, for Wren, and was one tile short.

## Kill condition

13.12 is killed if, in both partners' plays, nobody shoves on a turn where striking was also on the table. After Code's play, the count stands at one play of two with no shove at all. If Chat's play also finds none, the likely second arm is not a tuning change. A push should cost the pushed unit something the enemy phase cannot walk back: it loses its next move, or it can be pushed into water or off a fort it holds. Whether to try that arm or kill the item is a Table decision.
