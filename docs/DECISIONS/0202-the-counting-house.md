# 0202 — The Counting House, Ottilie's quest 1 (#635 slice 9)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (Ottilie 1 after map 6), the side-map shape, round 260 (quest 1 pays a class door where one exists; no second signature on campaign boards until 13.18 is kept) and STORY draft 6 (Ottilie's quest 1 is the counting house and ends at Hask's seal). The board is Code's lean, and Chat can argue it on the PR.

## What is built

- `content/quests/the_counting_house.map`: rout, 13x9, limit 10, Recall 2, enemy level 3, `announce: on`. A one-tile canal runs down column 6 with two bridges, 6,1 and 6,7. Ottilie holds the captain slot at 2,6 and the ally's bare slot is 3,7, on the near (west) bank. On the far bank a held archer at 7,3 and a held soldier at 7,7 stand at the water; a yard pair (brigand 10,7, hexer 9,6) and the house (lector 10,3, the Sworn Captain on the strongroom fort at 11,1) sleep as guards. Announced men come up the near bank behind the pair: a brigand at 0,8 on turn 2, a hexer at 0,0 on turn 3.
- **The twist is the canal.** A bow strikes across it and a sword does not, so Ottilie can open on the bank without crossing; but so can the far bank's archer and every caster, and a shot at 7,3 is a combat within 6 of the whole house. The bridges are one tile, and a unit on the near end (5,7 or 5,1) is the only tile an enemy crossing them can strike from: the player holds the cork this time.
- **Rout, the one objective no side map used.** It forces the crossing: the holds at the water never come, so the board cannot be won from the bank.
- `campaign.json`'s `ottilie_1`: part 1, `opensAfter: ironwake_raid` (Ottilie's arrival is not named yet, the hold Pell's and Teodor's slots use, 0199, 0201), 2 common material. Her door, Marksman, is a standard form, and her 13.18 signature stays off campaign boards, so material is the reward. Appended last in `quests`, so no other side map's seed moves (0198). The cards are placeholders with the rules line until the writing pass (#811).

## Measured and played

- Sim, 200 seeds, `--lead ottilie --lead wren`, level-1 cast: gate 1 0/200, every game a timeout and no deaths. The heuristic stalls behind its veto on the bank, as on the other side maps (2/200 to 36/200); the side-map gate is a cold chair (round 194).
- The first cut (enemy level 2, a soldier at the south bridge) fell on turn 1 at level 5: the cork and the yard died to Wren before the road arrived. Level 3 and a shieldbearer cork made the far bank a grind (25 HP at 2 to 4 a hit), which a rout turns into a slog, so the cork went back to a soldier, a turn-4 road soldier was cut (nine enemies for two units was too many), and the limit rose to 10.
- Code's warm play (side-map seed 980, a save at the camp after map 6 with the cast at level 5; ally Wren): lost on turn 8. Both Recalls spent, Wren and Ottilie fell. The journal is in PLAYTEST.

## Not in this slice

Ottilie 2 and Teodor 2, the writing, the Godot camp row, and a Sim player that crosses a canal.
