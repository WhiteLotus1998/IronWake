# 0161 — The campaign's enemy-level curve, the template swaps and the gate at level 7 (#704, slice 4)

Date: 2026-10-02. Issue #704 (Lotus's progression batch, item 1); the Table's rounds 222 to 226 on #665. This record restates what the Table agreed and names the two places the build bent it under the Table's own rules. Provisional until Chat's campaign play through Brackwater.

## Agreed on the Table (restated)

- **The second tier's gate is level 7 with rank C** (round 225, 226), in place of 10. The captain's ladder (0160) keeps its forms at 10; nothing on the Table moved them.
- **A campaign-only enemy-level curve** in `campaign.json` (round 223): a map's `enemyLevel` replaces its file's level in the campaign only. The standalone maps, their gates, `--smoke` and the EXP formula are unchanged; a difficulty's offset is added on top.
- **Templates through `campaign.json`, never through a `.map` file** (round 226): a campaign-only `swap` object, `"x,y": "<template>"`, fields that template on the tile's enemy placement, keeping its group, behaviour and boss flag. At most one on Sallow Grange; the rest on Brackwater Cut and the keep.
- **The Sim reads only the top unit** (round 225): on `--levels`, the highest unit's p50 is at the gate at the camp before Brackwater, with the `meets` column beside it. `meets` tests each form as if the unit stood in its base class (the heuristic never certifies), so a 0 there means rank C is the wall.
- **A tuned map's curve point bends, never its board** (round 225): if its campaign gate 1 falls under 60, its curve point comes down. On Brackwater the template count comes down first, its curve point second (round 226).

## Decided in the build

- **The print.** `--curve [--seeds N] [--map <id>]` prints gate 1 per campaign map three ways: as the file reads; as the campaign fights it (the curve and the swaps) with the file's party; and with that party raised by as many levels as the curve raised the enemy (`party +N`). The second line puts a starting party against level-8 enemies and reads near 0 on every raised map, so it cannot be what the 60 is read against; the `party +N` line is the proxy for the company a campaign player brings, and the bends below read it.
- **Brackwater Cut: two templates, not four.** With four (two Heavy Riders on the chase, a Longbowman at 11,1, a Sentry at 17,5) its `party +5` gate 1 was 99/200 (49 percent). With the two Heavy Riders alone it is 140/200 (70 percent); with none, 145/200. The count came down to the riders, per round 226's order, and the curve point stays 8.
- **Harrow Weir: its point bent back to its file's 2.** At 4 its `party +2` gate 1 was 109/200 (55 percent), at 3 `party +1` 103/200 (52 percent), both under 60; the file's own 123/200 (61 percent) is the board we rated. Per round 225 the point comes down; it comes all the way down because one level lower did not clear 60.
- **The raid: 6, one above the Table's 5.** Bending Harrow back cost the top unit a level: at the camp before Brackwater its p50 fell from 7 to 6. The raid is never tuned (round 158), so it carries the level back: at 6 its `party +4` gate 1 is 180/200 and the top unit's p50 is 7 again. The curve that ships is **1, 1, 2, 3, 2, 6, 7, 8, 8**.
- **Sallow Grange: one Veteran** at 7,5, the first of the field group, as the first sight of what is coming. **The keep's van: a Veteran, a Marauder and a Longbowman** (6,4, 6,7, 4,5). The keep is not tuned and its acceptance is play.
- **Journaled campaigns replay on content without the curve** (`Fixture.WithoutCurve`, `CurveFreeContentDirectory`): a transcript is a record of the build it was played on.

## Measured (200 seeds; `docs/measurements/levels-704-curve.txt`, `docs/measurements/curve-704.txt`)

- `--levels` on the shipped curve: at the camp before Brackwater (after map 7) the highest unit is p25 6, p50 7, p75 7, so the acceptance holds; `meets` is 0 at p75 there, so rank C is the wall until the keep. After Brackwater the highest p50 is 7, after the keep 9. 118 of 200 runs never win Starting Alone (the heuristic alone with the captain); of the 82 that reach the keep, 70 lose it on every try.

## Open

- Sallow Grange at 7 with its Veteran reads 64/200 (32 percent) on `party +4`, against 157/200 in its file. It is not tuned, so no rule bends it; the heuristic's real company there is one unit at 6 or 7 and the rest at 1 or 2, harder than the proxy. Its curve point is the first lever if a chair finds the map a wall.
- The keep at 8 with three templates is the hardest board in the campaign for the heuristic (12 of 82 won). Its acceptance is play; Chat's campaign run rates it.
- Whether the captain's forms (0160) move to 7 with the rest.
- Whether rank C at 80 points is the real wall; `meets` says so on the heuristic, a chair confirms.
