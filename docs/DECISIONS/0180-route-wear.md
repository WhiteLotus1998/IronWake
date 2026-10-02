# 0180 — The player owns the route on wearing ground (13.25, issue 782), provisional

Date: 2026-10-02. Chat filed #782 from its seed-4 play of `docs/samples/rotten_bridge_planks.map`; round 238 on the Design Table agreed it and picked `move ... preview` for the third ask. This record only builds what the Table agreed.

## Why

Round 237 made the route the decision on a plank bridge, but inside one `move` the route was the Dijkstra parent chain of DESIGN section 4 (issue 45's row-major tie-break). On the sample's turn 1, `move dunstan 7,5` went down column 6 and dropped 6,5 to Water with an equally short plank-free route through allies on offer, and nothing on screen said so first. The kill criterion's first clause ("nobody chooses a route for the wear") was rigged toward kill while the route was not the player's.

## The build

- **Tie-break.** `Movement.Reach` settles tiles in (cost, wear, row-major) order. A route's wear is charged when it leaves a tile: `Movement.WearOnLeaving`, the steps `Planks.Steps` gives the movement type held to what the tile's chain has left, 0 on a tile an ally stands on and on terrain without `wearsTo`. A tile's parent changes for a strictly cheaper cost or, at equal cost, strictly less wear. Lexicographic pairs add componentwise, so the search stays exact. On ground that never wears every wear is 0 and every path is the one issue 45 fixed, so no shipped map's path moves (the full suite and both planks transcripts replay unchanged unless noted in the PR).
- **One code path.** The move, the Canto, the retreat, the enemy planner and the Sim all read `Reach`, so the enemy takes the same tie-break without its own rule; it stays blind to the wear to come (0179).
- **`move <unit> <x,y> via <x,y>`.** `Move` takes an optional `Via`; `BattleState.RouteVia` joins the reach's path to the waypoint with a reach from it on the Mov left, the mover's start tile read as empty. Refused (`outOfReach`) naming the leg that fails; it may not end on an ally; it may end where it began. Canto reads the total cost. The protocol's `move` carries an optional `via`; the client's script reader and both script writers know it.
- **Preview.** `Queries.PreviewMove` applies the move to the state as it stands and reads back the walk and the plank wear on the tiles it left (a map event's terrain change is not wear), so the preview cannot disagree with the walk. The console prints `preview: Dunstan would move 6,8 -> 7,5 via 6,7 6,6 7,6; wears nothing` or `...; would wear 6,5 (Water)`, one tile each with the terrain it ends as. The protocol does not carry it yet; the client draws none of it.

## Not done

- `canto ... via` and a via on Fall back: no board asks for them.
- More than one waypoint: one is enough on every board we have (Chat, #782).
