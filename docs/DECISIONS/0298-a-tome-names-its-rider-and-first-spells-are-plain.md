# 0298: A tome names its rider, the school sets its numbers, and first spells are plain

Date: 2026-10-07. Issue #1250. Design Table #1218, rounds 418 (Chat) and 419 (Code). Amends 0297. Provisional.

## Context

0297 put a rider on the school, so every tome of the school carried it, and shipped fire dark because a burning Cinder lost Chat's cold chair 3971 on Starting Alone and moved the tuned Tollgate. Round 418 read Lotus's follow-up ruling: the first spell of every school is weak, and power comes from dropped grimoires. Cinder is fire's first spell. A burn on it makes the bottom rung the strongest thing fire does. And a rider on the school means every fire tome burns, Cinder included, forever.

## Decision

- **A tome opts in.** A Lore tome may name `"rider": "<kind>"` in `weapons.json`. It carries its school's rider only then (`GameContent.RiderOf`). A tome that names none is plain, whatever its school carries.
- **The school sets the numbers.** `rules.json`'s `schools` block keeps the shape (`kind`, `amount`, `phases`). A tome may name only the kind its own school carries, so a grimoire cannot invent its own burn.
- **Load errors** name `weapons.json`, the tome and `rider`: a rider on an unschooled tome, an unknown kind, a school with no rider, a school whose rider is another kind. The serializer writes the tome's `rider`.
- **Fire's rider ships as data** (`burn`, 2, 2), and no shipped tome names it. Cinder is plain fire; the burn ships on the first fire grimoire Lotus signs (#1247). Nothing on the board moves: every transcript and the `--smoke` numbers are unchanged.
- **No map edit to Starting Alone.** 0297's off state is now a decision, not a stopgap.

## Kill criterion

None of its own. If Lotus's spell list wants a school-wide rider after all, a tome naming its kind by default is a loader change, not a rewrite.
