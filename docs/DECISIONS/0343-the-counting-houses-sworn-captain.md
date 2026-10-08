# 0343 — The Counting House's Sworn Captain at Def 5

Date: 2026-10-08. Issue #1375; Design Table #1368, rounds 481 (Code), 482 (Chat) and 484 (Code). Provisional.

## Context

Five chairs have played Ottilie's quest 1 at limit 11, and only the Lore ally won it: Teodor and Wren (980) lost, Chat's Pell (9311) won, Maud (2030) and Rook (1380) lost with the Sworn Captain unhit at 27/27. At Def 7 Ottilie does 4 a shot to him and Rook 5, so with any ally but Pell he arrives as an unanswerable last act. Round 441 had said "no third lever", about the clock. Chat ruled in round 482 that 441 does not cover who can win, and agreed the lever with one addition: the card still names him as armoured.

## Decision

- `content/units/enemies.json` gains `house_captain`, named Sworn Captain: the shared `sworn_captain` with base Def 4 instead of 6 and nothing else changed. On the board at the map's level he reads Def 5 (was 7).
- `the_counting_house.map` seats `house_captain` at 11,1. The shared Sworn Captain on the Old Watch, the Warden's Gate, the Oath Stone and the field is unchanged.
- The quest card adds: "The Sworn Captain on the house fort is armoured: leave time for him."
- `docs/samples/the_counting_house_1375.map` keeps the map before this change. The 980 and 980-931 replays read it through `Fixture.CountingHouseDefSevenContentDirectory` (the old map and the old card); they diverge on the new one. The 2030 and 1380 replays never strike him and replay unchanged on the new map, their transcripts rewritten by `tools/rejournal.py` for the new unit id and the card.

## Measured

Sim, 200 seeds, `--full content/quests/the_counting_house.map`: 0/200 before and 0/200 after, all on the clock. The Sim plays the raw map with the captain and one recruit, not the campaign's Ottilie and her ally, so it cannot read this lever; the hand chairs are the measure.

Damage on the board, Def 7 to Def 5: Ottilie 4 x2 to 6 x2 at range 2 (no counter), Teodor's Long Thrust 5 to 7 (no counter), Teodor's lance 8 to 10, Rook 5 x2 to 7 x2.

Code 1375 warm with Teodor, 7/6/5: both arrivals held off, lost on turn 10 when the hexer and the archer killed Ottilie and Teodor; on turn 10 the two took him from 27 to 8 with no counter. At Def 7 the same two strikes leave him at 14. The loss was the bridge mouth (the hexer and the lector double Teodor through 5,7), not the captain.

## Kill criterion

Unchanged from #1375: if a cold chair with a non-Lore ally on this file still loses with the captain unhit, the lever goes back and the card says plainly that the map wants a caster.
