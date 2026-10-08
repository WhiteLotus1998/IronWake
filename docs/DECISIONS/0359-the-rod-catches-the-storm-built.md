# 0359 — The rod catches the storm: built

Date: 2026-10-08. Issue #1400. Builds DECISIONS/0351 (Lotus's ruling and the partners' lean from round 498); restates them, and records the build's readings.

## Built

- `AreaCast.Catcher`: a holder of a rod of the tome's school, not stunned, that the tome reaches from the caster's tile, catches a cast whose area takes in a unit of its side, itself aside, within the rod's radius of it. Of two, the nearer to the aimed tile, then unit order.
- A caught cast strikes the holder alone, once, at x0.5 (`Catching`), read at the caster's distance to him, no counter; `areaCastAt.struck` lists him alone and `rodCaught.aimed` names the struck ally that drew the catch. He is charged if he stands.
- A catch marks no one, on the single-target path as well (`Mark.AfterCombat`'s `caughtId`); a mark he carries is cashed as usual.
- The preview names the holder and prints his one strike with ` (caught x0.5)`. `AreaCast.Price` prices the one caught strike with no mark share, and `Best` reads the catch from the board, so a planner casts a storm a rod would catch only when the caught strike kills.

## Readings (Code's, reversible)

- **The holder need not stand in the area.** The trigger is an ally of his in the area within 2 of him, the issue's and 0351's wording; a holder one tile outside the storm still steps into it. If he is in the area with an ally, he is hit once (0351).
- **No `threat` row.** `threat` lists no enemy area cast today, caught or not, so a caught storm has no line to name. A storm row in `threat` is separate scope.
- No content unit carries the rod, so no Sim cell or transcript moves.
