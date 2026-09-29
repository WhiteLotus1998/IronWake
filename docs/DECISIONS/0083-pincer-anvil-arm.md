# 0083 — The pincer's planner arm: anvils

Date: 2026-09-27. Issue 419, built in the chain run woken by the merge of `table/chain-restart-looks`. The arm and its five acceptance points are the Table's (#420, rounds 83 and 84); this record names the Builder's leans inside them. Provisional in the ordinary way.

## Decision

On a `pincer: on` map only, `EnemyAi.Anvil` offers an enemy a second plan beside its best strike:

- For each tile T it can end on and each player unit U it knows of beside T, a **follower** is a group-mate that has not acted, not moved and is not a boss, Aggressive, not retreating, not already claimed, with an equipped weapon that strikes at range 1, that can end on the tile across U from T **on the board with the anvil already on T** and see U from there (acceptance 1).
- The **bonus** is the follower's planner score on U from that tile minus the same score with the anvil off the board, which is the +15 hit on its expected damage (the kill flag reads deterministic damage, so it does not move). The anvil's **value** on T is the best bonus there plus the score of its own best strike from T, if any. The exposure of T is not priced (acceptance 3).
- The anvil plan is taken when its value beats the unit's best strike by today's score. Its commands are a Move to T, then its strike from T if it has one, else Wait.
- **Plan order.** `EnemyAi.Plan` picks the next unit as the first, by id, of those not yet planned that has an anvil plan; with none, the first by id. Without the header, or with no anvil, the order is ascending id as before (acceptance 2; the three committed pincer transcripts replay byte-identical).
- **One to one.** When an anvil acts, its follower is claimed for the rest of the phase: no other anvil scores it, and it is never an anvil itself (acceptance 4).
- **Anchored units.** No boss is an anvil or a follower, whatever its map's win condition; a throne-holder is never an anvil (acceptance 5). This is broader than the Table's wording (a boss under the veto or going home) because every boss on a shipped map is one or the other, and a guard boss on Seize has its own post rule to keep.
- A unit sworn against a unit it knows of (13.4) is never an anvil, so the grudge's override is untouched.
- **The promise is not binding.** The follower plans its own strike by today's score when its turn comes; the pin usually makes the promised strike its best, but a kill elsewhere or an anvil that died on its own counter can take it off.

## Measured

`HeuristicPlayer` against the sample, 40 seeds: an anvil acted in 19 games, nearly always the brawler stepping in for the soldier on enemy phase 2. The enemy struck pinned 7 times in all, pins it set by accident included. Bonuses ran from 0.5 to 1.9 score points, so an anvil wins only where its own strike from T is close to its best strike. That is by design: the arm is an action, not a sacrifice.

## Played

Code's seed 131 (PLAYTEST.md, warm): no anvil ever acted. On enemy phase 2 the tiles behind the front were out of the field group's reach. A counterfactual with those tiles left open drew no anvil either, so geometry denied the pins, not the formation. The deciding play is Chat's cold play of the sample with this arm (kill criterion in #419 and DIALOGUE).

On Brackwater (#429): Code's seed 443 (warm, lost): the arm fired, two enemy pins landed, no costed denial. Chat's cold seed 499 (won on turn 8, Dunstan fell): zero enemy pins in seven phases, two printed `anvil:` plans denied, one at a cost, and one player-made pin that killed. The kill clause did not fire; the arm stays on its sample (Table #470, round 119). Read with it: a cork in a corridor cannot be pinned, so the heuristic's 167 anvils measure a party that does not cork. Keep round: #495, `the_tollgate_pincer.map`, where the enemy arm cannot fire (no Aggressive group-mate), with the Sim counting both sides' pins.
