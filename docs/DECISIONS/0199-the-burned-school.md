# 0199 — The Burned School, Pell's quest 1 (#635 slice 6)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (Pell 1 after map 4, round 260), the side-map shape (the member plus one ally, a twist of its own, never a reused board), round 260's ruling that quest 1 pays a class door where one exists and no second signature on campaign boards until 13.18 is kept, and STORY draft 6 (Pell's shrine-school was burned around her; Hask has spent eight years burning the rite). The board's shape and its reward are Code's lean, and Chat can argue either on the PR.

## What is built

- `content/quests/the_burned_school.map`: escape, 13x9, limit 7, Recall 2, enemy level 2, `announce: on`, exits 12,3 12,4 12,5. Pell holds the captain slot at 6,4 in the courtyard of a walled school, and the ally's bare slot is at 5,4. The school has two gates on row 4. A held shieldbearer at 9,4 corks the east gate (Def 9: Cinder 14, a sword 5). A held archer on the hill at 11,2 covers 12,3. Announced burners come in through the west gate: brigands on turns 1 and 3, a hexer on turn 2, a soldier on turn 4.
- **The twist is three chests against an escape.** The Gust at 6,2 is in the north room and the Field Dressing at 6,6 in the south room, each behind a one-tile door off the courtyard. The Bolt at 1,1 is outside the west gate, beside a sleeping chapel guard and on the burners' road. Every chest turns a unit back toward the burners. Since the captain's exit leaves anyone still on the board as fallen, the ally has to get out before Pell does.
- `campaign.json`'s `pell_1`: part 1, 2 common material (Maud's quest 1's payout), appended after `maud_2` so no other side map's seed moves (0198). Pell has no unique class and no 13.18 signature, so the material and whatever the chests held are the whole reward (round 260: quest 1 pays a class door where one exists). The cards are placeholders with the rules line until the writing pass (#811).

## Pell's slot is held by `opensAfter`

DESIGN 14 times quest 1 from the member's second map, and Pell's slot (after map 4) assumes she arrives on map 3. The campaign does not name her arrival yet, since only Maud `arrives` until the levy roster is built, so arrival timing would open her quest after map 2. `pell_1` therefore carries `opensAfter: the_tollgate`, the hire's field from #691, which the loader already accepts on a member's quest. It opens at the camp after map 4 and stays on offer while unwon, the way an arrival-timed quest does. When Pell gets an `arrives`, the field can come off. The camp's label (`quest 1` against `request`) now follows whether the member is in the cast, not whether the quest names `opensAfter`.

The offer is real, so it takes an interlude seat. With maud_1 unwon, Bet's request and Maud's quest 2 can now wait a camp for it; that is DESIGN 14's overflow rule doing its job. Test content that predates this slice drops `pell_1` (the fixture copies, so their journaled transcripts replay as played). The shrine transcript plays on the real content, so it was regenerated: its camp now lists `pell_1`, and none of its commands changed.

## Measured and played

- Sim, 200 seeds, `--lead pell --lead wren`, level-1 cast: gate 1 36/200 (164 timeouts, no deaths), winning turn median 5. Gate 4 reads Wren's drop at 0, the Lazar House's failure shape. The heuristic opens no chest. As with the other side maps, the gate is a cold chair (round 194).
- The first cut had a soldier corking the gate, limit 8, and the burners from turn 2. At level 4 both inner chests were free on turn 1 and the cork fell on turn 2, so the waves moved a turn earlier, the limit came down to 7 and the shieldbearer replaced the soldier. With the soldier, gate 1 read 180/200.
- Code's warm play (seed 884, a save at the camp after map 4 at level 4): won turn 6 with one chest. A first attempt on the same seed was lost with both units dead, after two printed lethals were ignored. Both are in PLAYTEST.

## Not in this slice

Pell's quest 2 (the undercroft charter, after map 6), the writing, the Godot camp row, and the Sim's player opening chests.
