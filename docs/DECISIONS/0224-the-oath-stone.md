# 0224 — The Oath Stone, Keziah's quest 2 (#635 slice 15)

Date: 2026-10-04. Built by the chain Builder. What to build is the Table's: DESIGN 14's slot table (the pick's quest 2 after map 9), the side-map shape, and STORY draft 6 ("Quest 2 is her oath, on one side of the hunger or the other"; DIALOGUE: "plays either side"). Nothing on the Table said what the oath is on a board, so the board is Code's lean (provisional), put to the Table as round 302 on #875. Chat can argue it there or on the PR.

## What is built

- `content/quests/the_oath_stone.map`: defeat boss, 15x10, limit 9, Recall 2, enemy level 6, `announce: on`, `oathbound: envoy, oath`, `freed: 9,4 by envoy`. Keziah holds the captain slot at 1,4; the ally's bare slot is 1,5. Hask's envoy (the Sworn Captain, a sleeping guard boss) sits on a fort at 12,3 in a walled yard (columns 9 to 14, rows 0 to 7). The yard's west door at 9,4 is corked by a held Marauder bound to the envoy; a breach at 11,7 lies under a held archer at 11,5. A sleeping camp pair (brigand 5,8, soldier 6,9) stands on the southern road. Announced pursuers: a rider at 0,0 on turn 3, a brigand at 0,9 on turn 5.
- **The oath is the board, not a verb.** Two built systems point opposite ways. Kinsbane drains 5 a phase unless fed (0130), on every map she carries it. The bond (0172) frees the bound enemy when the envoy falls, which is not a kill and feeds nothing. The short road goes through the bound man (kill him: fed, door open); the long road keeps him alive and pays the drain unless the scythe eats something else. The ally can kill him too, feeding nobody. Both are printed before the first command.
- `campaign.json`'s `keziah_2`: part 2, 2 common, no `pays`, no `rare`. Kinsbane is already her signature, issued at the pick, as Rook's drake is hers (0208). It opens two maps after `keziah_1` is won (`QuestOpensAt`), so only a Keziah run that won The Burned Shrine sees it. Appended last in `quests`, so no other side map's seed moves (0198). Cards are placeholders with the rules line until the writing pass.
- **The record.** `AfterQuest` never touches `FreedUnitFell` (only `AfterBattle` does), so a side map's bond cannot leak into the finale's ending, which reads Marrit's fate. Which side Keziah took is not recorded; that is #634's, if an ending wants it.

## Measured and played

- Sim: see the PR (`--full`, 200 seeds, `--lead keziah --lead ottilie`). As on every side map, the gate is a cold chair (round 194).
- Code's warm play (side-map seed 1133, level 7, ally Ottilie, fed 9): lost on turn 6, one Recall spent, 8/5/6. The hunger decided every turn; the oath never came up, because the camp woke on turn 1 and the envoy walked out of the yard past his bound man and went home. Choice at 5 fails the side-map gate (7).

## Levers, one at a time, in order

1. The camp pair further from the start, so the oath road is the long road and not a turn-1 trap.
2. The envoy as `boss` (keeps his fort), so the yard stays shut and the door is the question.

## Not in this slice

Recording the side taken, the writing, the Godot camp row.
