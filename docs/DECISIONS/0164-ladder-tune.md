# 0164 — The captain's ladder, first tuning pass (#705, slice 4)

Date: 2026-10-02. Issue #705. DECISIONS/0163 set the bar and its first reading. The numbers are Code's; provisional, content only, no rule changes.

## Decided

- **The budget is what the cast's captain can use.** He is a sword fighter with Mag 0 (growth 10), so a Mag modifier on a captain's class is dead weight at the level it certifies. Each class's modifiers are now stats he swings or stands with; the Lore classes keep their Mag growth.
- **The modifiers, before and after:**

| Class | Before | After |
|---|---|---|
| Vanguard | +2 HP, +1 Str, +2 Def | +1 Def |
| Marshal | +2 Mag, +1 Res, +2 Cha | +1 Str, +1 Spd, +1 Res, +2 Cha |
| Ranger | +2 Dex, +1 Spd | unchanged |
| Champion | +4 HP, +3 Str, +4 Def | +2 HP, +2 Str, +2 Def |
| Commander | +1 Str, +3 Mag, +2 Res, +3 Cha | +1 Str, +2 Spd, +2 Res, +3 Cha |
| Pathfinder | +1 Str, +4 Dex, +3 Spd | +1 Str, +3 Dex, +2 Spd |

  Growth modifiers are unchanged, so the Vanguard still grows into the sturdy one.

## The reading (`--ladder --seeds 100`, the Tollgate, the Mill, Brackwater; `docs/measurements/ladder-705.txt`)

| Map | Tier 1 before | Tier 1 after | Tier 2 before | Tier 2 after |
|---|---|---|---|---|
| the_tollgate | V78 M73 R57, spread 21 | V78 M71 R57, spread 21 | spread 8, Commander loses gate 4 | Ch98 Co98 P92, spread 6, Commander loses gate 4 |
| the_mill | V85 M46 R63, spread 39 | V83 M66 R63, spread 20 | spread 4, ok | spread 4, ok |
| brackwater_cut | V97 M39 R66, spread 58 | V64 M47 R66, spread 19 | spread 0, the Champion and the Pathfinder lose gate 4 (60 seeds, after 0163's Escape exemption) | spread 3, ok (the Pathfinder's gate 4 fails, as the unpromoted captain's does, so it is not held) |

- Tier 2 meets the bar on the Mill and Brackwater; the Champion no longer carries Brackwater alone (gate 4 ok).
- Tier 1 still misses, by about 20 points on each map, and the stats are no longer the gap. A lever that bought nothing was reverted: +1 Str on the Ranger left all three maps where they were (60 seeds). The Vanguard reads 78 on the Tollgate and 83 on the Mill at +1 Def alone, so its lead comes from the lance it carries (a second weapon against a sword-heavy roster) and from standing in front. The Ranger reads below the unpromoted captain on the Tollgate (57 against 70), and its captain absorbs a third of what the others do: the Sim's player shoots from range and gives up the anvil. That is behaviour, not a number.

## Open

- **Tier 1 is a verb question, and that belongs to the Table.** Either the Vanguard's lance or the Ranger's bow line has to change, or the bar has to admit that a class's line reads differently under the Sim's player. Code's lean: hold the kit fixed and give the Ranger a reason to stand somewhere (a forest or hill bonus at tier 1, as Trail Sense is at tier 2) before anything takes the Vanguard's lance.
- **The bar's resolution.** At 100 seeds the sampling error on a difference between two gate 1 rates is up to about 7 points (less where the shared seeds correlate), which is wider than the 5-point bar. A pass or a miss inside about 7 points is noise. A tighter read needs 400 seeds, about four times the run time.
- The Commander's gate 4 on the Tollgate (it carries: 5009 damage against the cadet's 2139).
- Harrow Weir, Saltmarsh, Sallow, Starting Alone, Old Mill Road and the side maps are still unread.
