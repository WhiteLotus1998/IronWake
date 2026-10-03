# 0187 — The supports' ceiling: a pairing player reaches B on a recruit pair, C on a captain pair (#77 slice 5)

Date: 2026-10-03. Built by the chain Builder. A measurement; no number moves. It adds the read 0186 asked for to the lean already on the Design Table.

## What is built

- `PairingPlayer` in the Sim: the heuristic player committed to one named support pair. It plans every unit as the heuristic does, with two changes for the pair:
  - **The striker goes first.** When the heuristic would move one member without a strike while the other member, not yet acted, has one, the striker plans first, so the idle member can follow it.
  - **It ends beside its partner where the tile is no worse.** For an attack, that means the same weapon on the same target from a tile beside the partner, at a score no lower than the heuristic's tile. For a plain Wait, it is the tile beside the partner nearest the heuristic's destination.
  - **The veto still applies.** A unit whose death loses the map ends only where the no-crit sum of everything that could strike it over the cycle stays under its HP.
  - **Some turns are left as the heuristic planned them:** heals, exits, Canto, a Wait on an exit tile, and any unit that has already moved.
- `PairingPlayer.Deploy` benches units from the back of the deployment until both members fight. A bench the map refuses passes to the next unit back, and a member not yet on the roster stays out.
- `--supports --pair <a> <b> [--seeds N]` runs `--levels`' campaigns under it. It refuses a pair `campaign.json` does not list. It prints the same table as 0186, then the named pair's final line and its p50 after each won map.

## What it read (200 runs each, `docs/measurements/supports-pairing-77.txt`)

| Pair | Floor (0186) final p50 | Pairing final p50 / p75 | Finished | Reached C / B / A | p50 after Brackwater |
|---|---|---|---|---|---|
| wren and pell (comedy) | 8 | 32 / 40 | 9 | 9 / 3 / 0 | 25 |
| pell and maud (romance) | 0 | 15 / 23 | 25 | 11 / 1 / 0 | 10 |
| captain and teodor (argument; romance with a woman captain) | 8 | 16 / 17 | 5 | 3 / 0 / 0 | 14 |
| captain and wren (mentor) | 10 | 10 / 12 | 12 | 1 / 0 / 0 | 8 |

- **No pair reaches A, even under a committed player.** The best recruit pair ends near 32, and B at 40 falls in only 3 of 9 finished runs.
- **A captain pair stays at C, even when committed.** The captain's veto keeps him off the threatened tiles where accrual happens (0042), and a captain pair accrues at the recruit's rate alone (0185). Wren's pair barely moves off the floor. Teodor's reaches C by the keep.
- **The pairing player wins about as often as the heuristic:** Brackwater 78 to 82 runs against 82. The extra keep wins on Pell and Maud's run come from small numbers, not skill.

## What it means for the lean

- 0186's lean, B 28 and A 48, fits the recruit pairs. Under it a committed recruit pair reaches B by the keep in the median finished run (32 against 28, 25 after Brackwater). A stays out of this heuristic's reach (p75 40), which leaves it as a stretch goal for a human who pairs better than the heuristic does.
- The captain pairs carry the romances, and under the lean they still stop short of B. Rounding the tiers down does not fix the hub rate. So a second lean goes to the Table with this read: **a captain pair accrues at both rates, the captain's included**, with the hub argument met differently. Only the captain pair a player keeps beside the captain climbs, and since one captain stands beside at most four partners, ten pairs cannot all climb at once. This is not built. 0185's rule stands until the Table answers.
- This heuristic is still a weak pairer: it never steps into threat to accrue. So the read is a ceiling for the Sim, not for a player. A chair's campaign through Brackwater, naming the pair it raised, is still the play that settles it.
