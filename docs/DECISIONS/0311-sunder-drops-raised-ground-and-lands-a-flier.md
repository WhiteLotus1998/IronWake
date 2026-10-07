# 0311: Sunder drops raised ground and lands a flier

Date: 2026-10-07. Issue #1281. Source: round 416 (Sunder drops a raised tile), Lotus's #1247 rulings relayed on #1251 (0307: "Sunder also grounds a flier and stuns it for a turn"). Provisional: the reading goes back to Lotus with the revised #1247.

## Context

Earth's school rider is `raise` (0300). Sunder needed two things from one tome: undoing a raised tile, and Lotus's flier clause. Ember (0308) had already shown the shape: a tome-only kind that borrows its school's rider.

## Decision

- **A tome-only kind.** `sunder` is named by a tome on a school whose rider is `raise`, as `ember` is on `burn` (`SchoolRider.Borrows`). A school's own rider is never a sunder, and a tome naming it on any other school is refused at load. One tome cannot both raise and sunder.
- **The drop is the Item action.** `item <unit> <slot> <unit|x,y>`: a unit of either side standing on the tile, or the tile itself, in the tome's range. Any overlay drops at once (earthwork now, Rampart when #1282 lands), whoever raised it. The tile gives its ground back, and whoever stands there stays. A tile that was never raised is refused, and a map's own fort is one. It spends a use and the action and earns no EXP (`groundSundered`, then `terrainChanged`).
- **The hit is an attack.** A hit on a flier still standing grounds it (Grounding's clock, as a bow's crit sets) and stuns it for its side's next phase (the stun's clock, counters kept). A boss is grounded and spared the stun. Anything that does not fly takes only the hit. Unlike lightning's stun it is not once a map per caster: the tome's uses are the limit. The forecast prints ` grounds and stuns` (` grounds (stun: bosses spared)`).
- **Uses: the lean is two a map.** That is a tome's `durability` (uses per battle), shared by drops and strikes, and the fixture carries 2. No shipped tome names `sunder` until Lotus signs.
- **Planners.** The enemy never casts the drop, and `Legal` and the Sim's player do not offer it. Neither planner prices the grounding or the stun, since no content puts the tome in anyone's hands yet.

## Kill criterion

If a play journals a flier taken out of a map by one cast with no answer, so the stun reads as a free kill, the stun goes first and the grounding stays. If nobody ever drops a tile because attacking with the tome is always worth more, the drop takes no action (a free cast before moving) as the lever.
