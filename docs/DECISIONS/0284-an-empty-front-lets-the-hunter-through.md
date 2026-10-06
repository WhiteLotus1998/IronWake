# 0284 — An empty front lets the hunter through

Date: 2026-10-06. Issue #1204, lever 1 of 3; Design Table #1187, round 407 (Chat), answered by Code the same day. Builds what the Table agreed. Amends 0148.

## Context

Under 0148 the hunter strikes only the defenders of the weakest standing front, and a front with nobody within 3 counts as 0 HP. In Chat's cold keep (2210, round 407) emptying the north on turn 4 read `hunts the north next: no defenders (weakest)`, and the hunter walked to 10,2 and waited; across turns 4 to 9 it struck one unit. Emptying a front switched the finale's hunter off.

## Decision

- The hunter's prey (`Hunt.Prey`) is the hunted front's defenders; when the front has none, the player units nearest it (Manhattan to the front's nearest tile, every one tied). It strikes and approaches only its prey, and marches on the front's tiles only when it knows of none (a dusk map). The pair rule still binds its strikes. Its path is the ordinary approach, not forced through the front's tiles; on the keep the nearest units to an empty front stand behind it, so the approach comes in through it.
- On screen: the hunts line on an empty front reads `<hunter> hunts the <front> next: no defenders (weakest); comes through it for the nearest: <names>`, and the board's rule adds `A front with no defenders lets it through: it strikes the units nearest that front.` `threat` and `end`'s lethal ask price the hunter's strike on its prey through the same `EnemyAi.StrikeOn` the planner uses, so the strike through the empty front shows on the same command (Code's condition, round 407).
- `hunt_waits: on` (needs `hunter:`) keeps the 0148 hunt for samples whose journaled plays were made under it: `docs/samples/ironwake_keep_pair.map` (695). The campaign keep never carries it.
- Chat's 2210 transcript is the record of its play under 0148 and no longer replays; its first four turns are now `ChatsEmptiedNorthLetsTheHunterThroughForTheUnitNearestIt`: the same emptied north names Pell, and the hunter comes through 10,2 and puts her on 3 HP.

## The Sim

`--finale ironwake_keep --seeds 200 --gates`, level 8 (`docs/measurements/keep-1204.txt`): full 167 to 174 of 200 (83 to 87 percent), depleted 136 to 143 (68 to 71), random 0, gate 4 median drop 0.095 to 0.070. Inside noise: the Sim's player never empties a front on purpose, so the lever bites a human chair, not the heuristic. The depleted floor of 60 holds; lever 2 (inside spawns on the tile just inside each front) is next, one lever a PR.
