# 0297: A school's rider is data on the school, and fire's burn is built but waits on the Table

Date: 2026-10-07. Issue #1243. Source: Lotus's schools ruling (#1218, 6027545091, point 5), Chat round 416 ("rider spec on the school"), Code round 417, 0264's fire lean, 0296. Provisional.

## Context

#1243 asked for a rider spec on the school in data, fire's rider as a burn (2 HP at each of the struck side's next two phase starts, never below 1, refresh not stack), and Cinder to take it by being `fire`. Its gate: `--full --all` before and after, and a tuned map crossing a gate line goes to the Table before merge.

## Decision

- **The spec is data.** `rules.json` takes a `schools` block: school to an optional `rider` with `kind`, `amount` and `phases`, each number at least 1. A tome carries its school's rider (`GameContent.RiderOf`); Core acts on the kind and never names a school. An unknown kind, school or field fails load naming `rules.json`, `schools.<school>` and the field. The block round-trips through the serializer. `gate` is not a field yet: #1246 adds it with the learning rule it serves.
- **The burn** (`Burning`): a hit from a burn rider's tome on a unit that survives the combat sets `burn` and `burnPhases` (`unitIgnited`). At each start of its own side's phase it loses `burn`, never below 1, and the count turns; it clears at 0. A second hit refreshes the count and never stacks. On a burning tile it loses the larger of the two, as one `unitBurned`. Overwatch's strike burns as a combat does. Board state, so Recall restores it.
- **On screen:** the forecast prints `burns 2 for two phases` after that side's strike columns (where `chills` prints), the card `burning: 2 for two more phases`, the event `<name> catches fire: 2 hp at the start of each of its side's next two phases`. The protocol unit carries `burn` and `burnPhases`.
- **`threat` and `end` are unchanged.** The issue assumed `threat` counts terrain burn; it counts none, and need not: a unit's tick lands at the start of its own side's phase, so a player unit's never lands inside the enemy phase `threat` prices, and a tick never kills.
- **Planners:** `EnemyAi.Score`, which the enemy and every Sim player share, adds the expected ticks to each side's line, weighted by the chance some strike lands through the one hit function (`Combat.HitProbability`), capped at the HP above 1 the strikes are expected to leave, and only the phases a refresh adds. A priced kill adds none.
- **Fire's rider is off in shipped content.** With `fire` given `burn 2 for 2`, 58 journaled transcripts move and a dozen plays change. Chat's cold chair 3971 is lost on map 1, Starting Alone: the hexer's Cinder burns the lone captain twice under the archer's chip, and he falls at 6,3 in the enemy phase of turn 5. Lotus also said shipped tomes stay as they are. So the system ships dark: `rules.json` names no rider, a test pins that, and the tests run the burn on shipped content with the rider added. Turning it on is one line of `rules.json` once the Table rules on the numbers below.

## Measured

`docs/measurements/schools-burn-1243.txt`: `--full --all`, 200 seeds a map, with the rider off (what ships) and on. The full suite, `--smoke` and every transcript pass unchanged with it off.

## Open for the Table

Whether fire burns in shipped hands, and on whose: every Cinder (the lean in 0264), the player's only, or not before map 2. The lean is not to burn on Starting Alone, where the captain has no healer.
