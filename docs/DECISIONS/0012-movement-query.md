# 0012 — Movement: the reach query, its tie-break, and the strict budget

Date: 2026-09-16. Ruled by: Code, while building issue 4. Findings were posted on issue 4 before the code. Argue on the PR if any of it reads wrong.

## Occupancy is an input, not a lookup
There is no `BattleState` until issue 6, and the pathfinder should not know one exists. `Movement.Reach` takes the map, the content, the mover's movement type and Mov, the origin, and a function `Coord -> Occupant` (`None`, `Ally`, `Enemy`). Issue 6 answers it from the battle state; `MapDefinition.OccupantAt(at, side)` answers it from starting placements for the map view and the `reach` command. The function is never asked about the origin: the mover's own tile is always a destination.

## Two sets, not one
Section 4 says units pass through allies and never end on an occupied tile. The result therefore distinguishes tiles the unit may end on (`CanEnd`) from tiles it may cross (`CanCross`). Ally tiles are cross-only; enemy tiles are neither. Every entry carries its cost and the tiles walked after the origin.

## The tie-break is a contract
DESIGN.md said nothing about equal-cost paths. The presentation protocol (issue 25) will carry paths to renderers, and the Godot build and the console must draw the same one, so the choice cannot be an accident of the implementation: tiles settle in (cost, row-major) order, neighbours expand north, west, east, south, and a tile's path changes only on a strictly cheaper cost. Written into section 4 and held by a test that pins the northern route around a mountain.

Amended 2026-09-17 (issue 45, the Critic's finding): the neighbour clause does no work. Every step costs at least 1, so every tile at a cost is enqueued before the first tile at that cost settles, and settle order is exactly (cost, row-major); a tile's parent is the row-major-first neighbour that offers it its cost, whatever order the neighbours are enumerated in. The Critic reversed the enumeration and only `Coord`'s own test failed; Code reproduced it. Section 4 now states the contract as the one clause it is, and a theory over Old Mill Road checks every reachable tile's path against it. `Coord.Neighbors()` keeps north, west, east, south for readability, not as a rule.

## The budget is strict
No allowance for entering a tile that costs more than the remaining Mov. Armored with Mov 4 stops after one Hill (3) and does not enter the Forest (2) beyond it. Section 9's corridor authoring depends on reach being countable exactly.

## `ironwake reach`
`ironwake reach <map> <x,y> [<movement>:<mov>]` prints the map view with the destinations marked `*`. An enemy's movement comes from its template's class. A player slot has no class until the roster (issue 13), so it is drawn as infantry with Mov 4, the cadet's numbers, unless overridden; the override also probes an empty tile. This is a reading tool for both partners, not a rule.

## Not decided here
Whether Cavalry gets Canto in Phase 3 changes nothing here: Canto is a second reach query with the remaining Mov. Road's reserved cavalry bonus (section 4) is a content change to the cost column when it comes.
