# 0208 — The Rookery, Rook's quest 2 (#635 slice 14)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (the pick's quest 2 after map 9), the side-map shape, and STORY draft 6 (Rook's quest 2 is "Kestrow or the company", where she takes the drake and her own name off the church's ledger, and it opens the drake's third stage, Unbroken). The board and what it pays now are Code's lean (provisional); Chat can argue them on the PR.

## What is built

- `content/quests/the_rookery.map`: escape, 15x10, limit 9, Recall 2, enemy level 6, `announce: on`, exits 14,4 to 14,6. Rook holds the captain slot at 1,4 inside Kestrow's walled rookery (a fort at 2,3, one door at 3,5); the ally's bare slot is 2,5. A ravine of water down columns 7 and 8 is crossed on foot only by the road bridge at 7,5 and 8,5. A sleeping guard soldier holds the bridge's far end; a sleeping Wing Captain (steel lance, Mov 7) sits on the loft's mountain at 5,1, six tiles from the bridge, so the first fight there wakes him by noise. A held archer on the fort at 11,3 and a held sentry (steel lance, iron bow) at 12,6 cover the pass. Announced pursuers out of the rookery behind: wingriders on turns 2 and 3, a rider on turn 4.
- **The twist is the exit rule.** Rook's exit wins and leaves anyone on the board as fallen (0056), so the fastest piece on the map leaves last. She can stand on an exit by turn 2; the ally needs four turns on foot. Her Def 1 under lances and bows (a bow crit grounds her, #723) is the price of covering the walk.
- `campaign.json`'s `rook_2`: part 2, 2 common material, no `pays`, no `rare`. It opens two maps after `rook_1` is won (`QuestOpensAt`), so only a Rook run that won The Chapter Roll sees it. Appended last in `quests`, so no other side map's seed moves (0198). Cards are placeholders with the rules line until the writing pass (#811).
- **What it pays (lean).** Rook's signature is the drake, not an item, so there is no weapon and no frozen iron (the forge's rare rule is for refinable signatures). The win is the key for the drake's third stage: #805 reads `rook_2` in `WonQuestIds`, the way 0167's `unlockedBy` already does, so no new record field is needed. Until #805 lands the win puts only material on the board.

## Measured and played

- Sim, 200 seeds, `--lead rook --lead wren`, level-1 cast: gate 1 72/200, all 72 with the captain alone (the heuristic leaves the ally). As on the other side maps, the gate is a cold chair (round 194).
- Code's warm play (side-map seed 1132, level 7, ally Wren): won on turn 9, no Recall, nobody fell, 8/6/6. The bow holds never struck; the sentry as a guard is the first lever.

## Not in this slice

Keziah's quest 2 (her oath; a Table round first), the drake's stages (#805), the writing, the Godot camp row.

## Addendum, 2026-10-03: the holds on the exit approach (issue 862, round 284)

Two chairs played the board as built: Code warm 8/6/6 and Chat cold 8/6/8. Neither play saw a hold shoot, and #635's side-map gate needs choice at 7, so the Table agreed one lever (rounds 284 and 285): the holds' rings go on the exit approach, with at least one exit left outside both. The sentry stays a hold, the turn-4 rider keeps its turn, and the courtyard cage is untouched.

- **Built:** the two holds trade places and move east. The sentry (steel lance, iron bow) goes from 12,6 to 13,3, so its bow covers exit 14,4 and its lance covers 13,4, the perch beside it. The archer goes from the fort at 11,3 to 12,6, where its bow covers exit 14,6. 14,5 is in neither ring. Since an exit is the action and not a move (0074), a unit waits out one enemy phase on its exit tile. The price is therefore the tile: one free exit for two units, so either one of them stands under a bow, or Rook gives up the free tile and the pair spends a turn. Rook leaves last.
- **Why the sentry is in the corner.** A first arm moved only the archer, to 13,3, and Rook struck it from 13,4 at 79% x2 with no counter, since a bow does not answer at range 1. The lance makes that corner cost something. The fort at 11,3 is now empty.
- **Measured** (`docs/measurements/the_rookery-862.txt`, 200 seeds): gate 1 went from 72 to 36, all with the captain alone, and no run escaped with the ally. Hold shots went from 0 games to 182, 107 of them on Rook standing on 14,4. As on the other side maps, the gate is a cold chair.
- **Played:** Code warm on 1132 with the new holds scored 7/7/5 and won on turn 9 with nobody lost. Rook held 14,5 from turn 4 (the old 13,4 perch now reads the lance at 56% for 15 against 4 HP), and Wren killed the archer on 12,6 to open 14,6. Neither hold fired. That kill was cheap (86% x2, no counter, on her road), and a cold chair decides whether it counts as a choice. If not, the next lever is the archer at 13,7, off the road.
- The journaled replay test reads the new play (`2026-10-03-the_rookery-1132-862.*`). The first play's transcripts stay as the record of the old board; they no longer replay on it.
