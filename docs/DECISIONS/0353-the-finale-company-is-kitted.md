# 0353 — The finale company is Kitted

Date: 2026-10-08. Issue #1395 (`bug`), slice 2, layer 1; Design Table rounds 494 to 497. This is implementation of what the Table agreed. Amends 0293's finale company.

## Decision

- **`FinaleCompanies.Roster` fields every story member through `CampaignRecord.Kitted` before raising them to the finale's level.** Keziah carries Kinsbane in front of her pack, Teodor carries the Family Lance behind his, and Rook's drake is half-grown. `play --company` fields the same roster, so a hand chair gets it too. Hires are unchanged.
- **The majority rule (round 497) applied to this layer:** the heuristic campaign (`LevelRun`) fights no side map, so no run pays a quest by the keep. The drake stays half-grown (Grown needs Rook's quest 1), and no quest-paid weapon is carried.

## Read

`--finale content/keep/ironwake_keep.map`, L8, 200 seeds (full / depleted / floor), in `docs/measurements/keep-1395-kitted.txt`:

- Before: 153 / 101 / 0. Kitted: **149 / 101 / 0**. Kitted less Kinsbane: 153 / 101 / 0.
- Depleted and floor don't move. Neither fields Keziah or Rook, and Teodor never swings the lance he carries behind his own.
- **The whole move is Kinsbane's.** Over the 200 full-company games, Keziah attacks 538 times with the scythe against 747 without it, kills 159 times against 242, and drops to 5 HP or less in 93 games against 52. The Sim holds her back as the drain bites, so she can't feed. Round 497 calls that a player fix if the Sim is feeding badly, and the weapon stays in.

## Next

- Trace Keziah's held-back turns: which refusal keeps a drained scythe-bearer from the kill that would heal her. Fix it in the player if it's the Sim's fault.
- Layer 2: median campaign-earned ranks per main type. Layer 3: keep stock within rank.
