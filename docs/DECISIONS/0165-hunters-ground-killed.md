# 0165 — Hunter's Ground, a tier 1 lever for the Ranger, killed (#705, slice 5)

Date: 2026-10-02. Issue #705. Follows DECISIONS/0164's open question on tier 1. Code's, measured, reverted.

## Tried

0164's lean was to hold the kit fixed and give the Ranger a reason to stand somewhere before anything takes the Vanguard's lance. The spike: a new effect kind, `ground` (a stat delta while the holder fights from named terrain, read from the terrain the combatant already carries, so the forecast, `threat`, both planners and the resolver read one number), and Hunter's Ground on the Ranger as a class ability: +2 Def and +2 Spd on forest or hill.

## The reading (`--ladder --map <id> --seeds 100`; `docs/measurements/ladder-705.txt`)

| Map | Ranger, 0164 | Ranger, Hunter's Ground | captain absorbed, before and after | tier 1 spread |
|---|---|---|---|---|
| the_tollgate | 57 | 53 | 80, 61 | 21 to 25 |
| the_mill | 63 | 65 | 199, 180 | 20 to 18 |
| brackwater_cut | 66 | 66 | 208, 216 | 19 to 19 |

Every move is inside the bar's noise (about 7 points at 100 seeds). The Ranger's captain absorbed no more with the bonus; on the Tollgate it absorbed less and dealt more (3442 to 4195). The Sim's player does not take a Def bonus as a reason to stand: its tile choice prices exposure, and a bow captain's best-priced tile is out of reach, not in the wood.

## Decided

- **Reverted whole**: the content and the `ground` kind, since no ability ships it (no dead kinds). 0164's precedent: a lever that buys nothing is reverted.
- **Tier 1 is not tunable by stats or by ground** under the Sim's player. The two remaining doors are the Table's (round 229): a verb change (the Vanguard's lance or the Ranger's bow), or a bar that reads tier 1 differently from tier 2.

## Open

- Round 229: which door, and what the bar's 5 points mean at 100 seeds.
