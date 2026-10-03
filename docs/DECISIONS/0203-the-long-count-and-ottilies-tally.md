# 0203 — The Long Count, Ottilie's quest 2, pays Ottilie's Tally (#635 slice 10)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (Ottilie 2 after map 8), the side-map shape, STORY draft 6 (quest 2 is "his books", proving to the regions' people that Hask did it) and the signature item's default shape (the best shop weapon of its rank plus an art only it declares, 0099). The board, the bow's rank and the art's numbers are Code's lean, and Chat can argue any of them on the PR.

## Why Ottilie 2 before Teodor 2

Teodor 2 is first in the slot table (after map 7), but nothing settles what it pays. Quest 2 pays the signature item, and Teodor's signature item is the Family Lance, which he already carries and which quest 1 wakes (0201). STORY says quest 2 "names it". The payment is a Table question, so this run built the next fully specified slot.

## What is built

- `content/quests/the_long_count.map`: escape, 15x9, limit 8, Recall 2, enemy level 5, `announce: on`, `dusk: 4`, exits 14,3 to 14,5. Ottilie holds the captain slot at 1,4 in a walled bookroom, the ally's bare slot is at 2,4, and the room's one door is 3,4. A stream runs down column 9 with two crossings: the road bridge at 9,4, with a sleeping soldier on its far end (10,4) and a held archer on a fort at 11,3, and a footbridge at 9,1, the long way round. A sleeping pair guards the gate (a hexer on the hill at 12,1, a brigand at 13,6). Announced pursuers come up behind: a rider at 0,8 on turn 2, a brigand at 4,0 on turn 3 and a hexer at 0,8 on turn 4.
- **The twist is the dusk.** Sight is 4 on turn 1 and 1 from turn 4, so for the first three turns Ottilie's bow works alone. From turn 4 she shoots only what an ally stands beside. A bowman whose flaw is pricing people needs one, and Wren's spotting decided turn 4 of the warm play. The dark also blinds the enemy: a pursuer that arrives without knowing where you are waits, and the fort archer sees only what stands beside it.
- `campaign.json`'s `ottilie_2`: part 2, `pays: ottilie_tally`, 3 frozen iron (the loader asks for 3 rare for each issued signature that refines). It opens two maps after Ottilie 1 is won, by the part-2 rule, with no `opensAfter`. It is appended last so no other side map's seed moves (0198). The cards are placeholders with the rules line until the writing pass (#811).
- **Ottilie's Tally** (`ottilie_tally`): the Iron Bow's numbers (bow E, 5/70/0, weight 5, range 2, 40 uses, crit +20 on fliers), bound to Ottilie and unpriced. It is rank E because she starts at bow E and needs 30 points for D, and `levels-776` counts about ten bow combats per won campaign, so the median Ottilie is not at D by map 8. E makes it work the day it is paid, as Pell's Commonplace does (0200). The ceiling reads it at 0.80 of the Steel Bow.
- **Paid in Full** (`paid_in_full`, bow E, cost 4, Mt +6, Hit -25, `single`, `item` the Tally): one heavy arrow that never doubles, billed at five uses. It is built from her flaw, everything has a price: she knows exactly what the shot costs and pays it. It loses to the plain attack on 13 of the ceiling's 30 targets (the soft ones, where the plain arrow's hit wins) and beats it on armour. The first cut, Hit -10, lost on none, because at her cast card she doubles nothing and +6 Power for -10 hit always wins. -20 lost on 1, and -30 lost on 23. Ottilie knows it from the start, as Maud knows Unasked and Pell knows Read Ahead, and it is declared only with the Tally.
- Tests: the old-transcript fixture drops a part 2 whose part 1 went with the held slots, and drops Paid in Full from Ottilie's card the way it drops Read Ahead.

## Measured and played

- Sim, 200 seeds, `--lead ottilie --lead wren`, level-1 cast: gate 1 2/200 (198 timeouts), winning turn median 6. This is the shape of the other side maps; the gate is a cold chair (round 194).
- Code's warm play (side-map seed 91, a save at the camp after map 8 with the cast at level 7, ally Wren): won on turn 8 with one Recall, nobody fell. The fort archer never acted, and that is the first lever if a cold chair finds the map soft.

## Not in this slice

Teodor 2 (its payment goes to the Table), the writing, the Godot camp row, the Sim's player declaring Paid in Full (it declares no art), and gate 4's ablation with the item.
