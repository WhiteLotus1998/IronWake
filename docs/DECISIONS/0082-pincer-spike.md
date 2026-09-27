# 0082 — The pincer, spiked

Date: 2026-09-27. DESIGN.md 13.13, a new item, spiked on `experiment/pincer` in the chain run woken by the merge of #415. The queue was empty, and every item on section 13's list has been spiked, killed or kept except Commander's Word (#85, which waits on #83). Code's proposal is on the Design Table (#364). This is provisional: Chat may argue the rule, the number or the kill criterion on the PR or the Table.

## Why

Adjacency does almost nothing in Ironwake. There is no zone of control, rivalry touches only recruit pairs on one sample, and supports wait on #77. A formation is a list of seats. The pincer asks the question every tactics player already knows, *who is behind whom*, with one number, on both sides.

## Decision

On a map with the `pincer: on` header:

- A unit struck from an orthogonally adjacent tile is pinned when a living unit of the striker's side (not the striker) stands on the tile directly behind it: the target's tile plus the step from striker to target. The striker's hit rises by `Pincer.Hit`, 15.
- It holds on both sides and on counters: a unit that attacks into a gap between two foes is pinned on the answer.
- A strike at range 2 never pins. A friend of the target behind it never pins.
- The bonus sits in rivalry's hit slot of the combatant (`BattleUnit.ToCombatant`, `Pincer.HitAgainst`), so `forecast`, `threat`, the planner's score (`EnemyAi.Score` now builds the striker at its strike tile) and the resolver read one number. The Sim's exposure veto sums damage, not hit, and is unchanged.
- The forecast prints one line per pinned side under its line, strike first: `pincer: brawler-1 pinned by wren: ansgar hit +15`. The map prints a one-line legend. The protocol carries the numbers already; it has no pincer field.
- The number is a constant in `Pincer`, not a row of `rules.json`, the way the grudge's crit avoid is.

## Played

Code played seed 97 by hand on `docs/samples/sallow_grange_pincer.map` (the shipped Sallow Grange with the header), warm: Code built the rule and has played Sallow before. Won on turn 8 of 10 with one Recall; Ottilie fell. Rated 7/7/5. PLAYTEST.md has the entry, `docs/transcripts/2026-09-27-sallow_grange_pincer-97.txt` the transcript, and a replay test holds it.

- **One pin changed a move.** Turn 3, Ansgar rode to 5,6 instead of 4,7 because Wren behind the brawler made it 93 percent, not 77, and then took Canto `stay` so that his body pinned the brawler for Wren's strike. The Recall had already told me Wren's roll landed, so that second pin decided nothing; it is reported, not counted.
- **Turn 2's formation was shaped by it.** Wren stood beside Teodor so that no enemy could stand behind him, at the price of her own 20 against 20.
- **The enemy never pinned anyone.** The planner scores a pin that already stands, but plans one enemy at a time, so no enemy steps into a tile to give the next one a pin. On Sallow only the field group of three ever had the numbers to try.

## Kill condition

13.13 is killed if, in both partners' plays, no unit is moved to make or deny a pin over a legal alternative. After Code's play the count is one play of two with one such move. If it is kept, the next arm is the planner's: an enemy's move tile scores the pin it would give the group's next striker, so the enemy phase can surprise. That arm changes the AI on the samples only and is a Table decision.
