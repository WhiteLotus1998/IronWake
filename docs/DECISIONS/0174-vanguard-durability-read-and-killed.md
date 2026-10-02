# 0174 — The Vanguard's durability lever, read and killed as a hoarder; the ladder's campaign bar (#758)

Date: 2026-10-02. Issue #758, from the Table's round 232 (#731). The bar and the kill-and-keep rule were agreed by both partners before the read. This record restates them and applies them. The instrument is the Builder's and provisional.

## Decided

- **The bar (round 232) is built.** `--ladder` now reads a per-map floor: no class more than 10 under the unpromoted captain, with 0165's absorption exemption, and gate 4 kept under every class wherever the unpromoted captain keeps it. Separately, for each tier, it reads a campaign check: each class's mean gate 1 over the maps read that `campaign.json` fields, leaving out Starting Alone and Sallow Grange (`LadderRun.Campaign`, `OffTheBar`), must be within 10 of the others. A single map's spread is still printed, but it no longer fails that map. The last line prints both verdicts.
- **The captain's share is printed.** Each won game carries #738's camp reading (`GameResult.Camp`), and `--ladder` prints the captain's share of the company's EXP per class, pooled over the won games.
- **The Vanguard's durability-only modifiers (`hp +4, def +2, res +1`) are killed. `classes.json` is unchanged.** The rule agreed before the read: kept if within 10, absorbing the most of the three and sharing the least. If it absorbs the most and shares the most, durability made a better hoarder, and we go to the verb door. The read (below) lands in that second case.
- **A form never reads under its base.** A form's `modifiers` replace its base's (`Unit.EffectiveStats`); they do not stack. So the lever as written would have cost the Champion 2 HP and 1 Res on promotion. While it was built, the Champion kept its old step over the base (`hp +6, str +2, def +3, res +1`). That went out with the revert. A test now holds every form at or above its base on every stat modifier, and it fails on the lever's numbers with the old Champion.

## Measured

`docs/measurements/ladder-758.txt`, 200 seeds, with the lever in. Campaign maps at tier 1 (Brackwater, Harrow, Saltmarsh, the Mill, the Tollgate):

| Class | Gate 1 per map | Mean | Absorbed (sum) | Captain share per map | Share mean |
|---|---|---|---|---|---|
| Marshal | 81 / 96 / 62 / 63.5 / 76 | 75.7 | 1972 | 51 / 31 / 67 / 58 / 38 | 49.0 |
| Ranger | 68 / 94 / 67.5 / 73 / 74 | 75.3 | 1901 | 48 / 31 / 68 / 57 / 38 | 48.4 |
| Vanguard | 95 / 86.5 / 48.5 / 86 / 86 | 80.4 | 3652 | 57 / 33 / 79 / 54 / 39 | 52.4 |

- **The mean moved, and Code's round 232 expectation was wrong.** The Vanguard went from 14 under (0171, 100 seeds) to 5 over the others (spread 5.1, inside the bar). Durability *is* a gate 1 lever on these boards: Brackwater 62 to 95, the Mill 83 to 86, Harrow 68 to 86.5. Off the bar, Starting Alone goes 0 to 42.5 with the captain alone.
- **It absorbs the most**, nearly twice either other class.
- **Its share is the highest of the three**, highest on four of five maps. The gap is small everywhere except Saltmarsh, where it is 79 against 67 and 68.
- Tier 2 with the lever in: Champion 95.0, Commander 88.7, Pathfinder 95.1 (spread 6.4).
- The per-map floor fails only on gate 4 under some classes (Brackwater's Ranger and Champion, Harrow's Commander and Pathfinder, Old Mill Road's Champion and Pathfinder, the Tollgate's Pathfinder). No class reads more than 10 under the unpromoted captain on any map.

## Open

- **The verb door** for the Vanguard's tier 1 is the Table's next move (round 232). On `main` the Vanguard is 14 under again (0171).
- No 200-seed share read exists *without* the lever, so "durability made it a hoarder" is read against the other two classes, not against the old Vanguard. The rule as agreed asks only for that comparison.
