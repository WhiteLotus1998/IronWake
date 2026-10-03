# 0198 — The First Shrine, Maud's quest 2, pays the Psalter (#635 slice 5)

Date: 2026-10-03. Built by the chain Builder. What to build is the Table's: DESIGN 14 (quest 2 a side map one size larger than quest 1, paying the signature item, two maps after quest 1), STORY draft 6 (Maud's quest 2 is the first shrine, where the goddess speaks to her once), round 260 (Maud 2 after map 5, the Psalter as her item; 0196). The board's shape is Code's lean; Chat can argue it on the PR.

## What is built

- `content/quests/the_first_shrine.map`: seize, 14x9, limit 10, Recall 2, enemy level 2, `announce: on`, `brace: on`. Maud in the captain slot at 6,5 and the ally's bare slot at 8,5, just over a causeway. The sanctum is walled: the altar (Gate) at 7,0, a one-tile door at 7,2, a held soldier at 7,1 corking it and a held archer at 5,1. A sleeping yard pair (brigand 2,3, hexer 11,3) flanks the approach. Behind the party the river has one crossing, the causeway at 7,6, and a long way round at 13,6; announced pursuers arrive at 7,8 (turn 2 brigand, turn 4 soldier), 6,8 (turn 3 archer) and 13,8 (turn 5 brigand).
- **The twist is the cork and the brace.** On `brace: on` a held enemy that waits braces, so the door soldier is struck at -15 every turn he holds. The door opens only to a committed strike, and the pursuers make every turn spent on it a turn the back is open. The ally can hold the causeway (and brace there) or go through the door with Maud; it cannot do both.
- `campaign.json`'s `maud_2`: part 2, `pays: maud_psalter`, no material (a healing spell never Refines, so the rare rule needs none). Placeholder cards: a before card with the rules line, an after card marking where the goddess speaks, and a rules line naming the Psalter and Unasked. Text waits on `docs/WRITING.md` (#811).

## Side-map seeds are stable (a fix on the way)

`CampaignRecord.QuestSeed` counted the member quests, so adding a second one moved every side map's seed, the Lazar House's included, and three journaled transcripts stopped replaying. A side map now seeds on its place in `campaign.json`'s `quests`: past every map's and trial's seed, offset by the map index plus the map count times that place. That equals the old rule for the two quests it seeded (maud_1 first, bet_postern second), so no existing transcript moves. New quests are appended to the file, and an appended quest moves no other side map's seed (a test holds it). `maud_2` sits after `bet_postern` for that reason.

## Measured and played

- Sim, 200 seeds, `--lead maud --lead wren`, level-1 cast: gate 1 5/200 (178 timeouts, 17 Maud), gates 2 to 8 ok. The heuristic does not break the cork at level 1; as with the Lazar House, the side-map gate is a cold chair (round 194), not gate 1.
- Code's warm play (seed 875, from a save before the raid at level 4 with quest 1 won): won turn 4, nobody fell. Two earlier attempts on seed 861 (before the seed fix) are disclosed in PLAYTEST: Wren dead on turn 1 in one, Maud at 6 HP in the other. The waves after turn 3 never arrived in time to matter. Transcript `docs/transcripts/2026-10-03-the_first_shrine-875.txt`.

## Not in this slice

The goddess's scene (writing pass), the Godot client's camp row for a part 2 payout, gate 4's ablation with the Psalter on a main map, the Sim's player declaring Unasked.
