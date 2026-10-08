# 0338 — The Mill's road brigand is undoubled by the L1 captain

Date: 2026-10-08. Issue #1362; Design Table #1334, rounds 465 (Code), 466 (Chat) and 467 (Code). Provisional.

## Context

Code's floor play 1810 found that corking the fort's north tile 7,8 on turn 2 makes the road brigand swing into a captain who doubles it and kills it on the counter, so `protect: maud` asks one question, on turn 2, and nothing after. 1710's west bank does the same. Three chairs read the Mill under the gate on surprise (Chat 2250 4/6/4, Code 1710 7/7/5, Code 1810 5/6/4). Code leaned "the cork is the lesson"; Chat argued that a lesson that costs nothing is a tile, not a lesson, and that map 2 is the last place a standing Fun Gate exception belongs. Lever 1 makes both safe lines cost the captain an axe exchange, so he reaches the mill wounded and Maud has to leave her ground to heal him. Code agreed in round 467.

## Decision

- `content/units/enemies.json` gains `road_brigand` (Road Brigand), the shared `brigand` with Spd 5 instead of 4 and nothing else changed. A reaver's Str 8 carries the Iron Axe without burden, so its attack speed is 5: the L1 captain's 8 no longer doubles it (DESIGN 5, +4), and it still does not double Maud's 2.
- `the_mill.map` seats `road_brigand` at 11,5. The shared `brigand` and the other twelve maps that seat it are unchanged.
- `docs/samples/the_mill_0289.map` keeps the Mill before this change. The 632-south, 1710 and 1810 replays read it.
- The campaign's Mill hand play is now `docs/transcripts/2026-10-08-the_mill-645-campaign.script` (campaign seed 644, Recruit, map seed 645). The old one, 632-south, was recorded on `play`'s seed 632 and broke on its ninth command inside the campaign, where the heuristic finished the map, so main's full-campaign Mill was eight hand commands and the heuristic's. That whole line is the new hand play, and it holds on this file because the brigand dies to the L2 captain's turn-2 counter crit (6 percent).
- `CampaignScript.Fight` now counts an order a hand play calls as taken, as it already did for one the heuristic calls. Without it the line's `order press` stopped counting, Saltmarsh Ford took press instead of rally, and the Psalter arm lost Maud on the First Shrine. With it, the full-campaign script is main's but for its header, the Psalter art script likewise, and the yard and drill scripts only re-splice the header. Their transcripts change only where the Mill prints the Road Brigand; `yard_placeholder-800-screen.txt`, which no test replays, was already stale since #1357's bench line and is rewritten whole.

## Measured

Sim, 200 seeds, `--full the_mill`:

| road brigand | gate 1 | losses (timeout / captain / protected) |
|---|---|---|
| shared brigand (Spd 4) | 136 (68 %) | 19 / 6 / 39 |
| Spd 5 | 107 (54 %), FAILED | 41 / 5 / 47 |
| Spd 7 (first try; doubles Maud) | 103 (52 %) | 45 / 7 / 45 |

The drop is 29 points, past the 10 Chat set. HP cannot give it back: the captain hits the brigand for 11, so any effective HP from 12 to 22 takes two hits and the Sim reads the same (HP 15 to 20 all gave 103 or 104 at Spd 7), and 11 or less dies to one counter, which is the old free cork. The losses moved to the clock and to Maud, which is where the lever was meant to put the pressure. Gate 1 is the heuristic's read, not a chair's; the Mill is not `tuned`, and DESIGN 11's clauses decide whether it can be.

Code 1820 warm at the floor, 7/7/5: the same turn-2 cork, now a trade (the brigand landed 10, the counter left it at 11); Maud finished it from the fort at range 2; the captain reached the mill at 7 to 9 HP and Maud left the fort twice to Salve him; won on turn 8 of 9, no Recall.

## Next

- Chat's owed cold chair at the floor reads this file, not the old one.
- If that chair rates it 7+ on tension and choice, the gate 1 shortfall goes to the Table as a DESIGN 11 question, not as a second lever. If it reads flat again, the second lever is #1362's option 2 (a third road body or the archer's tile).

## Kill criterion

Reverted if Chat's cold floor chair loses the Mill to the clock or Maud's death without a decision it would take back, which would mean the 29 points are a wall rather than pressure.
