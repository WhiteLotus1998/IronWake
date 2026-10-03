# 0206 — The Burned Shrine, Keziah's quest 1 (#635 slice 13)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (the pick's quest 1 after map 7), the side-map shape, round 260 (quest 1 pays a class door where one exists; no second signature on campaign boards until 13.18 is kept) and STORY draft 6 (Keziah's quest 1 is the burned shrine; her door, the Berserker, is the standard Reaver door, so there is no unique door to open). The board is Code's lean, and Chat can argue it on the PR.

## What is built

- `content/quests/the_burned_shrine.map`: rout, 15x9, limit 10, Recall 2, enemy level 5, `announce: on`. A ring wall (columns 4 to 10, rows 1 to 5) with a one-tile west door at 4,3 and a one-tile breach at 7,5; the hearth inside is a fort at 7,3. Keziah holds the captain slot at 1,6, the ally's bare slot is 2,6.
- The nave group, asleep: a brawler (gauntlets) at 6,4, an acolyte (Radiance, Salve) at 8,2, and a held shieldbearer at 7,2, beside the hearth and never on it. The grove group, asleep, outside the east wall: an archer at 12,1 and a hexer on the hill at 13,3. Announced burners: a rider at 14,7 on turn 3, a brigand at 0,0 on turn 5.
- **The twist is the hearth.** It is the best tile on the board (fort, heals 5) and the only tile beside the shieldbearer that a fight from wakes the grove: the hexer at 13,3 is six tiles from 7,3, inside the noise radius, while a strike from 6,2 is seven from it and eight from the archer. The rule is printed; the tile is the player's to find. Keziah's two weapons are the other choice: gauntlets beat the axe on everything but armour, and on the plain the shieldbearer reads 4 x4 at 98 against the axe's 9 x2 at 82.
- `campaign.json`'s `keziah_1`: part 1, `opensAfter: sallow_grange`, 2 common material, no door. Offered only when Keziah is on the roster, so a Rook run never sees it. Appended last in `quests`, so no other side map's seed moves (0198). Cards are placeholders with the rules line until the writing pass (#811).

## The first cut, and what play changed

- The shieldbearer stood on the fort. Its 5 a turn against Keziah's axe at 60 was a grind with no choice in it; moved to 7,2, the fort became Keziah's.
- The grove's archer was held at 12,1. Once the rest fell, turns 8 to 10 were a walk round the east wall to a unit that never moved; as a guard it wakes with the grove and comes to the breach, which a unit at 7,4 corks against bows.

## Measured and played

- Sim, 200 seeds, `--lead keziah --lead wren`, level-1 cast: gate 1 0/200 (all timeouts). As on the other side maps (0/200 to 36/200), the side-map gate is a cold chair (round 194).
- Code's warm play (side-map seed 1113, a save at the camp after map 7 with Keziah picked and the cast at level 7; ally Wren): won on turn 8, no Recall, nobody fell, 7/7/5. One Recall spent on the first cut is not in the transcript. The journal is in PLAYTEST.

## A test note

A record started with no pick has both claimants on the roster, so `rook_1` and `keziah_1` together fill an interlude's two side-map places. No played campaign can reach that state (the pick is made at the raid's camp and is final), so the tests that read later interludes now start with Rook picked.

## Not in this slice

Both claimants' quest 2, Kinsbane on the pick (#804), the writing, the Godot camp row.
