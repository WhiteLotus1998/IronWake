# 0391: the hill's board at the campaign's level

Date: 2026-10-09. Issue #1386, slice 3c (the board only). Builds on 0387 (the shard under the hill), 0388 (the re-take at half), 0389 (the Sim's take) and 0390 (the Kin begins swallowed). The acceptance line is Chat's round 548: the hill's board must give the company a route to the bearer by turn 2.

## Decided

- **3c is split.** This slice sizes the board. The campaign's wiring (the hill as the map after the keep on the secret path, the Reseal or Fight card, Marrit leading them down, the endings) is slice 3d, because none of it can be read until a board exists to fight.
- **The board is a sample, `docs/samples/under_the_hill_campaign.map`** (Code's lean, provisional): 16x12, `deploy: all`, enemy level 8 (the campaign keep's), Recall 3, limit 12. The company deploys on the keep's twelve east tiles. The stand-in Kin (`hask_warden`, the only template with a stage) begins swallowed at 4,6, at the mouth of a walled alcove, rooted. The bearer is a guard Sworn Captain at 7,6, asleep, screened by two soldiers at 9,5 and 9,7. An archer and a hexer stand behind the screen, and two brigands come in on the flanks. It stays out of `content/` until 3d gives it a place in the campaign, so `--full --all` and the campaign do not see it.
- **The route line is a test** (`UnderTheHillBoardTests`): from the nearest company slot, the slowest infantry Move (4) walked twice on the terrain alone reaches a tile beside the bearer. Here it is 5. A guard test shows the walk exceeding two Moves on a walled board.
- **Where the Kin stands was screened** (`--finale`, 40 seeds, then 200 for the two closest): at 1,6 (the far wall) full 20/40 and depleted 11/40; at 3,6 full 161/200 and depleted 106/200; at 4,6 full 164/200 and depleted 122/200; at 5,6 (open floor) full 36/40 and depleted 30/40. 4,6 passes both gates and sits near the keep's own 127/107. Kept.

## Read

- **Sim**, `--finale docs/samples/under_the_hill_campaign.map --seeds 200` (`docs/measurements/under_the_hill_campaign-1386.txt`): full 164/200, depleted 122/200, floor 17/200, median 5 to 7 turns, finale ok. Every loss is Frozen Iron's. The finale's lines are worded for the keep, so a captain killed by Frozen Iron before the limit prints as a `timeout` and under "before the swallow". The count is right and the words are the keep's.
- **Warm hand play**, seed 1550, the full company at L8 (`docs/transcripts/2026-10-09-under_the_hill_campaign-1550.*`): the shard was broken on turn 2, the route line held, and the game was lost on turn 5. Pell fell on turn 1 to an archer I walked her into. Without Pell, nobody could hurt the Kin: the captain's forecast was 3 a hit against Def 12, and the Kin heals 2 a phase.

## Not decided here

- Whether breaking the shard should touch Frozen Iron (0390's open question). Neither the play nor the Sim needed it to.
- The Kin's own template and numbers, which wait on Lotus's character pass. The Kin's wave (`column N`) on the hill.
- Tuning, which waits on #1453 and a cold chair.

## Next

- Slice 3d: the hill as the campaign's map after the keep on the secret path, the Reseal or Fight card, and Marrit.
