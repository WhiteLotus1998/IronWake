# 0037 — Enemy retreat: a command, a shared predicate, and a header that turns it on

Date: 2026-09-25. Issue 33 (DESIGN 13.10, second in the Table's experiment order). Built by Code; the review is on the issue. The leans below are implementation and reversible. Keep or kill waits on Chat's play and is not decided here.

## Decisions

1. **Retreat is a command.** `Retreat(unit, to)` is applied by the resolver: the unit moves to the tile along its reach path, ends its action, and is marked `Retreated`, a flag that is never cleared. It emits `UnitRetreated(unit, from, to)` and then the `UnitMoved`. "Never twice" is a fact on the board, so a Recall restores it with everything else. The planner emits the command. The player never does.
2. **One predicate.** `RetreatRule` answers both the planner and the resolver, so they cannot disagree. A unit may retreat when the map has the header, it is an enemy whose effective behavior is Aggressive (a woken Guard counts, Hold and Boss never do), it has not moved this phase or retreated this battle, and it is strictly below 30 percent (`hp * 100 < max * 30`; 6 of 22 retreats, 7 does not). The tiles are healing terrain in its reach this phase, which today means Fort and Throne. Its own tile counts. If no tile qualifies, it stands and fights. `Resolver.Legal` lists every allowed tile.
3. **Which tile.** The ordering is fewest player units whose reach set contains the tile (section 8's count), then cost from the mover, then row-major.
4. **Healing.** The existing phase-start rule heals, with no new code: 20 percent at the unit's next phase start.
5. **Opt-in.** A map turns retreat on with the `retreat: on` header. It is off by default, so no gated map and no gate number moves. The sample is `docs/samples/old_mill_road_retreat.map`, which is Old Mill Road plus the header (a test holds it to the shipped file). If the rule is kept, the decision is whether the header becomes the default or stays a per-map tool.
6. **Not touched.** The exposure sum of section 11 still counts a unit that will retreat as a striker. On a retreat map it overcounts, which is the conservative direction. It matters only if the rule is kept.

## Not decided

- Keep or kill. Code's first play (seed 41, PLAYTEST.md) saw the rule fire once and cost nothing: the bandit fell back onto a fort adjacent to the fight and died on the next player phase before it healed. The rule asks a question only when the refuge is out of the party's next reach. That is a map property, not a rule property, so a keep would make retreat a map-authoring tool (the header) rather than a global default.
- Whether a retreat tile should need to be out of every player unit's reach, rather than only preferring it. The requirement would make the rule fire less often, and fire only when it matters.
