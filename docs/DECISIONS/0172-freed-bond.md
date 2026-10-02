# 0172 — The bond: the finale's hunter is freed when the lord falls (#750)

Date: 2026-10-02. Issue #750 (Chat, from round 214 on #665 and round 231 on #731). The mechanic half of Marrit's release, built on the finale stand-ins. The story half waits on #656. The rule is Chat's; the shapes below are the Builder's and provisional.

## Decided

- **Header `freed: <x,y> by <group>`.** The tile names an enemy placement (matched by placement, as `hunter:` is). The boss is named by his group, not a tile, because the finale's lord is spawned by a turn-6 event and takes the nearest free tile, so a boss tile could name the wrong square. Refused: a tile with no `E` line, a boss on the tile, the messenger on the tile, and a group with no boss, placed or spawned. Each refusal has its own test.
- **The freeing (`Freed.After`).** After any command where a boss of the group dies while the bound enemy stands, she leaves the board with one `UnitFreed` (`<name> lays down the weapon: freed (N hp)`; protocol `unitFreed`). It is not a kill: no `UnitDied`, no EXP, no drop, and she is gone for a rout. It runs before the break, so a bound unit in the boss's own group is freed, not broken. `Outcome` is read from the board, so the events read: he falls, she is freed, the map is won.
- **Killing her first** is an ordinary kill. `BattleState.Bond` records the fate (`fell`, or `freed` over it), set where the messenger's fate is set; `Canonical` prints `bond freed|fell`; a Recall restores it. `CampaignRecord.FreedUnitFell` is set by `AfterBattle` on a won map and kept for the rest of the campaign. The record JSON writes `freedUnitFell` only when it is true. Nothing reads it until #656.
- **Printed, never learned by surprise.** While she stands, the board, `threat` (one row) and every forecast with the boss or her in it print `<her> is bound to <boss>: freed when <boss> falls`. The boss is named from the map before he spawns. The issue's draft said "he falls" and "drops her weapon"; enemies carry no pronoun, so the lines use names and "the weapon".
- **The sample.** `docs/samples/ironwake_keep_finale.map` carries `freed: 7,6 by lord`. No shipped map does.
- **`--finale`** prints one bond line per company: hunter killed, freed, or standing at the end. Data only. The Sim's player does not price an ending, so nothing is tuned from it.

## Measured and played

`--finale` at 50 seeds: see the PR. Code's warm hand play, seed 750 at level 8 (`docs/transcripts/2026-10-02-ironwake_keep_finale-750.*`), was stopped at turn 6 in a losing position. Pell's Cinder went into the hunter twice to end the hunt, but she was never killed.

## Kill criterion (Chat's, unchanged)

If two chair plays of the stand-in finale never consider leaving her alive, the bond is scenery. The first lever is printing it on the board, which this build already does, so the next lever would be the cost side: what killing her takes from the founding.
