# 0176 — The curve's bend reads `carried`, not `party +N` (#764)

Date: 2026-10-02. Issue #764, from the Table's rounds 233 and 234 (#731). The rule was agreed by both partners; this record restates it, names the Builder's implementation choices (provisional), and records the first read. Amends 0161's "the bends below read it".

## Decided (restated from round 234)

- **0161's bend reads `carried`.** `party +N` raises every member by the curve's raise; no company that reached Brackwater looks like that (the Sim's top 6 and median 1, Chat's two chairs 13 and 18 summed levels against the proxy's 30). `party +N` stays printed for comparison.
- **The read picks the lever, never the board:** `carried` under 60 and `spread` at 60 or over means distribution (multiplier or feeding, the point holds); both under 60 means the total is short (the point steps 8 to 7, then 6); both at 60 or over means the point is fair; `carried` near 0 everywhere means the chooser hoards (an EXP-share term in the player's score).

## Builder's choices (provisional)

- `carried`: per campaign map, `LevelRun`'s campaign (the heuristic, permadeath off, as `--levels`) gives each won battle's deployed levels as it began (`Camp.Deployed`, issue 738's camp line). Sorted highest first, each place reads its p50 over the runs that won the map; a run that deployed fewer units does not vote on the lower places. The count of those runs prints after `over`.
- The file's party takes the places captain first, then in placement order (every cast member is level 1 in its file, so this only names who carries the top level; in the Sim's campaign that is the captain). Levels only rise (`Unit.AtLevel`, average growth); a place under a unit's own level keeps it.
- `carried, spread`: the same total over the same places, the remainder one level each to the top places.
- Both lines print on every campaign map, including the ones the campaign fights as the file reads.

## The first read (`docs/measurements/curve-764.txt`, 200 seeds, 82 runs reach map 2)

| Map | file | campaign | party +N | carried | spread |
|---|---|---|---|---|---|
| the_tollgate (tuned, point 3) | 79 | = file | - | 81 (3/1/1/1) | 79 |
| harrow_weir (tuned, point 2) | 71 | = file | - | 90 (4/1/1/1/1/1) | 72 |
| brackwater_cut (tuned, point 8) | 65 | 5 | 65 | 34 (6/3/1/1/1) | 9 |
| ironwake_raid (not tuned) | 92 | 37 | 90 | 41 | 36 |
| sallow_grange (not tuned) | 81 | 9 | 36 | 42 | 57 |
| ironwake_keep (play decides) | 73 | 3 | 34 | 6 (over 11) | 3 |

- **Brackwater at 8: both under 60.** By round 234 the total is short: the point steps 8 to 7, then to 6 if 7 is still under. Chat's prediction (`spread` stays under 60) holds. No tuning in this issue; the step is its own issue.
- **The Tollgate and Harrow are fair** at their points: both lines at 60 or over.
- `carried` is not near 0 anywhere, so the hoarding fix is not called.
- Spread reads lower than carried on every raised map: for the heuristic, one strong unit is worth more than the same levels spread, the opposite of the feeding lever's premise. A note for the Table, not a rule.
