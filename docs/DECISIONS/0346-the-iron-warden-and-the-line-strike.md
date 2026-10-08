# 0346 — The Iron Warden and the line strike are built; the pitched stage 1 waits on a sample

Date: 2026-10-08. Issue #1384; Design Table #1368, Lotus's Hask rework (relayed 11:38Z), Chat's round 487, Code's round 488. Provisional: every number and name is on Lotus's list (#1247).

## Built

- **The class.** `iron_warden`, "Iron Warden" (placeholder; "Warden" is the faith class), `enemy: true`, armored, Mov 4, lance only, no modifiers, so a template's card reads its own numbers.
- **The line strike** (`line_strike`, reach 4; command `StrikeLine`, event `LineStruck`). His action, after a move or without one, out through a tile orthogonally beside him. It strikes every unit of another side on up to 4 tiles of the line, nearest first, once each, at the equipped lance's numbers read as if adjacent: one keyed roll each, no double, no counter. Code's leans, not in the round: the line stops at the map's edge and before a wall (water and the rest pass it), his own side is passed over (Frozen Iron is the both-sides spell; the lance is not), it spends no use and earns nothing, and a line with no foe on it is refused.
- **The planner** strikes the line catching the most units it knows, at least two, from any tile its plain plan may strike from; ties go to the most expected damage, then reach order and north, east, south, west. A boss under the veto strikes only from a tile the veto passes. Otherwise it swings plainly.
- **`threat <unit>`** prints the line the planner would strike through the unit on the asked tile, with its acc and damage, as its own line, outside the seated total (the total seats one striker per tile beside the target, and the line has no such tile).

## Measured (`docs/measurements/keep-1384.txt`, `--finale`, level 8, 200 seeds)

| Keep | full | depleted | floor |
|---|---|---|---|
| main: stand-in Hask (L10, HP 44, Str 9, Def 9, Res 7) | 167 | 136 | 0 |
| the line strike on the stand-in numbers | 168 | 96 (FAILED) | 2 |
| the pitched numbers, no strike | 0 | 12 | 0 |
| the pitched Warden (numbers and strike) | 0 | 0 | 0 |
| the same at Def 11 / Def 12 | 9 / 2 | 0 / 0 | 0 / 0 |
| the pitched Warden, story members at level 10 | 4 | 0 | 0 |

The numbers are the wall, not the strike: the strike alone costs the depleted party 40 wins and the full party none; the pitched numbers alone take the full party from 167 to 0. Def is not the one lever (11 and 12 stay under 5 percent), and two more levels in the party do not open it.

## Decision

- The engine ships: the class, the ability, the command, the planner and `threat`, with the ladder tests.
- **The campaign keep keeps the stand-in Hask** (`hask`, Bulwark, unchanged) until a stage 1 passes `--finale`. The pitched stage 1 is `hask_warden` (L14, HP 52, Str 13, Dex 16, Spd 9, Def 14, Res 11, lance A, the Warden's Lance) on `docs/samples/ironwake_keep_warden.map`, which is the campaign keep with him in the `lord` spawn. A test pins the campaign's Hask as the stand-in, so the swap is a deliberate edit to that test.
- This departs from #1384's "replaces the stand-in lord's numbers"; Lotus's own instruction was that the Sim gate applies as usual, and it reads 0 of 200. The next lever is the Table's: the numbers to try (Str and HP before Def: with the pitched numbers and no strike, 196 of the full party's 198 losses are not the clock), and whether the line strike's 40-win cost to the depleted party is the price of a final boss.
