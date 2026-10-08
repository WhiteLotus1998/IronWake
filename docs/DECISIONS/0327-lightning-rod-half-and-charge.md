# 0327: Lightning Rod's half damage and its charge, engine

Date: 2026-10-08. Issue #1329 slice 2. Source: Lotus's round-3 spell rulings, settled on the Table in 0322 (rounds 443, 444). Provisional.

## Context

Slice 1 (0326) built Spark Storm's area and its mark. The rod (#1280, 0309) caught a spell aimed at an ally within 2 and took it at full damage. Lotus ruled that a caught spell does half to the holder, and that the catch charges his next lightning spell with +25%. 0322 settled that status and spell multipliers multiply on final damage, rounded down once.

## Decision

- **One final-damage multiple per side** (`DamageScale`, a fraction on `SideForecast.Scale`). Effectiveness stays on Mt. `Damage`, `CritDamage`, `MarkedDamage` and `MarkedCritDamage` each take the product of every multiple applied to the unscaled damage, after the crit, rounded down once. The mark joins the product only on its first hit. A forecast nothing scales is unchanged.
- **The half:** the strikes of a spell the rod caught deal x0.5 to the holder (`Combatant.Catching`, set wherever `Catcher` redirects: the resolver, the forecast, the planner). His counter is plain.
- **The charge:** a holder still standing after a catch is charged for the rod's school (`BattleUnit.RodCharge`, `rodCharged`). His next strike with a tome of that school deals x1.25 on every strike of that cast. That covers an attack, an area cast or a watch shot. The cast spends the charge, hit or miss (`rodChargeSpent`). A counter neither reads nor spends it (`ToCombatant` sets `Charged` only when not countering). A second catch while he is charged gives no second charge. The charge is board state, so it ends with the map and Recall restores it.
- **Shown:** the forecast suffix ` (caught x0.5)`, ` (charged x1.25)`, or ` (charged x1.25, caught x0.5)`; the card `charged: next lightning x1.25`; the protocol `rodCharge` on a unit, `unscaled` and `scaleNumerator`/`scaleDenominator` on a forecast side, and the two events. The enemy planner scores both through its forecast.

## Unsure

- Whether the holder's own counter in the catch combat should spend a charge he already held. Lean: no, a counter is not a cast, as the ruling's "his next lightning spell" reads.
- A watch shot counts as a cast. Overwatch is killed on the main maps, so this matters only on its samples.

## Kill criterion

If a play with a shipped rod-holder never sees him take a catch on purpose, or the charge is never spent on a turn chosen for it, the pay is too small to plan around. Retune the number before cutting it.
