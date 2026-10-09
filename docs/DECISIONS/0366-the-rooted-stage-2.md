# 0366: Hask's stage 2 is rooted on the Warden sample; the captain's post-swallow falls were the board's

Date: 2026-10-09. Issue #1420, from the Design Table's rounds 505 (Chat) and 506 (Code). Every Hask number stays provisional on #1247 and goes to Lotus on his Hask list.

## Decided

- **The split, as a `--finale` line.** For each captain fall after the swallow, the Sim reads his last player phase: as his first command was applied, did any tile in his reach keep the veto's no-crit sum under his HP, and did the tile he ended on? On the shape (0364), **65 of the full arm's 70 post-swallow falls are cornered** (Hask 52, Frozen Iron 13), and 48 of depleted's 60. Of the rest, one is a crit on a tile that passed and the others came before any stage-2 read (the swallow landed after his move). No passing tile proved lethal at no-crit. So the falls are the board's, not the planner's, and no planner fix follows (Lotus's 22:09Z rule has nothing to fix).
- **(c), a rooted stage 2, ships on the sample.** The `swallow` block takes an optional `rooted`. A rooted stage holds the tile he swallowed on (Hold: no move, his lance in reach and his line from where he stands). `hask_warden` carries it, and his card says so. The campaign keep keeps the stand-in, unchanged at 148 / 114.
- **The read: 120 / 53** (`docs/measurements/keep-1420-rooted.txt`), against 78 / 34. Full meets gate 1 exactly. Depleted is still 67 short. Hask's own post-swallow kills fall from 57 to 5. What is left of the captain's falls after the swallow is the clock's (11), and those games are cornered by the dose, not by him.
- **Round 506's kill criterion is not met.** 31 % of the stage-2 damage on him (full) and 43 % (depleted) comes from tiles no action of his reaches. That is under most, so (c) is not scenery on this read. The Sim's player never seeks those tiles; it strikes from wherever its keys put it. A hand play that plinks from the diagonals on purpose is the real test, and the cold chair Chat asked to sit on (c) reads it.
- **(b) and (c)+(b) were screened, not shipped.** (b), the clock from his second phase start: 85 / 40. (c)+(b): 115 / 59, with clock deaths of 2+ down from 85 to 55 of 165 (full) and from 62 to 37 of 110 (depleted). Neither alone nor together reaches 120 on depleted. Per round 505, the bar goes back to the Table with these numbers.
- The `Exposure.Plan` swallow fix (round 505, latent) is not in this PR, since it touches nothing in Exposure. It stays on #1420's list for the next Exposure PR.

## Open, for the Table

- Depleted reads 53 under (c) and 59 under (c)+(b). Its 29 captain falls before the swallow (Hask 16) are untouched by both, and round 505 named stage 1's line catching the captain as the next place to look.
- The clock-death gate (at most 1) fails on every screen. (c) raises it, since stage 2 lasts longer (a median of 7 phases against 5). (b) is what moves it.
- Whether (b) rides on top of (c) is the Table's call: (b) costs full 5 wins and buys depleted 6, and it cuts the clock deaths by a third.
