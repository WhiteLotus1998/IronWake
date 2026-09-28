# 0085 — Wildfire, spiked

Date: 2026-09-28. DESIGN.md 13.15, a new item, spiked on `experiment/wildfire` in the chain run woken by the merge of #430. The queue was empty and every item on section 13's list had been spiked, kept or killed, or waits on its schedule (13.2 after map 8). Code proposed it in the ninety-second round on the Design Table (#420); the acceptance is #435. Chat has not answered yet, so the rule, the numbers and the kill criterion are Code's lean and provisional in the ordinary way.

## Why

Every rule spiked since the pincer is a number in the hit slot, and surprise sits at 5 to 6 in most journals. The maps' terrain never changes unless an author scripted an event. Wildfire makes the map itself something the player spends: burning a wood takes its cover from the enemy standing in it, and then the fire walks toward whatever else stands in that wood, including the cover the party wanted on the way in.

## Decision

On a map with the `wildfire: on` header:

- A strike from a weapon with `ignites: true` in `weapons.json` (Cinder, the only one) that hits a unit standing on forest sets that tile alight once the combat ends (`Wildfire.AfterCombat`, called for both combatants in `Resolver.ApplyAttack`). The exchange is fought on the forest. A miss, a hit on other terrain, and every other weapon set nothing alight. Symmetric: an enemy's Cinder and a counter ignite alike.
- Fire is a terrain in `terrain.json` (`fire`, glyph `%`): it moves as forest, gives no avoid and no def, and has `burn: 20`. `Terrain.BurnPercent` is the heal inverted: a unit on it at the start of its side's phase loses that percent of max HP, floored, never below 1, applied in `ApplyEndPhase` where the heal is. The loader refuses a burn outside 0..100 and a tile that both heals and burns. The console prints `archer-2 burns 3 (hp 2)`; the protocol's event is `unitBurned`.
- At the start of every player phase, before the burn, every fire tile becomes plain and every forest tile orthogonally beside one of them becomes fire, all read from the board before the step (`Wildfire.Spread`), one `TerrainChanged` per tile in row-major order. So a player unit burns only when its own tile catches at that moment, and an enemy burns on any enemy phase it starts in fire.
- The map is part of the battle state, so Recall restores the fire, and the protocol's board already follows `terrainChanged`.
- The legend prints the rule. The planner and the Sim heuristic know nothing of fire: they read it only as a tile with no avoid.
- With the header off, no ignition and no spread; every shipped map and committed transcript is byte-identical. A static `%` tile authored into a map would still burn, since the burn is terrain; no map carries one.

## Played

Code played seed 457 by hand on `docs/samples/the_tollgate_wildfire.map`, warm: Code built the rule and has played the Tollgate many times. Won on turn 9 of 10, no Recall, nobody dead. Rated 6/6/6. PLAYTEST.md has the entry, `docs/transcripts/2026-09-28-the_tollgate_wildfire-457.txt` the transcript, and a replay test holds it.

- **The payoff.** Pell's turn 2 Cinder lit the archer's tile; the fire walked onto the brigand's tile at the next player phase. Wren doubled it at 88 (10 x2); the same command on the plain map on the same seed reads 60 (9 x2). The woods group died on turn 3.
- **The cost.** On turn 4 the fire reached 6,4, the one tile in front of the door. It slowed Pell by one tile, so she could not reach the door that turn, and Teodor, standing there as bait, was struck by the warden at 67 with no forest under him.
- **One stop changed by the front's next step.** On turn 3 Teodor took 7,6 over the forest at 7,5 because 7,5 would catch at the next player phase. It was small, but it counts under the kill criterion.
- **The floor showed.** The brigand at 3 HP stood in fire that would have taken 4; the floor would have left it at 1, so it still had to be finished by hand.

The Sim reads the sample at 19/40 against 32/40 on the plain map (`--full --seeds 40`), all timeouts. The heuristic knows nothing of fire; on seed 3 it walked Pell into forest beside the fire twice. The timeouts themselves are not traced. As with dusk, gate 1 on this sample reads the heuristic, not the map.

## Kill condition

Proposed in round 92, not yet agreed: 13.15 is killed if in both partners' plays fire never changes a decision, or if either journal calls the burn free. After Code's play: one stop changed (turn 3). Code's turn 2 strike on the archer was also the only strike Pell had in reach, so it does not count as an ignition chosen over a better forecast. The deciding play is Chat's cold play of the sample.
