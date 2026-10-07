# 0299: Ice's rider is frozen iron's chill; the stun is a rider gated on class

Date: 2026-10-07. Issue #1244. Source: Lotus's schools ruling (#1218), 0155, 0264 (ice slows, the storm-warden's stun), 0297, 0298. Provisional.

## Context

#1244 asked for ice's chill and lightning's stun as school riders on 0298's shape (the tome names the kind, the school sets the numbers). Its body said "Mov -2" for the chill, but its own source, 0264, says an ice tome's hit uses frozen iron's chill (0155), and that chill is Mov -1.

## Decision

- **Fields per kind.** `burn` keeps `amount` and `phases`. `chill` takes none: it is frozen iron's (Mov -1, never below 1, on 0155's clock), so the loader rejects a number nothing would read. `stun` takes `classes`, the classes whose casters fire it: at least one, each a class in `classes.json`. A field another kind owns is refused naming `rules.json`, `schools.<school>` and the field.
- **The chill** (`Frost.Chills`): a hit from frozen iron, or from a tome that names its school's `chill`, chills exactly as 0155 does. That covers the forecast's ` chills`, the card, `unitChilled`, the watch shot, and refreshing without stacking. Rook's hold still wins (`DrakeFrost.Mov` floors after `Frost.Mov`). A re-chill keeps `LockedBy`, so a locked unit stays locked.
- **The stun** (`Stun`): a hit from a tome naming `stun`, struck by a caster of a class the rider names, on a non-boss target that survives the combat, sets `Stun` to 1 (`unitStunned`). It runs on the chill's clock. When the stunned side's next phase begins, the clock turns to 2 and the unit begins that phase moved and acted (`stunSkipped`). Its commands are refused with "is stunned and skips this phase", and `EnemyAi.StrikeOn` gives it no strike. The clock clears when that phase ends. A stunned unit still counters. Bosses are spared. It works once a map per caster (`StunSpent`), and only a stun that lands spends it: a miss, a kill or a spared boss costs nothing. All of this is board state, so Recall restores it, and the protocol carries `stun` and `stunSpent`.
- **On screen:** the forecast prints ` stuns`, ` stun: bosses spared` or ` stun spent` after that side's strike columns. The card shows `stunned: skips its next phase` (or `this phase`). `threat` drops a stunned enemy's line, because `StrikeOn` gives none, and prints `Stunned, no line: <name> (<tile>) skips the enemy phase`.
- **Planners:** neither rider adds a scoring term. The chill had none before (0155), and the stun is read through the board it leaves: a skipping unit plans nothing, and `threat` and `end` see that.
- **Content:** `rules.json` gives ice `{ "kind": "chill" }`, and no shipped tome names it, so no board moves. Lightning has no rider in content: the stun's gate must name a class, and the storm-warden's class does not exist yet (#1247). The tests use fixture tomes, `test_frost` and `test_knell`, in Pell's hands.

## Kill criterion

The stun is cut back if the first play with the storm-warden journals a turn where it erased a threat with no choice spent, meaning the stun was always the right call. The first lever is to make it spend on cast rather than on a land.
