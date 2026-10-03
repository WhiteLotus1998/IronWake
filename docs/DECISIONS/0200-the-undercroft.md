# 0200 — The Undercroft, Pell's quest 2, pays Pell's Commonplace (#635 slice 7)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (Pell 2 after map 6, round 260), STORY draft 6 (quest 2 is the undercroft, where she reads what the armistice seals, and it pays her signature tome), the side-map shape, and the signature item's default shape (the best shop weapon of its rank plus an art only it declares, 0099). The board, the tome's numbers and the art are Code's lean, and Chat can argue any of them on the PR.

## Why Pell 2 before Teodor 1

Teodor 1 comes first in the slot table (after map 5), but it cannot be built from what is agreed. STORY says his quest 1 "wakes" the family lance, and nothing issues the lance: Teodor's cast pack carries an iron lance (0131 left it so, behind the story gate), and the lance wakes on its own hidden counter. Whether the quest issues it, turns it a stage, or something else was a Table question, asked in round 265 and answered in round 266 while this slice was built (the lance packed from arrival, iron first; quest 1 holds it at sound until won). Pell 2 was fully specified, so this run built it.

## What is built

- `content/quests/the_undercroft.map`: seize, 17x11, limit 7, Recall 2, enemy level 4, `announce: on`. Pell holds the captain slot at 2,2 in the stair room, the ally's bare slot is at 2,3, and the desk (the throne) is at 16,4 in the reading room. Three routes lead east. The north passage is long and quiet. The middle runs through the stacks, past a sleeping brigand (7,4) and hexer (11,4). The south gallery runs past the sleeping cells, a soldier (6,9) and an archer (10,9). A held lector at 14,3 corks the north door into the reading room; the middle door (12,4 to 13,4) and the south door (14,6) are open. The sworn come down the stair at 1,10 from turn 2 (a brigand, a soldier, a hexer), announced.
- **The twist is that the walls are thin.** The wake radius is Manhattan with walls not considered (DESIGN 8), and the board is built around it. On the north passage the quiet stops fall exactly four tiles apart (5,1, 9,1, 13,1), so an infantry unit can cross without waking the stacks only by counting. A spell crosses walls too: a hexer in the stacks strikes the passage at range 2. The lector's own strike is noise within 6 of the stacks, so even the quiet route ends with them awake and coming through the middle door.
- `campaign.json`'s `pell_2`: part 2, `pays: pell_commonplace`, 3 frozen iron (the loader asks for 3 rare per issued signature that refines), appended so no other side map's seed moves (0198). It opens two maps after Pell 1 is won, by the part-2 rule; no `opensAfter`. The cards are placeholders with the rules line until the writing pass (#811).
- **Pell's Commonplace** (`pell_commonplace`): Cinder's numbers (reason E, 5/90/0, weight 3, range 1 to 2, 8 uses), bound to Pell, unpriced, and it never ignites: she does not set fire to a page. Rank E, because Pell starts at Lore E and no rank report puts her at D by map 6, so it works the day it is paid. The ceiling reads it at 0.78 of Bolt.
- **Read Ahead** (`read_ahead`, reason E, cost 2, Mt -2, range +1, `item` the Commonplace): a reason strike at range 3, which no stocked tome reaches. It is built from her flaw (a page over a person): she trusts what she has read about the range more than the people beside her. It loses to the plain attack on 30 of 30 targets in the ceiling's read (2 less damage), and wins where the third tile is out of every answer. Pell knows it from the start, as Maud knows Unasked.

## Measured and played

- Sim, 200 seeds, `--lead pell --lead wren`, level-1 cast: gate 1 2/200 (198 timeouts), winning turn median 5. Gate 4 reads Wren's drop at 0.010. These are the other side maps' shapes: the gate is a cold chair (round 194).
- The first cut put the reading room's lector off the path. The quiet route then won on turn 5 with one strike taken, so the lector moved onto the north door, and a south door was opened (the gallery led nowhere). The starting tiles were inside the brigand's radius in an earlier cut; they moved one row north.
- Code's warm play (seed 960, a save at the camp after map 6 at level 5, ally Wren): won turn 6, Pell at 7 HP, no Recall. Two earlier runs from the same save were lost or abandoned, both disclosed in PLAYTEST.

## Not in this slice

Teodor 1 (round 266), the writing, the Godot camp row, the Sim's player declaring Read Ahead (it declares no art), and gate 4's ablation with the item.
