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

## Amended by issue 344 (the second arm)

Date: 2026-09-27. Chat filed this from its cold seed 41 play of Sallow Grange with the header. The push that would have seized on turn 7 was refused as `pell cannot shove captain: heft 3 is under its 13`. It was agreed on the Table in the fifty-ninth and sixtieth rounds.

- Heft is checked only when the target is an enemy. An ally consents, so a push on an ally needs only the geometry: orthogonally adjacent, and the tile beyond on the map, passable for it, and empty. `Resolver.ShoveRefusal` returns before the heft check when both units are on the same side.
- A pushed ally fires the enter events of the tile it lands on (`MapEvents.AfterMove` on the target, a no-op for an enemy). This reverses the line above that a shove fires no tile-entered events. A pushed ally takes no keepsake, and on an Escape map a push onto an exit does not remove it; it leaves with its own `exit`.
- Relays stay uncapped and logged. A unit is capped at one push a phase if either play shows a relay before first contact (any enemy attacking or attacked; a wake from noise does not count), or a relay-assisted daylight Sallow seize on turn 5 or earlier.
- The sample is `docs/samples/sallow_grange_shove.map`, the shipped map with `shove: on` after `enemy_level: 3`. `ShoveSallowTests` pins Chat's refused position as a constructed state, with the Reeve's heft refusal as its falsifier.

Code's seed 83 play of the sample (PLAYTEST.md, not cold) won on turn 6 with nobody lost. There were four pushes, all on allies, all after first contact, and each one taken where the pusher also had a strike on the table. That is one play of two toward the kill criterion.

## Kept as ally pushes (issue 355)

Date: 2026-09-27. The sixty-second round on the Design Table (#322), from Chat's cold Brackwater seed 67 play (8/7/7) and Code's reply. Both plays of the second arm are in, and in each one pushes were chosen over legal strikes, so the kill criterion is met and 13.12 is kept.

- The rule is one sentence: a unit may use its action to push an orthogonally adjacent ally one tile straight away from itself, into an open tile. Everything else from the second arm stands: the pushed ally keeps its flags, fires the enter events of its landing tile, takes no keepsake, and leaves an Escape map only by its own `exit`. A shove is still noise at both tiles.
- The enemy half is cut. Neither play pushed an enemy, and twice in Chat's play killing the enemy was better. `Resolver.ShoveRefusal` refuses any enemy target right after the player-side check, as `only allies are shoved`, so `Legal` never offers one. Stagger, the proposed rescue for the enemy half, is dropped untried.
- Heft is cut: `Resolver.Heft`, the `heft N` on a unit's row on a shove map, the heft clause in the map legend and the CLI help, and the heft tests. The protocol never carried it.
- The relay cap and its trigger are retired, not applied. The trigger fired on Chat's free turn-1 relay (Pell and Wren throwing the captain before any contact), which decided nothing. The cap, one push per unit per phase, would have left that relay legal and killed the double throw round the Reeve in Code's seed 83 play, the rule's best turn. A trigger that measures the wrong thing is retired rather than obeyed.
- The header stays on the two samples. Which shipped maps get it is a later keep round.
- `CliPlayTests.TheJournaledShovePlaysReplayWithNoHeft` replays both journaled scripts under `--strict` and checks that no `heft` is printed. The committed transcripts are kept as played, so their unit rows still carry the old `heft N`.
