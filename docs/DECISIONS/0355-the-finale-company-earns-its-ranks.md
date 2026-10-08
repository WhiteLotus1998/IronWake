# 0355 — The finale company carries the ranks a campaign earns by the keep

Date: 2026-10-08. Issue #1395 (`bug`), slice 2, layer 2; Design Table rounds 494 to 497. This implements what the Table agreed. It amends 0293's finale company, after 0353 (Kitted) and 0354 (the scythe plans first).

## Decision

- **`campaign.json`'s `keep.finaleRanks`** maps a cast member's id to rank points per weapon type their class uses. The loader refuses a non-member, a type the class does not use, and points that are not an integer of at least 1, naming the file, entry and field.
- **`FinaleCompanies.Roster` raises each story member's points to their entry** after Kitted and the level scaling, and never lowers them. Maud keeps her declared faith D. Hires are unchanged. `play --company` fields the same roster.
- **The numbers come from `--levels --finale-ranks`**: the heuristic campaign (permadeath off, retries as `--levels`) over 200 seeds. For each story member it takes the lower median of their points per type, read from the company standing after the map before the keep. A type whose median is 0 is left out. This is round 494's "median points per member's main types, as earned by the keep".

## Read

`--levels --finale-ranks --seeds 200`: 82 runs won the field, the map before the keep. The medians are Wren sword 8, Teodor lance 11, Ottilie bow 16, Pell reason 69 (D), Dunstan lance 3, Maud faith 40 (D), and Rook lance 30 (D). Ansgar and Brannock have none. Keziah is in no run's company: the heuristic never takes her by the keep, so she earns nothing and keeps E.

`--finale content/keep/ironwake_keep.map`, L8, 200 seeds: **157 / 101 / 0**, the same as main. That is expected. Rank points gate what can be equipped and change no combat number, and every finale weapon is still rank E. The layer moves nothing until layer 3 kits within rank. The full read is in `docs/measurements/keep-1395-ranks.txt`.

## Next

- Layer 3: keep stock within these ranks. Only three members reach D: Pell (a D reason tome), Maud (a D faith tome) and Rook (a D lance). Everyone else stays on E steel-less kit. The quest-paid weapons stay out under the majority rule (0353).
- The heuristic earns little rank by the keep (Wren at 8 points after nine maps). That is a measurement of the Sim's campaign, not of a human's. If the Table wants the finale to read a human-paced company instead, it would need a different source than this median.
