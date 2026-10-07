# 0309 — Lightning Rod catches a lightning spell aimed near it

Date: 2026-10-07. Issue #1280; Lotus's spell rulings (0307, relayed on #1251: "Catchrod becomes Lightning Rod, a passive that works on lightning spells only"). Provisional: the reading goes back to Lotus with the revised #1247.

## Context

Catchrod was an action: until the holder's next phase, the next enemy spell aimed at an ally within 2 struck the holder instead. Lotus made it a passive and limited it to lightning. The engine already had one target swap, 13.19's cover, read by the resolver, the forecast, `threat` and the planner; the rod takes the same shape.

## Decision

- A new ability effect kind `rod` (`school`, `radius`); `lightning_rod` in abilities.json is `{ "kind": "rod", "school": "lightning", "radius": 2 }`. No class carries it until Lotus signs the storm-warden's kit.
- Always on. An attack with a tome of the rod's school, aimed at a unit of the holder's side (the holder itself aside) within the radius, strikes the holder instead (`rodCaught`), and the holder counters as any struck unit does, from where it stands.
- The tome must reach the holder from the caster's tile. A bolt that cannot reach the holder passes to the aimed unit. The combat math needs a real distance for both the strike and the counter, and a spell striking a unit it cannot reach would be a second, unpriced kind of range.
- A stunned holder catches nothing; a fallen one is off the board. Of two holders, the nearer to the aimed unit catches, then unit order.
- `LightningRod.Catcher` is the one rule. The forecast is against the holder and names it (`(Lightning Rod: strikes <holder>)`), the planner scores a caught spell against the holder so it aims elsewhere or strikes the holder knowingly, and a caught `threat` line prints who it strikes and stays out of the unit's total. A cover swap is read after the rod, on whoever is struck.

## Kill criterion

If Lotus meant a buff to the holder's own lightning spells, this becomes a loader and ability change, not a rewrite. If the first storm-warden play shows enemy casters simply never aim near the holder, so the rod never fires, the radius or the reach clause is the lever, not the passive.
