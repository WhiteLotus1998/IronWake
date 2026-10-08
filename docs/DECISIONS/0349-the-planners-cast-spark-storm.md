# 0349 — The planners cast Spark Storm

Date: 2026-10-08. Issue #1391, from #1329 slice 3 (DECISIONS/0348). Implementation; the cast rule is Code's lean from the issue, open to the Table.

## What is offered

- **`Resolver.Legal` offers an area cast** (`UseItem(unit, slot, "x,y")`) from where the unit stands. It offers one per set of enemies struck, naming the first tile in row order that strikes that set, among the tiles in range its side can see. It offers nothing with no use left, with no wield, or with no enemy in the area. Every offer is accepted by the Item action.

## How a cast is priced (`AreaCast.Best`, `AreaCast.Price`)

- **On `EnemyAi.Score`'s scale**, so a cast and an attack compare directly:
  - for each one struck: the kill bonus (100) when the forecast's damage, a cashed mark's when it cashes one, takes its HP, plus the expected damage capped at its HP;
  - for a marking tome, each survivor adds the expected x1.5 share on that damage;
  - `NoCounterBonus` (10) once.
- **It qualifies only when it strikes two or more, or kills one.** A single sting never stands in for an attack.
- Ties go to the first tile stood on, then the first tile cast at, in row order.

## Who casts

- **The Sim's player** casts when the best qualifying cast outscores its best attack, and a hunt still wins.
  - It casts at the enemies its side can see, never from a tile left free for a corked captain.
  - For a unit whose death loses the map, it casts only from a tile whose no-crit exposure stays under its HP (the veto's rule).
- **The enemy planner** casts after a line strike, over any attack the cast outscores, under the boss veto.
  - A caster with no weapon to equip casts or waits.
  - No shipped enemy carries an area tome yet, so this changes no shipped play. It is ready for the storm caster's placement (#1286).
- **The campaign script's writer** (`CampaignScript.WriterPlayer`) casts nothing. Its script is played back through the client's clicks, which take no area cast until #1392. #1392 turns it on and regenerates the parity scripts.

## What the Sim reads

`--full --all`, 200 seeds, main at b7167e4 against this change (gate 1 wins; gate 4 median drop):

| Map | main | cast |
|---|---|---|
| brackwater_cut | 133; 0.235 | 135; 0.255 |
| harrow_weir | 127; 0.265 | 170; 0.225 |
| sallow_grange | 157; 0.370 | 171; 0.385, fails on Wren (0.040, was 0.120) |
| the_tollgate | 158; 0.340 | 167; 0.315 |
| the_field | 101; 0.240 | 93; 0.065, **fails** |
| old_mill_road, saltmarsh_ford, starting_alone, the_mill | 57, 50, 1, 107 | unchanged |

- **The field's gate 4 now fails.** With the cast on, benching Ottilie (drop -0.285) or Keziah (-0.085) wins more games than fielding them. That is measured, not traced. The field stays `tuned` on its hand plays (0233, 0250).
- **The tripwire:** a trace of a field seed that the cast loses and main wins. That trace decides whether a cast rule changes (for example, no cast that wakes a group) or the gate reading does. It goes to the Table before any lever.
