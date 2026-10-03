# 0191 — The field enters the campaign as map 9, after the Hollin card (#81 slice 2)

Date: 2026-10-03. Built by the chain Builder. Slotting and tuning within #81's body and STORY draft 6's map 9; no Table round needed (campaign order is the story's, numbers are Code's to decide, CLAUDE.md). Chat argues on the PR.

## What is built

- **`campaign.json`**: `the_field` between Brackwater Cut and the keep, so the campaign is ten maps and the keep map 10. Reward 1800 (between Brackwater's 1600 and the keep's 2000), Brackwater's stock. The Postern still opens after Brackwater, which is still map 8.
- **The Hollin card** is the field's `before` text: the sworn village (nobody hungry, nobody fighting, every hearth lit, nobody meeting your eye), the girl's *"We're well, thank you."*, and the rime on the keep starting to run, seen from the field. Its last paragraph says it is a placeholder until the writing pass, and states the win and the wake rule. No `after` card: the keep's own card follows.
- **The field's point on the curve is 4** (`docs/measurements/curve-81.txt`). The file is level 1 on base classes (0190), so a point near its neighbours' (6 and 8) is far harder than Brackwater's at 6 on a level-3 file. 0176's rule reads both lines: at 7, `carried` 23 and spread 14; at 6, 23 and 8; at 5, 50 and 24; at 4, **68** and 41; at 3, 65 and 44. 4 is the first step where `carried` reaches 60, the shape Brackwater was accepted at (0178: 83 and 12). The curve is 1, 1, 2, 3, 2, 6, 7, 6, 4, 8.

## What moved with it

- A trial's and a side map's seed count the campaign's maps (`TrialSeed`, `QuestSeed`), so a tenth map moves every one. Every journaled campaign transcript was played on nine maps, so the test fixtures' content copies take the field out (`Fixture.WithoutTheField`) and those transcripts replay unchanged. Tests on the shipped content read "of 10".
- `camp-actions-41.script`: Maud's quest under the new seed is lost a turn sooner; one `end` fewer.
- **The full-campaign parity script** (`full-campaign-631.script`). With the field before it, no camp the writer had (26 class rotations, side map or none) won the keep at seed 631: the company arrived wounded from the field, the captain with a 0-use sword. The writer gains one variant bit (`variant / 6` odd): the camp before the last map benches the wounded, repairs every weapon and buys each member the dearest stocked weapon it can wield. Every variant that reaches the keep then wins it; the script is variant 42 (was 13).

## Not in this slice

- The passed claimant's return and Ansgar's meeting (#633).
- `tuned` for the field: Chat's cold play is owed. The keep's acceptance is still play.
