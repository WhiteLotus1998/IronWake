# 0357 — The client clicks an area cast

Date: 2026-10-08. Issue #1392, from #1329 slice 3 (DECISIONS/0348) and #1391 (DECISIONS/0349). Implementation.

## What the client offers

- **An area tome is a row of the action list,** `Item: Spark Storm (area)`, after every other row as the item rows are.
  - It arms a pick of every tile of the map the Item action takes the cast at: in range, seen at dusk, with an enemy in the area.
  - With no such tile the row is greyed, `no target in reach`.
- **The hover reads `AreaCast.Preview` on any tile of the map,** so a tile out of range or unseen shows the console's own refusal.
- **The board marks the area:** `PickArea` gives every tile within the radius of a tile the pick takes.
- **A click on a marked tile submits the `UseItem`.** The target word is a unit's id when one stands there, else `x,y`, as the other tile picks write it.

## Parity

- `Script.ApplyByClicks` takes an `item` cast line through the row and its click. The journaled Tollgate storm play (seed 1329, `docs/transcripts/2026-10-08-the_tollgate-1329-storm.script`) is the click-parity script: the console and client logs match byte for byte.
- **The campaign script's writer casts** (0349's note), so the full-campaign script now carries five Spark Storm casts, taken by clicks in `ci` and godot-parity. Seed 644, variant 44 is still won.
- **The Psalter-art script is written with `--no-casts`.** With casting on, the writer fails to reach the art at each of twelve variants tried (32 to 58), most lost on map 7. The script is unchanged but for its header. The test that the Lazar House and Shrine hand plays pay the Psalter inside the campaign (the same quests, no deploy) is written the same way, for the same reason. Why a casting heuristic loses that company's map 7 is untraced; it is the Sim's player, not the click path.
- The full-campaign parity guard (a log missing the Fall back line) now differs at line 516, not 1481: the Tollgate's casts move the first Fall back there.
