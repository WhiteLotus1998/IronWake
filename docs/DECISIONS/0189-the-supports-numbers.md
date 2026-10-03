# 0189 — The supports numbers: C 16, B 28, A 48, a captain pair at the higher rate (#77 slice 7)

Date: 2026-10-03. Built by the chain Builder. Restates rounds 258 and 259 on the Design Table (#780), which settled the leans 0186 and 0187 put to it; amends 0184's B and A and 0185's captain rate.

## What is built

- **Tiers.** `rules.json`'s `rivalry.supportTiers`: C 16 (+5 hit), B 28 (+5 hit, +5 avoid), A 48 (+10 hit, +5 avoid, +5 crit). C and the bonuses are unchanged.
- **The captain pair's rate.** In `Rivalry.Accrue`, a support pair with the captain gains `max(captain's rate, recruit's rate)` on a threatened phase beside each other, never the sum. The captain's Cha 9 sits on the top step, so every captain pair accrues at 4 today, twice 0185's rate. Origins move no Cha, so no support number depends on origin (#681). One tier table serves every pair.
- Three journaled transcripts gained the new rapport amounts (and, where a pair now reaches C, a tier line and its forecast); the full-campaign parity guard's first differing line moved from 1102 to 1103.

## The re-read (`docs/measurements/supports-numbers-77.txt`)

Round 259's bars, on the after-Brackwater p50 (about 80 runs) as the headline:

| Bar | Read | Verdict |
|---|---|---|
| A committed captain pair within one threatened phase of B (24) | captain and Teodor 28 (keep 32; B in 5 of 7 finished, A in 1) | met |
| Floor captain pairs at C and under B | best captain pair 20 (keep 20) | met |
| No pair at A under the heuristic | at A p50 0 on every map | met |

Beside them: a committed recruit pair (Wren and Pell) 25 after Brackwater, 36 at the keep (B in 10 of 11 finished, A in 1); Pell and Maud 10, 15 at the keep; captain and Wren committed 16, 20 at the keep. Under the floor the captain is now the best-paired member (20 against the best recruit pair's 8), at C: the captain knows everyone a little, B and A are chosen.

## Still open

- A is reachable by a committed human: the chair's campaign decides. If a player raising one pair on purpose cannot reach A by the keep, A drops to 44, with nothing redesigned.
- The writing waits on `docs/WRITING.md` (#811) and the voice sheets.
