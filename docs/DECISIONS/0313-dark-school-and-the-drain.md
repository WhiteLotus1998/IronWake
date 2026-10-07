# 0313: Dark is the fifth school; its rider, the drain, heals by the HP a hit took, after the combat

Date: 2026-10-07. Issue #1283. Source: Lotus's #1247 rulings (#1251, 6032995734, 6033061250; DECISIONS/0307), 0296, 0297, 0301. Provisional.

## Context

Lotus ruled three dark spells: two that drain (heal by the damage dealt) and a raise dead (#1284). The book is the Grave Ledger. Dark was "not a school" in 0296; this makes it one.

## Decision

- **`dark` joins `MagicSchool`.** It is a valid tome `school`, class `schools` entry and `rules.json` key. No shipped class reaches it; the dark-mage mini-boss (#1286) is the first. **A primer never teaches dark**, as it never teaches earth (lean: the dark is reached by class or found, never read at camp; #1285's grimoires may revisit).
- **Dark's own rider is `drain`**, no fields but `gate`, so a dark tome opts in by naming it, as every school's does (0298), and a learner is gated as for every rider (0301).
- **The heal is the HP the hits removed, once, after the combat.** Overkill does not count, a kill drains, a miss does not, a counter by a drain tome does, and a watch shot does. Capped at max HP (`unitDrained`). After the combat rather than strike by strike, so every forecast, `threat` and lethal read stays the combat's own; the cost is that a drainer the counter kills heals nothing.
- **The forecast says ` drains up to N`**: every strike landing at the forecast's damage, never past the target's HP. A crit drains more, unprinted, as the damage column leaves crits out; counting them made a 3 percent crit print `drains up to 23` beside `dmg 13` on the warm play. The gate's refusal reads ` no drain: Mag 4, Res 4`.
- **The planner prices the heal as HP**: the expected damage dealt, up to the HP the drainer is expected to be missing after the counter; a draining counter subtracts its heal. So a wounded drainer prefers the long fight it can win.

## Play

Code 1283 warm on the Tollgate with a fixture Ledger, 7/6/7, won turn 10. `end` asked for `end !` three times on a drainer whose counters were 99 percent, since the lethal sum does not price the heal between combats. Next: a `threat` note under a drainer's lines, the total unchanged (0281).

## Kill criterion

Revisit the timing (strike by strike) if a journal shows a drainer dying to a counter it would have outlived had the first hit's heal landed first, and the player read that as unfair rather than as the price of the read.
