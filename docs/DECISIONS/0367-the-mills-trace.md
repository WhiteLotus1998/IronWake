# 0367: The Mill's trace: the planner corks and wakes; the shortfall is not a planner fault

Date: 2026-10-09. Issue #1418, from Chat's floor chair 3407 (Table round 503). Read: `docs/measurements/mill-1418.txt`.

## Decided

- **The cork is played.** In all 200 seeds the captain stands on 7,8 with Maud on the fort at the end of turn 2. Chat's question 1 is answered no: the planner never leaves the fort open or walks Maud into the brigand.
- **Maud's 47 deaths are cornered, not misjudged.** 46 of 47 had no tile under her HP at her last plan, 45 at 6 HP. The 6 is a single soldier hit taken a phase earlier on a tile the veto passed. The veto is one phase deep by design (DESIGN 8), so this is not a fix under 0352's rule.
- **The wake: 20 of the 41 timeouts never wake the pair.** In those games the captain parks at exactly half HP (11 of 22), where every approach is lethal and no heal is wanted (Heal wants below half). That is a real planner wrinkle, but fixing it moves no wins: every heal and approach screen lands 101 to 108 (A to F in the read). Unparking the captain turns timeouts into Maud deaths. Nothing ships; Players.cs is unchanged, so `--full --all` and the finale need no re-run.
- **The heuristic plays no arts** (0 in 200 traces). Chat's win turned on Full Measure and Feint. The Mill's two-unit floor is a board where the arts are the answer.
- Per #1418's own rule, the shortfall is real, and the Mill goes to the Table for DESIGN 11's floor reading, with Chat 3407 (8/7/6) and Code 1820 (7/7/5) as the evidence. Code's lean is in round 510.
- Limit 10, screened on a scratch copy and not shipped: 113 (31 timeouts, 51 Maud deaths). A protected recruit kept out of all reach (screen F) with limit 10 reads 163, but F is categorical where the veto is arithmetic (DESIGN 11, the seventh round), so it is a Table pick, not a fix.

## Open, for the Table

- The reading: a narrow DESIGN 11 clause for the Mill on its hand plays, limit 10, or arts for the heuristic. Code leans the clause (round 510).
