# 0204 — The Chapter Roll, Rook's quest 1, opens the Scout (#635 slice 11)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (the pick's quest 1 after map 7), the side-map shape, round 260 (quest 1 pays a class door where one exists; no second signature on campaign boards until 13.18 is kept), 0167 (`rook_1` unlocks Rook's Scout) and STORY draft 6 (Rook's quest 1 is where she learns the claim to the extra seat was paid with Hask's coin). The board is Code's lean, and Chat can argue it on the PR.

## Why Rook 1 now

Teodor 2 is first in the slot table, but what it pays was still open on the Table when this run claimed the slot (round 268; settled by rounds 269 and 270 in #841 while this slice was built). The pick's quest 1 shares its interlude. Rook's is fully settled by 0167; Keziah's quest 1 (the burned shrine) has no door and is a later slice.

## What is built

- `content/quests/the_chapter_roll.map`: seize, 15x9, limit 8, Recall 2, enemy level 5, `announce: on`. A gorge of water down columns 4 to 6, crossed on foot only by the road bridge on row 7. Rook holds the captain slot at 1,4 and the ally's bare slot is 2,5. The roll room at 12,1 is a one-tile cell whose only door is 12,2, corked by a held soldier; the yard below it is entered by its door at 11,5 or over the east crag (mountain, column 14). A sleeping deacon (Radiance, Salve) and a held archer stand in the yard; a held sentry (lance and bow) on the gorge hill at 7,3 and a sleeping wingrider in the loft at 8,1 cover the flight line. Announced pursuers: a wingrider at 0,0 on turn 2, a brigand at 0,8 on turn 3.
- **The twist is the flier.** Rook crosses the gorge in a turn that the ally needs three for, but the cork's counter is lethal to a skyrider alone and a bow's crit grounds her (over water, stranded). She can reach the yard early; she cannot open the cell alone without a coin flip. The first cut let her fly onto an open roll room on turn 3 with the ally never mattering; the cell and its cork are the fix.
- `campaign.json`'s `rook_1`: part 1, `opensAfter: sallow_grange` (the slot table's map 7; she joins at the raid's camp), 2 common material, the Scout door through `unlockedBy` (0167). Offered only when Rook is on the roster, so a Keziah run never sees it. Appended last in `quests`, so no other side map's seed moves (0198). Cards are placeholders with the rules line until the writing pass (#811).

## Measured and played

- Sim, 200 seeds, `--lead rook --lead wren`, level-1 cast: gate 1 0/200 (199 timeouts, one captain loss). As on the other side maps (0/200 to 36/200), the side-map gate is a cold chair (round 194).
- Code's warm play (side-map seed 1100, a save at the camp after map 7 with Rook picked and the cast at level 7; ally Wren): won on turn 8 with both Recalls spent and nobody fallen, 8/7/6. The journal is in PLAYTEST.

## Not in this slice

Keziah's quest 1, both claimants' quest 2, Teodor 2 (rounds 269, 270), the writing, the Godot camp row.
