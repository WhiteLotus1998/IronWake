# 0038 — Rapport and Rivalry: a map header, arms as content, rapport on the board

Date: 2026-09-25. Issue 16 (DESIGN 13.1, third in the Table's experiment order). Built by Code; the review is on the issue. These leans are implementation and reversible. This record does not decide keep or kill. That waits on Chat's play, and the PR carrying that decision files the battalions fork (issue 16's body).

## Decisions

1. **Opt-in.** A map turns the spike on with `rivalry: <arm>`, following 0037. Off by default, so no gated map and no gate number moves. The parser refuses an arm the content lacks and names the arms it has. The sample is `docs/samples/saltmarsh_ford_rivalry.map`, which is Saltmarsh Ford plus `rivalry: symmetric` (a test holds it to the shipped file). Old Mill Road deploys one recruit and the Tollgate three Aldmere recruits, so neither can show a rival. Saltmarsh's Ottilie (Sallow) is the rival of both Wren and Teodor (Aldmere).
2. **Who.** A recruit is a deployed player unit other than the captain, with a region. Two recruits of different regions are rivals until their rapport reaches `overwriteAt`. The captain is nobody's rival, since as `crown` he would be everyone's.
3. **Arms are rows of `rules.json`.** `rivalry.arms` maps an id to `hit`, `crit`, `critAvoid` and `countersOnly`: `written` (-5, +10, 0), `symmetric` (-5, +10, -10), `counter` (-5, +10, 0, counters only). Hit and crit apply while a rival is adjacent, and under `countersOnly` only when the unit is the side struck first. Crit avoid applies whenever a rival is adjacent. `Combatant` gains `HitModifier` and `CritModifier` beside the existing unclamped `CritAvoidModifier`.
4. **One seam.** `BattleUnit.ToCombatant(state, content, countering)` reads the neighbours from the board. The resolver's attack, the forecast query, the enemy's target score, the exposure sum and the Sim's kill probability all call it, so the forecast, the resolution and the enemy's pricing cannot disagree (section 8: the enemy prices what the player sees).
5. **Rapport.** A per-pair tally on `BattleState`, sorted, so a Recall restores it and `Canonical` prints it on a rivalry map. At the end of each player phase, every adjacent pair of recruits gains both recruits' rates. A rate comes from `rivalry.rapportRate`, a step table by effective Cha: 1 from Cha 0, 2 from 3, 3 from 5, 4 from 8. Provisional, with `overwriteAt` 16. Events `RapportGained` and `RivalryEnded`. The CLI's `show` prints the rate and the rivals with their rapport, never a bare Cha as the rate.
6. **Exposure is the CLI's.** A scripted or live play ends with `rivalry (<arm>): N player phases ended beside a rival, M of them attacked in the enemy phase after`, and a Recall drops the entries it undoes. The Sim does not count it, because the heuristic neither seeks nor avoids rivals, so its number would measure accidental adjacency.

## What the first play found (seed 23, PLAYTEST.md)

- The symmetric cost is real and legible. On turn 2 a rival-adjacent Ottilie ate an 8 percent crit that prints 0 without the neighbour, and it killed her. The keyed replay without the neighbour landed the same swing for 12, and she lived.
- -5 raw hit is about -9 displayed under two rolls in the 55 to 70 band (67 to 58). The arm's hit cost is larger than its content number reads.
- At a one-tile choke, every tile a bow can shoot from touched a rival twice, so there the arm was weather, not a choice.
- Exposure read 1 of 8, but five of those phases had no enemy in reach. The counter should count only phases where an enemy could reach the unit before a low number argues for arm 3.
- Cha's sign could not be read: the three deployed recruits all have rate 2.

## Not decided

- Keep or kill, and which arm.
- Whether exposure should count only threatened phases (lean: yes, before any arm is chosen on it).
- The rate table and the threshold. The cast's recruit Cha runs 2 to 5, so the table as written barely separates them.
