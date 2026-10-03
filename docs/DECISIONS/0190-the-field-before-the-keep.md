# 0190 — The field before the keep: the board (#81 slice 1)

Date: 2026-10-03. Built by the chain Builder. Authoring and tuning decisions within #81's body; no Table round needed (map layouts and numbers are Code's to decide, CLAUDE.md).

## What is built

- **`content/maps/the_field.map`**, 20x16, Defeat Boss, limit 20, Recall 3, `enemy_level: 1`. A river on columns 13 and 14 runs from the north lake to row 12. Ground units cross it at the bridge (rows 7 and 8) or round its southern end (rows 13 to 15). Only a flyer crosses anywhere else.
- **Five groups, sequenced by distance.** West of the river: `pickets` (an archer in forest, a soldier, a brigand) to the south and `line` (an archer on the fort at 10,2, two soldiers) to the north. The two are 10 tiles apart at their nearest, so no tile wakes both, and a fight beside one is outside the other's noise. At the bridge, `bridge` is one Sentry on Hold. East of the river, `camp` is a brigand, an archer and the boss, guarding the fort at 18,6. The bridge's east end is inside the camp's noise, so crossing there is the commitment. `south` is one rider asleep on the long way round.
- **A new boss, `sworn_captain`** (Sworn Captain, pikeman L3, steel lance only). It plans under the boss veto (0077), and on its fort it holds against a full party.
- **The board's cast is the ground six** (captain, Teodor, Ottilie, Pell, Maud, Keziah). The first draft fielded Rook in Keziah's slot. The heuristic flew her into the pickets on turn 1 every game, and benching her raised gate 1 by twelve wins (gate 4 named her dead weight at drop -0.200). The lake is still the flyer's route for a campaign company that has one.
- Art: the boss's eight clips are on ART_SPEC's names block and generated (`make_art.py`, `for_lotus.py`).

## The tuning, in order (60 seeds each unless noted, `docs/measurements/the_field-81.txt` for the last)

| Board | Gate 1 |
|---|---|
| Draft: enemy level 2, advanced classes in the camp, two bridge holders, a deacon, limit 16 | 3% |
| Level 1, one bridge holder, no deacon, limit 18 | 3% (the cast at L1 against Veteran, Marauder, Longbowman) |
| Base classes throughout | 27% |
| Boss without the toll spear, limit 20 | 25% |
| Deploy moved south, the line moved north and apart (the groups stopped merging on turn 1) | 27% |
| Camp moved north off the southern way (the rider's fight stopped waking it) | 42% |
| A ground cast (Keziah for Rook), two pickets | 88% at 100 |
| Third picket back at 8,14 | 55% at 200 |
| Third picket at 10,14 | **60% at 200** |

At 200 seeds every gate passes: gate 1 120/200 (60%; 48 losses, 32 timeouts, the stall median 0.0355), gate 2 0%, gate 3 none, gate 4 median drop 0.355, gates 5 to 8 ok. The **free prefix is 0** (prefix 2 drops 0.200): no turn of this map can be given away, which is what #81 wants of a finale.

## Not in this slice

- The campaign slot before the keep, the Hollin card (STORY draft 6, placeholder text), and every list pinned to the nine-map order (`CampaignContentTests`, the Postern's `opensAfter`, `--curve`, `--levels`, `full-campaign-631.script`). Slice 2.
- The passed claimant's return and Ansgar's meeting wait on #633.
- `tuned` needs both partners' cold Fun Gate entries. Code's warm entry is 7/7/5.
