# 0303: The held bar, and what it blocks waits (the Lazar House sample)

Date: 2026-10-07. Issue #1259. Source: Design Table #1251, rounds 420 to 422 (Code's warm 1590, Chat's cold 4204 and the held-bar lean, Code's waiting queue, Chat's reach condition). Provisional, a sample only; this restates what the Table agreed and records the shapes.

## Context

On the shipped Lazar House a stop on 8,1 or 10,4 walls a lane for the rest of the map, so the map's best turn (the turn-2 bar) also ends it. Round 421 agreed a held bar plus a waiting queue as a retune lever, not a spike (0237), built on a sample: a bar shuts its lane only while someone stands on it, and an arrival a bar blocks is not spent but waits. Round 422 added a build condition: each bar tile in reach of an enemy still standing on turns 5 and 6, on screen.

## Decision

- **`held`** ends a `terrain` action on a one-tile `enter` trigger (the loader refuses any other trigger). The change records a bar (`bars` in the state and the protocol: event, holder tile, tile, terrain, the terrain under it). After every accepted command, a bar whose holder tile has no player unit on it gives its terrain back (`barReleased`, then `terrainChanged`), and the event is unfired, so the next stop fires it again. A held change its occupant blocks is not spent either.
- **`arrivals: wait`** (header; refused without a non-boss spawn): a non-boss spawn whose tile holds a unit or terrain it cannot stand on joins `waiting` (`arrivalWaits`). At each enemy phase start, before that phase's own events, the oldest waiting arrival of each open tile lands, one a tile, so a lane released on turn 4 lets in one arrival on turn 4 and the next on turn 5. A boss never waits (he already lands on the nearest free tile).
- **On screen.** The board prints one line per held event, `north bar (8,0): barred while teodor holds 8,1; waiting: brigand` or `open, barred only while one of yours holds 8,1`, and `waiting at <tile>: ...` for a waiting tile with no bar. The announce lines say `8,0 becomes wall while one of yours stays on 8,1; it gives way when 8,1 is left` and `A unit or a wall on 8,0 holds it back, and it lands at the first enemy phase that starts with 8,0 open`. `threat ... from` applies the release after the stop, so a read off the bar prices the next waiting arrival landing, and only that one (issue 1258's read, extended).
- **The reach condition.** The sample adds one arrival, `east3`: an archer on 11,2 in turn 5's enemy phase, which no bar closes and which reaches both holder tiles (10,1 for 8,1, 10,2 for 10,4) on turns 5 and 6. It is announced like every arrival.
- **A spawn on a retiled tile.** The map loader lets a spawn stand on terrain its template cannot enter when an event in the block retiles that tile, since a battle's map written back (the protocol state) carries the wall over the lane. This also fixes the shipped map's state read once its permanent bar is down.
- Nothing else moves: `content/maps` and `content/quests` are untouched, the Sim's player never bars, and no other map uses either flag.

## Kill criterion (from #1259)

Kept if a journal names a turn where a unit stayed on or stepped off a bar for a price (the ally's HP, a heal, a kill). Killed if both plays hold both bars from the turn they are first taken to the end with no step-off weighed, or if a play loses to a released arrival `threat` did not price. If killed, Chat's fallback: permanent bars, the north bar moved to the hill at 9,1.
