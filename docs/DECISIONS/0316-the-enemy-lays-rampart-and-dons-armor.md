# 0316: The enemy lays Rampart where a ward is struck, and dons armor only with no strike

Date: 2026-10-07. Issue #1286, slice 2 (the planner's earth casts). Source: Lotus's enemy-caster ruling (#1251, 6033129482; DECISIONS/0307), 0312, 0315. Provisional: a Table lean, argued on the PR.

## Context

#1286's step 2 asks the enemy planner to cast "when a chill, a Rampart, a raise is worth more than a strike". Slice 1 (0315) built the raise. The chill already rides a frost tome's strike, because the planner strikes with any tome it carries, so there is no separate cast to choose. The two Item-action casts the enemy never made were Rampart (`Earthwork`, on an ally) and armor (`Armor`, on the caster).

## Decision

- **Rampart (`EnemyAi.Rampart`).** A unit holding a tome naming earth's raise rider, which it can wield with a use left, lays the ground under an ally of its side in place of any strike that is not a kill, ranked after a raise. The ally must meet three conditions:
  - it stays where it stands this phase, either because it has moved or acted, or because its behavior keeps it on its tile;
  - it stands on open ground, or on the caster's own earthwork, which a recast refreshes; another caster's earthwork is never taken;
  - a player unit can strike it next phase, priced as the boss veto prices the player phase (`Exposure.OfBoss` above 0).
- **Which ally and which tile.** The ally with the most exposure, then the least HP. The caster casts from the tile fewest player units can reach, then the cheapest, as the raise does.
- **Armor (`EnemyAi.Don`).** A unit wearing no armor, holding an armor tome it can wield with a use left, dons it only in a phase with no strike. It dons on the tile it ends on (its approach tile, or where it stands) if a player unit can strike it there. It never replaces a strike, so 0312's kill criterion reads the cast cleanly.
- **Why these lines.** A Rampart lasts through the player phase that follows, so it is worth most under an ally the player is about to hit. Placing it under an ally that will still move wastes it. Armor costs Mov for phases. On a unit that could strike, it would trade a sure exchange for a Def bonus the player can simply walk around.
- **No content.** No shipped class or tome casts either spell. The fixture is the woods archer of `the_tollgate_frost.map`, made an Adept given earth. No gate moves.

## Not built here

- `threat` reads neither cast, as it reads no raise (0315). A caster's line shows its strike or nothing.
- `Score` does not price the chill.
- The enemy classes, the placement proposal, and `drops:` wait on Lotus's signature on #1247.

## Kill criterion

- **Rampart.** If a journal shows an earth-shaper that never strikes because some ally is always struck, the Rampart goes behind a strike worth more than the ward's exposure saved: 5 Def times the strikes priced on it.
- **Armor.** If armor never forces a choice, 0312's levers apply.

## Amendment (issue 1286, slice 3; Chat on #1305, 6037806738; Code, 6037828052)

- **Order.** A unit holding a raise or Rampart tome it can wield with a use left plans after the rest of its side, a stable partition of the phase's order (`EnemyAi.CastsOnTheBoard`). A ward must have moved or acted, or hold, so before this the map file's list order decided whether an earth-shaper could ward the brigand that just closed in. It also means the bodies from this phase's counters lie on the board when a raiser plans.
- **Warded first (`EnemyAi.WardedFirst`).** A boss on a Defeat Boss map, or a raiser with a Hollow of its own standing, is warded ahead of any other ally whenever it meets the three conditions and some tile reaches it; otherwise the most exposed ally, as before. Both are tags the board shows, so this is no hidden value function. Bosses mostly stand on forts and thrones, where the ground cannot rise, so it rarely fires.
- **Journal watch.** Every journal of a board with an earth-shaper records each ward, and whether the player struck through it or went around it. Going around every time is the sign that Rampart is cosmetic. A journal saying it warded a grunt while the player killed the boss points at the warded-first rule as the first lever.

## Second amendment (issue 1286, slice 4: `threat` reads the casts)

- **What `threat` got wrong.** A Hollow rises having acted, and a Rampart is laid after the rest of the side has acted, so no cast adds or strengthens a strike in the phase it is made: `threat` never under-prices for one. It over-said instead: a raiser's or earth-shaper's strike that does not kill was a plain line, though the planner casts in place of it.
- **Decision.** A line whose strike would not kill the unit it lands on if every hit lands, from an enemy holding a raise it has not spent (no Hollow standing) or a Rampart tome, gets one row: `May raise the dead instead: it casts in place of any strike that does not kill (still in the total)`, or `May lay Rampart instead: ...` (`EnemyAi.CastsInstead`, `ThreatLine.Casts`; the protocol's `casts`, `raise` or `rampart`). A killing line gets none. A windup raise and a held line get none.
- **Why counted.** Whether the cast is taken depends on bodies and allies the phase moves, after the phase-start board `threat` reads; the total is the worst case (0281), and `end` is unchanged. Armor never replaces a strike, so it gets no row.
- **Kill criterion.** If a journal shows a player reading the row as "this strike will not come" and losing a unit to it, the row names the condition the planner reads on the phase-start board (a body in reach, a ward exposed) instead of "may".
