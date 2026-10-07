# 0308 — Burn stacks, its tick reads Res, and the ember cashes it out

Date: 2026-10-07. Issue #1279; Lotus's spell rulings (0307, relayed on #1251). Provisional: the numbers are Code's leans from 0307, argued on the PR.

## Context

0264 and 0297 shipped fire's burn as "refreshed, never stacked". Lotus replaced Thatchlight with Smolder, a burn scaled by the target's Res, and kept Last Ember only if burn can build high. So burn has to stack, and a tome has to cash it out.

## Decision

- A burning hit lays a stack, up to the burn rider's `cap` (rules.json, 4 when omitted; fire names 4). `BattleUnit.BurnStacks` holds the count; `Burn` stays the per-stack amount.
- One phase count, refreshed by each new stack, not a clock per stack. One count prints as one line, `burn 3 (2 stacks, 2 phases)`, on the card and in the forecast, and what a burn still owes is just the tick times the phases left. A clock per stack would need a list on the unit and a card line per stack, for a difference a player would rarely see.
- The tick is `max(1, stacks * amount - Res / 2)`, the HP floor of 1 unchanged, and still the larger of it and tile fire, once.
- The cash-out is a new rider kind, `ember`, that only a tome names, on a school whose rider is `burn`. It reads the burn rider's gate. A school's own rider may not be `ember`, so rules.json keeps one rider per school.
- An ember hit on a burning unit that survives deals what the burn owes at once, never below 1 HP, and clears it (`burnCashed`). On a unit not burning it does nothing, and the forecast says `cashes no burn`.
- The planner prices the ticks a new stack adds to what is owed (nothing at the cap) and what an ember cashes.
- No shipped tome names burn or ember, so play is unchanged until Lotus signs the revised #1247.

## Kill criterion

If the first fire grimoire signed and played shows stacks never pass 2 in a map, the cap is scenery, and the Table should look at the phase count before the cap. If a cash-out at the 1 HP floor reads as a wasted swing in a journal, revisit whether an ember can kill (it needs a death outside combat, which no path has today).
