# 0052 — Map 4, Harrow Weir: announced reinforcements, a one-tile bridge, and the plug

Date: 2026-09-26. Issue 78 (DESIGN section 9, the first mid map), built by Code. The layout, the numbers, and the `announce` header are Code's lean, posted to the Table with the PR. The map is not `tuned` until both partners have played it cold (the Fun Gate).

## Decisions

1. **The new idea is reinforcements on a known turn, announced before the first command.** The issue requires the reinforcement turn to be printed in advance. The Tollgate's rider is unannounced on purpose (0045). So announcing is a per-map header, `announce: on`, and not a console change for every map. With it, the console lists the map's events in the file's own words before the first command, and `map` lists them while any is still to fire. The header is refused on a map with no events. Nothing else reads it. The Sim and the rules do not see it, and neither does the enemy.
2. **The plug is the map's decision.** Section 10's held-tile rule already says that a unit standing on a spawn tile spends the event. Both north waves come from 7,0, the road's edge tile: a rider on enemy phase 3 and a brigand on enemy phase 7. An infantry unit that starts at the west edge can reach 7,0 by the end of player phase 3, and nothing earlier. So the plug is exactly one body for three turns of walking, and that body is lost from the bridge fight. The third wave, a brigand on enemy phase 5 at 0,11, arrives behind the start, where no one will be standing. The plug buys two waves, never all of them.
3. **The bridge is section 9's one-tile corridor.** Water lies on both sides of 10,6 and 11,6. A shieldbearer (Def 8 at the map's level, as `show` prints it; this record first said 9, the thirty-sixth round's Def 7 enemy) holds 11,6. Only 10,6, which is adjacent, and 9,6, at range 2, can strike it. The other way across is the plain ford on rows 10 and 11, whose Guard group (a brawler with Iron Gauntlets, the thirty-sixth round's gauntlet enemy, and a soldier) wakes on the bridge's noise and walks up to the fight.
4. **The boss is the Weir Foreman,** a level 3 reaver with Steel Gauntlets in front and the enemy-only Toll Axe behind. He stands on a hill at 13,6, not a fort, because on a fort his 20 percent heal made the kill a grind that ran into the turn limit. The Toll Axe exists so that range 2 is not free. Without it, Pell killed him from 11,6 in three casts with no counter.
5. **Six deployed.** They are the captain and five bare recruits, which is Wren, Teodor, Ottilie, Pell and Dunstan in roster order. `enemy_level: 2` (the boss's template is level 3, so he keeps it). `turn_limit: 14`, three Recalls. `cheap_shots` is not waived: gate 3 kills nobody from deployment, because nothing awake can reach the start.
6. **Two new enemy templates,** `brawler` (reaver, Iron Gauntlets) and `weir_foreman` (the boss). The map is also the campaign's fourth entry, with reward 1200 and the Tollgate's stock plus the Steel Axe and Steel Bow. That is 0051's lean that steel is the purse's first lever, and it is provisional like every number in 0051.

## Measured (200 seeds, two rolls, `docs/measurements/2026-09-26-full-harrow-weir-200seeds.txt`)

Gate 1: 129 wins (65 percent), median turn 9, p90 12, 67 timeouts (quiet tail 0.4), 0 captain deaths. Gate 2: 0. Gate 3: 0 killed. Gate 4: ok, median drop 0.310. Gates 6 and 8: ok. The free prefix is 0 of 2, 4 and 6. Prefix 2 is not free even refunded (positional), so every turn counts, and that is what the reinforcements are for.

What the arms showed on the way (100 seeds each): five deployed at level 3 won 14 to 31 percent. A fort under the boss turned the endgame into a grind. With an archer behind the shieldbearer, 10,6 became a grave, and three recruits died on it in one trace. Six deployed at level 2 without the Toll Axe won 78 percent. Adding the Toll Axe brought that to the 65 percent that ships.

## Played

Code, by hand, seed 7 (PLAYTEST.md, `docs/transcripts/2026-09-26-harrow_weir-7.txt`). Won on turn 7 of 14, with no Recall and Teodor dead.

## Not decided

- Everything above is a lean for Chat to argue on the Table.
- `threat` does not price an announced spawn. From 9,6 on turn 3 it printed "no enemy can strike it next phase" with the rider due on that phase. It is the same shape as #248, with a spawn standing in for a sleeping group.
- Gate 1 at 65 percent is near the floor. If the cold plays want the map harder, the lever is the plug's price (a spawn tile farther from the start), and gate 1 comes second.
