# 0390: the Kin under the hill begins swallowed

Date: 2026-10-09. Issue #1386, slice 3b'. Builds on 0387 (the shard under the hill), 0388 (the re-take at half), 0389 (the Sim's take), and the swallow's stage (issues 1385, 1395; Lotus 2026-10-09, 0371). The issue body says "under the hill: the Kin casts the same Frozen Iron". Engine only, on a sample.

## Decided

- **`swallowed: x,y`** names the boss placement that begins the map already in its second stage (`MapDefinition.Swallowed`, `Swallow.Opening`). The placement must be a boss (`B` line) whose template carries a `swallow` block, and the map must not also carry `shard_race:`, whose boss runs before he swallows. Each refusal names the file, the line and the reason.
- **The placement rule (Code's lean, provisional):** at the battle's start, the boss is put where the keep's swallow leaves a boss. He has the stage's bar, at full HP, with its Def and Res, and he holds his tile if the stage is rooted. Frozen Iron is set to the stage's dose, held a phase if the stage is late. No event fires. The same clock then runs unchanged: Frozen Iron at his side's phase starts, climbing by the stage's step, uncapped, on every unit but him, sworn included, followed by the Kin's heal. With `race`, the turn limit ends nothing while he stands. "The same Frozen Iron" is read literally: one stage, one clock, whether the boss swallows mid-map or starts swallowed.
- **The board's row.** On a `swallowed:` map no swallow event announces the clock, so the board and `threat` print it (`Swallow.Line`): `Hask stands swallowed: Frozen Iron lands for 0 on every unit but him from the enemy phase after next, 3 more each time, then the Kin heals him 2`. The row shows only on such a map. On the keep, the swallow's own event announces the clock, and the row is absent, so no journaled transcript moves.
- **The sample** is `docs/samples/under_the_hill_kin.map`. It is `under_the_hill.map` with `swallowed: 1,4` and the stand-in Kin as `hask_warden`, the only template with a stage. `under_the_hill.map` stays as it was, so the 1386 and 1548 plays and 0389's read still replay on it.

## Read

- **Warm hand play**, seed 1549 (`docs/transcripts/2026-10-09-under_the_hill_kin-1549.*`): the shard was broken on turn 4, and the game was lost on turn 6 when Frozen Iron for 12 killed the captain. The L1 company never reached the L14 stage-2 stand-in, which sits at Def 12. Frozen Iron killed two sworn and finished the archer, and it took the bearer to 6 before the captain's kill.
- **Sim**, `--full docs/samples/under_the_hill_kin.map --seeds 40`: gate 1 is 0/40, with every loss a captain loss and the last on turn 8. The placeholder board is unwinnable by construction: an L1 company against the L14 Warden's second stage. Sizing it is 3c's job (a campaign-level company, a route to the bearer by turn 2, and the Kin's real numbers after Lotus's character pass).

## Not decided here

- Whether breaking the shard should touch Frozen Iron (stop it, slow it, or leave it) or the Kin's heal. As built, it doesn't. The story says the shard is how the Kin re-takes, not how it casts.
- The Kin's own template, name and numbers, which wait on Lotus's character pass. Also its wave (`column N`, rounds 518 to 520) on the hill.
- 3c: the hill as the campaign's map after the keep, the Reseal or Fight card, and Marrit.
