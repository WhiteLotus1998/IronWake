# 0292 — The Sim's planner leaves a corked captain its no-counter tile

Date: 2026-10-06. Issue #1206 (split from #1198, round 406). Implementation of the issue's lean; Sim player only, no rule changes.

## Context

On the First Shrine the door soldier at 7,1 corks the altar; 7,3 is the one tile that strikes it without a counter. The planner, in id order, let whoever planned first take 7,3, so the second attacker had nowhere to strike from and the yard woke on it. Both partners' hand plays find the line at once: the ally takes the counter, the captain finishes from the safe tile.

## Decision

- **Corked:** a Seize map whose throne the living captain has no open path to (`HeuristicPlayer.Corked`, the same test `Approach` uses to turn a Seize approach into a Rout one).
- **Order:** when corked and the captain, not yet acted, has a tile in reach that strikes some enemy without a counter (`SafeStrikeTiles`), the captain plans last.
- **The captain's tiles:** a recruit never strikes from, and never approaches onto, one of those tiles while the captain has not acted (`CaptainsTiles`). The issue's "or not at all": a recruit whose only attack tile is the captain's walks instead.
- The issue's first form (the captain last whenever corked, a recruit allowed onto the tile when it has no other) was measured first and dropped: Maud and Pell fell from 24 to 1 of 200, since the turn-1 order swap put Pell ahead and he took 7,3 himself.
- Guards (`HeuristicCorkTests`): the order on a corked Seize and its absence on a Rout of the same corridor; the recruit strikes in melee and the captain then finishes from range 2; a bow recruit neither strikes from nor stands on the captain's tile on Seize, and does strike from it on the Rout.

## Measured (`--full`, 200 seeds)

| Board | Before | After |
|---|---|---|
| the_first_shrine, bare cast | 0 | 0 |
| Shrine, Maud leading: Pell / Wren / Teodor / Ottilie / Dunstan / Brannock | 24 / 5 / 6 / 4 / 0 / 0 | 40 / 5 / 7 / 4 / 0 / 0 |
| the_tollgate | 167 | 169 (gate 4 ok, 0.300) |
| sallow_grange | 161 | 161 |
| the_cold_kitchen | 74 | 75 |
| the_undercroft | 2 | 1 |
| the_chapter_roll | 0 | 0 |

The rule only fires on Seize, so Rout, Defeat Boss, Escape and Survive maps play as before; the Seize maps above are the ones whose cells it can move. The full-campaign parity script's variant 30 now loses map 6 on seed 644; variant 44, which keeps the side map and the spent purse, wins and is the committed script.

## Next

The Shrine's Sim number is in (#1198's lever 1 is measured): Maud and Pell 40/200, still 144 timeouts. The planner still walks Pell into the woken yard after the strike; the Shrine's tuning rests on the hand plays (Code 1530, Chat 2240), not this number.
