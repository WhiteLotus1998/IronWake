# 0039 — Old Mill Road: one dressing per cadet, through a `ration:` header

Date: 2026-09-25. Issue 160's next pass (issue 13's Old Mill Road slice), from the twenty-eighth round. Chat's cold re-rate on DECISIONS/0034 is 6/7/5 on seed 131, which passes on choice and fails on tension and surprise. Both partners' plays name the same leak: the road's cost is gone by turn 4. Chat's lean is to cut map 1 to one dressing per cadet before moving the party's start. Built by Code; Chat re-rates cold and argues on the PR if it disagrees.

## Decision

A new optional map header, `ration: <item>:<uses>`, space-separated when it names more than one item. When the map starts, it caps each deployed player unit's stack of each named item at that many uses (`MapDefinition.Rationed`, applied in `BattleState.From`). Old Mill Road ships with `ration: field_dressing:1`. Nothing else on the map moves: not the grid, not the limit of 12, not the pair, not the mill.

- **Why a header and not the cast.** `cast.json` gives every cadet one `field_dressing` stack at the item's 3 uses, and the cast is the same on every map, so cutting it there would also cut Saltmarsh Ford and the Tollgate. Supply is the map's to set, the way `recall:` is.
- **A clamp, never a set.** A unit carrying fewer uses keeps fewer, and a map can take supplies away but never hand out more than a unit carried. When the between-map screen (#74) carries inventories across maps, a rationed map stays a scarcity rule and cannot become a free refill.
- **Player units only.** Enemy templates carry what they carry; a test holds it.
- **Validated on load.** The loader refuses an entry that isn't `<item>:<uses>`, an id that isn't in `items.json` (weapons included, since durability is not a supply), uses outside 1 to the item's own uses, and an item named twice. Each error names the file and the header's line. `MapFormat.Write` emits the header after `rivalry:`, so the canonical round trip holds.

## Measured (two rolls, 200 seeds, Old Mill Road only; nothing else moved)

| Dressing uses per cadet | Gate 1 | Losses | Gate 4 (Wren) |
|---|---|---|---|
| 3 (0034, before) | 107/200 (54 percent), median 9, p90 11 | 38 timeout, 55 captain | 0.515 |
| 2 (probed, not taken) | 93/200 (47 percent), median 9, p90 11 | 33 timeout, 74 captain | 0.455 |
| **1 (shipped)** | **59/200 (29 percent), median 8, p90 10** | **32 timeout, 109 captain** | **0.285** |

Gates 2 to 8 pass. Full output: `docs/measurements/2026-09-25-old_mill_road-ration-200seeds.txt`.

## Gate 1 fails harder, and is read the way 0034 read it

Every dressing taken away costs the heuristic captain deaths, and none of them costs stalls. That matches 0034's reading: the planner walks both cadets into the wave, keeps a hurt recruit swinging, and heals only below half, so each spent dressing is a captain death later. On seed 8 it dies on turn 4 without having used a dressing at all. The hand play never met that board. By hand, seed 139 was won on turn 7 with no Recall and no dressing used (below). So the gate is still read as the heuristic's recruit policy against a wave it walks into, not as the map being unbeatable. It stays owed, and it is the first thing this map answers after Chat's re-rate. The levers are unchanged: the party's start, then the archer's numbers, never the mill. The ration is a dial between 1 and 3 if the re-rate says the road now costs too much.

## The hand play

Seed 139 won on turn 7 of 12 with no Recall and nobody dead. PLAYTEST.md has the entry (6/6/4). The transcript is `docs/transcripts/2026-09-25-old_mill_road-139.txt`, and a test replays its script under `--strict` in place of seed 101's, which spends a second dressing and no longer replays. The ration never came into play: the brigand missed Wren at 51, so the road cost nothing. The one tense decision was turn 6: Wren on 10 HP, with her one dressing unspent, taking an 85 against a bandit whose counter would have killed her. A ration only binds when the road lands, so it adds no tension on a kind seed. Chat's seed 131 spent exactly one dressing per cadet, and it replays to the same game under the ration, with only the printed uses left differing.

## Tests that moved with the map

The CLI's dressing test (captain's stack is now 1, and gone after use). The journaled-script test (seed 139 in place of 101). The trace-with-an-item test and the win-not-stall test (seed 20 in place of 5, which now loses to a captain death). The DESIGN.md example and the retreat sample take the header, because tests hold both equal to the shipped map.

## Not decided

The map is not `tuned`. It needs Chat's cold re-rate on this version, then gate 1.
