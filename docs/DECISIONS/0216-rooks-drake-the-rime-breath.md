# 0216 — Rook's drake: the rime breath spike (#805 slice 3)

Date: 2026-10-03. Built by the chain Builder. The shape is the issue's ("once a map, a short line that chills both sides and turns Water into Rime ice that wears back to Water", rounds 245 and 246); the verb, the numbers and the thaw were the Builder's to propose (review on #805). Provisional, a spike behind a header on a sample.

## What is built

- **The command.** `breathe <rider> <x,y>` (`Breathe`, protocol `breathe`), on a map with the `breath: <rider>` header. The rider's drake must be Unbroken. Once a map (`BattleUnit.Breathed`, never cleared), as the rider's action, after a move or without one; no Canto follows. The x,y is the orthogonally adjacent tile that names the direction. The line is 3 tiles straight out from the rider, stopping at the map's edge.
- **The chill.** Every unit on the line, of either side, is chilled: the frozen-iron chill (issue 702), Mov -1 never below 1, on its clock, with its card line. The breath does no damage and rolls nothing.
- **The ice.** Every Water tile on the line turns to Rime ice (`rime`, glyph `&`; foot and flyer 1, horse and armour 2; no avoid). Its new terrain field `thawsTo: water` puts it on a clock (`BattleState.Rime`, counted as the chill's clock): it holds through the enemy phase and the breather's next phase, and thaws as that phase ends. A tile with a unit on it at the thaw holds, and thaws at the first phase change that finds it empty. Nobody is ever left standing on water.
- **On screen.** The board prints the verb while it is ready, and the ice with when it thaws (`rime: 6,5 6,6 6,7 (thaws as the next player phase ends)`). The terrain card has a `Thaws:` sentence. The protocol carries the command, `breathed`, the unit's `breathed`, and the state's `rime`.
- **Client.** Both palettes, LOOK.md (`#6090C0`, the only frost-blue that clears every terrain and side separation), the art spec, the generated tile and the sheet for Lotus carry the new terrain.
- **Blind spots, as with the carry and the rock.** The enemy never breathes. The planner, `Resolver.Legal`, the random player and the Sim never offer it. `threat` reads the chilled Mov and the ice like any board, and both sides path over the ice while it holds.

## Choices made here (provisional)

- **A clock, not feet.** The issue named 13.25's `wearsTo` machinery. Under it, one foot crossing turns each ice tile back to Water. A line across a river would then carry exactly one unit, which makes the breath a worse carry. On the clock the ice is a causeway for a round that both sides may use, and #872's Deep rime is exactly one more round on it. Planks keep their walk rule; the ice uses its own field.
- **3 tiles.** That is the width of the sample's river. A breath along a river freezes three separate footholds; a breath across it freezes one crossing.
- **No damage.** The cost under 0099 is the action, the once, and the line's indifference to sides. The Sim's ceiling has no damage to read.

## The first play (Code 805 warm, PLAYTEST)

Lost on turn 8 with one Recall. The breath froze 6,5 to 6,7 on turn 1, and the company crossed into it single file. On turn 2 I corked the causeway's north end with Teodor instead of running the captain over. The cork held one melee at a time, and the bridge group left the bridge to come around by the south. Then the thaw did what the rule says: each tile I let go of became Water. With the causeway gone, the north bank's archers and soldier walked to the bridge and filled it, and the captain, still on the south bank, had no way over. The route was taken for the breath, which meets the keep clause at the warm chair. The thaw clock decided the game: it closed the crossing behind the half of the company I had left on the wrong side. The chill caught nobody.

## Next

- Chat's cold play of `docs/samples/kestrow_water_rime.map` decides the breath. It is killed if no strike or route is taken for it.
- #872 (the Drover) can build Deep rime on `RimeTile.Clock`.
