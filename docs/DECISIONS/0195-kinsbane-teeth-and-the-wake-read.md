# 0195 — Kinsbane's teeth and the wake read (#804 slice 1)

Date: 2026-10-03. Built by the chain Builder. The shape is the Table's (round 249: the teeth, `teeth n/5` on the card; rounds 250 and 251: measure the wake first with a `--kinsbane` read shaped like `--heirloom`, the Sim's player taking the hunt). The hunt rule's exact form is Code's lean.

## What is built

- **The teeth.** `Kinsbane.Teeth(fed)` is the Mt step, zero to five; the fifth is the waking. The card reads `Kinsbane: fed 7, teeth 2/5. Power +2. ...` and the feed line adds `, a tooth grows (teeth 2/5)` on the kill that grows one. The protocol already carries `fed` on the stack and `mtBonus` on `hungerFed`, so a client derives the teeth with no new field. The art's six blade states are #589's.
- **The hunt.** The Sim's heuristic player strikes with a hungering weapon that has not woken ahead of any other weapon it carries; among such attacks the score decides as before. Without it the read was empty: the heuristic chose Keziah's gauntlets and fists over a 10-weight scythe, and she fed it 0 times in 5 runs. Only a carrier of a hungering weapon plans differently, so no shipped map's measurement moves (Kinsbane ships only behind the `kinsbane:` sample header).
- **`ironwake-sim --kinsbane [--seeds N]`.** The heuristic plays the whole campaign from map 1, permadeath off, as `--levels` does. It picks Keziah at the raid's camp, issues her the scythe in place of her iron axe, and benches from the back so she fights every map. A map counts won only with her standing where she fought, up to 50 tries (the Recall a player spends). Per map from her first it prints the feed count by its end (p25, p50, p75, with the teeth), the drains and the starved forms; per tooth, the median map it grew on over the runs that won her first map.

## The reading (200 runs, `docs/measurements/kinsbane-804.txt`)

82 runs win Starting Alone (the lesson's known baseline, as in `--levels`). From there:

| Map | Her map | Won | Fed p50 | Teeth p50 |
|---|---|---|---|---|
| 6 the raid | 1 | 82 | 3 | 1 |
| 7 Sallow Grange | 2 | 29 | 6 | 2 |
| 8 Brackwater | 3 | 29 | 7 | 2 |
| 9 the field | 4 | 27 | 13 | 4 |
| 10 the keep | 5 | 5 | 15 | 5 |

The first tooth grows on her first map in 65 of 82. Among the runs that survive, the scythe wakes on the keep at the median, with four teeth by the field.

## What it says about round 251's bars

The bars (first tooth by map 3, waking by map 8) were set while Keziah was on the roster from map 1. Since #633 (0192) she joins at the raid's camp, map 6, and fights five maps at most. The first tooth lands on her first map. The waking misses map 8 by two maps, and 15 kills in three maps is not a feed-schedule fix (2,2,3,4,4 still needs 15). The bars need restating against the branch before the schedule is touched; that is the Table's (round 262). The schedule is unchanged.

Two artefacts of the read: forcing her onto every map costs the heuristic Sallow Grange (53 of 82 runs lose it with her standing), so the late rows rest on 27 runs; and the campaign itself does not issue the scythe yet (she joins with her iron axe), which the next slice does once the bars are restated.

## Not in this slice

The waking gain (round 251's full Move again once a map), the voice lines, the clip's hound, Harrow Weir's choice screen, and the campaign issuing the scythe on the pick. No hand play: nothing a player sees changes but two printed lines.

## Amended (round 263)

The bars are restated against her maps: the first tooth on her first map (met), the waking by the field (map 9) on the median run. The lever is the tooth schedule, lean 2,2,2,3,4 (woken at 13; 2,2,1,3,4 if the next read's median at the field is under 13); the waking gain and the +5 cap stay. A passed Keziah returns at the fourth tooth's count (Fed 9 under that schedule), replacing round 244's Fed 12. The scythe-on-pick slice adds an iron-axe control arm on the same seeds; until it reads, the schedule is provisional, and if the axe clears Sallow far more often than the scythe, Drain comes back to the Table first.
