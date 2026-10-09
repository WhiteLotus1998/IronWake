# 0385: the secret path's flag at the keep, and the coma in the ending

Date: 2026-10-09. Issue #1386, slice 2b. Builds on 0382 (the race engine) and 0384 (the resistance, the Sim's take). The conditions are STORY's Under the Hill (draft 6, all four, none announced); Chat's round 542: the race is read, not tuned, until #1453.

## Decided

- **`shard_race: <x,y> <phases> secret`** holds a race apart (`MapDefinition.SecretRace`, with the map's `race` events): no race, no announcement line, nothing for the Sim, until `MapDefinition.ArmSecretRace` puts both on the board. A battle's map text writes an armed race as `armed`, so a suspend reads it back armed.
- **The campaign keep carries it**, copied from the sample: inner tile 15,6, 5 phases, a held soldier at 11,6 on `race 0`, a brigand at 15,0 and a soldier at 15,11 on `race 2`. Placeholders for the tuning pass, as on the sample.
- **The conditions are content:** the keep's `campaign.json` entry carries `secret: { "bearer": "keziah", "quest": "pell_2" }` (one map at most; the bearer must be in the cast, the quest a side map). `CampaignRecord.UnderTheHill` reads three of STORY's four before the battle: the bearer stands in the company with the hungering weapon woken (its last tooth, `Kinsbane.WakeKills`; STORY's "15 fed" predates the teeth at 12); both claimants stand in it, the pick and the passed one `turned` on the return; the quest is won. `CampaignRecord.Begin` arms the race when they hold.
- **The fourth, Marrit held, is read in the battle:** an armed secret race starts only while the enemy the map's `freed:` header binds has not been killed (`BattleState.Bond` not `fell`). Killed first, Hask swallows as on the normal path. A plain (sample) race ignores the bond, so 0384's read stands.
- **The coma carries:** `CampaignRecord.Coma` gains a won map's `BattleState.Coma` (the protocol's `coma`, written only when not empty), and `ending.json`'s block carries `coma`, always written; the block's version goes to 2.
- The normal path is unchanged: the keep's race is held unless armed, so `--finale` and every keep replay read as before.

## Not decided here

- Whether a taken shard frees Marrit (`freed:` fires on the binder's death, and the take is not a death). She stays bound on the board until the map is won either way; the hill map (slice 3) is where she leads them down, so it is decided there.
- The race's tuning (board, guard, wave, phases), after #1453 and against a warm and a cold chair.

## Next

- Slice 3: under the hill (the Kin's Frozen Iron, the sworn bearer, the re-take), on the hill map.
