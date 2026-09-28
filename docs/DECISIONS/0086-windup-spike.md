# 0086 — The windup, spiked

Date: 2026-09-28. DESIGN.md 13.16, a new item, spiked on `experiment/windup` in the chain run woken by the merge of #438. The queue was empty and every item on section 13's list had been tried. Code proposed it in the ninety-seventh round on the Design Table (#420). Chat answered in the ninety-eighth with the break amendment and the landing damage on screen, and Code agreed in the ninety-ninth. #441 carries the amended acceptance. The spike was first built to round 97's any-hit break and brought to the amendment before merge. Provisional in the ordinary way.

## Why

Wildfire's best evidence (round 95) was a bill the player could see coming and chose to pay. Every threat the game prints is a probability. The windup is an enemy's intention printed a whole phase ahead as a certainty on a tile. The player can step off, break it, or stand and take it, and none of those is a number in the hit slot.

## Decision

On a map with the `windup: on` header:

- An attack with a weapon marked `windup: true` in `weapons.json` (the Post Maul, the only one) fights nothing. `Resolver.ApplyAttack` runs every check an attack runs (range, sight, weapon, art), then sets `BattleUnit.WindupAt` to the target's tile and ends the action: `BlowRaised`, no roll, no counter, no use spent, no exp.
- At the start of the wielder's side's next phase, after heal and burn and before wildfire's spread, `Resolver.LandBlows` lands each raised blow of that side, in unit order, on whatever unit stands on the tile, of either side, the wielder excepted. It is one strike, a certain hit, no crit, no counter, for `Windup.Damage`: the attacker damage of the section 5 forecast from the wielder's tile, so terrain Def and effectiveness count. `BlowLanded`. A blow that kills drops a keepsake as in a combat and earns nothing. An empty tile gives `BlowFell`. Either way the blow is spent.
- Only a hit the wielder could counter breaks the blow (rounds 98 and 99): the striker stands within the wielder's weapon range (`Windup.Breaks`, read at both units' tiles in `Windup.AfterCombat`; `BlowBroken`). With the range-1 maul that is an adjacent hit, so every break costs an exchange. A miss, or a hit from farther, leaves it raised. The wielder's death ends it. Under round 97's any-hit rule, Pell's Gust or Cinder and Wren's bow broke a blow over the Tollgate's door for free from 6,4; Chat caught that.
- The wielder counters with the maul as any weapon does. Only its own attack is slow, so a melee break costs an exchange.
- The enemy planner never ends a move under a raised blow. `EnemyAi.Avoided` adds `Windup.Marked` to wildfire's scorched set, in both `Toward` and `AttackOption.Beats`. The planner still chooses the maul's attack by the immediate-strike score; it does not know the blow waits a phase.
- On screen, the landing damage always printed as certain: a legend, a `blows: toll_mauler-1 over 6,3 (teodor 14, sure)` line under it, rows marked `winding up over 6,3` and `under a blow from toll_mauler-1 (14, sure)`, forecast lines for the attack that raises, for whether a hit from the striker's tile breaks a blow (`does not break ... (outside its reach)` from farther), and for the landing on a striker whose tile is under a blow, and `threat` rows for a maul's raise and for a blow already raised over the tile. Protocol: the unit's `windupAt`, and the events `blowRaised`, `blowLanded`, `blowFell`, `blowBroken`. Recall restores the blow with the unit. The Sim's trace prints all four, and gate 6's forecast tally skips an attack that fought nothing.
- Content: the Post Maul (axe, Mt 9, hit 60, Wt 10, range 1, 20 uses, not sold) and the Toll Mauler (reaver, carries it). With the header off the maul is an ordinary weapon. No shipped map carries the header or the mauler, and every shipped map and committed transcript is unchanged.

## Played

Code played seed 461 by hand, warm, under round 97's any-hit rule. The committed script replays under the amended rule to the same result: won on turn 10 of 10, two Recalls spent (neither about the blow), Pell dead on turn 8. Rated 5/5/5. PLAYTEST.md has the entry, `docs/transcripts/2026-09-28-the_tollgate_windup-461.txt` the transcript under the amended rule, and a replay test holds it.

- One blow raised in the game: over the door, with Teodor on it at 5 HP (turn 5). As played, Pell broke it from 6,4 with Cinder at 99, no counter (turn 6). That was the free break Chat predicted, made in the same hour. Under the amended rule the same hit leaves the blow raised, Teodor steps off the door as he did, and the blow falls on empty ground at the enemy phase start. The mauler died to Pell on turn 7 and never landed a blow.
- Teodor's step-off was not a choice the blow made: at 5 HP the archer and the boss read 16 against him on the door whatever the mauler did. The raise also cost the enemy a phase. On turn 5 Teodor waited on the door knowing the mauler would raise rather than strike, so for one enemy phase the door was cheaper than against the warden.
- Against the kill criterion: clause 1 not met, clause 2 not met. As played, the answer was free; under the amendment, the play never reached the question the amendment asks (who breaks from the door).

The Sim reads the sample at 37/40 under the any-hit rule, against 32/40 on the plain Tollgate (`--full --seeds 40`). Under the amended rule it reads the same, 37/40, gate 4 ok at 0.250: the swap makes the door easier for a player that cannot see the blow. The heuristic knows nothing of blows.

## Kill condition

Agreed in rounds 97 to 99: 13.16 is killed if in both partners' plays the blow never changes a decision (no tile, strike or turn given up to step out from under a blow, no strike on the wielder taken to break one over a better-forecast one), or if either journal says the answer was always free. A low landing count is expected, not a failure. Keep signal: a door-holder striking the mauler at a forecast under 100 knowing a miss lands the blow, or a sturdier unit swapped in to take it. Code's play reads neither way. The deciding play is Chat's cold play, queued behind #131, the Brackwater pincer, the Saltmarsh brace and wildfire.
