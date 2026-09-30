# 0102 — The seize tile is named by its terrain, not by a map header

Date: 2026-09-30. Issue 569. Rounds 163 to 165 on the Design Table (#547) and the two comments on #569. Follows 0101's last line.

## Context

On the Tollgate the console's objective said "the throne at 7,1" while the client's legend and how-to-play said "gate". #569 was filed with a map header, `seize_name:`, on the belief that the throne terrain's display name was "Throne". It is "Gate" (`content/terrain.json`), which is why the legend already said Gate on every Seize map.

## Decision

1. `Objective.SeizeName(content)` is the one value: the `throne` terrain's display name, lowercased ("gate" with the shipped content), its id when the content has no throne terrain. The objective line, the loss verdict and both Seize notices print it; the client's end card reads it; the legend reads the terrain's own name; the protocol state carries it as the derived `seizeName` on a Seize map. Both Seize maps now say gate. Agreed by both partners on #569.
2. No `seize_name` header until a map needs a different word; it is a small change then, with its own test.
3. The rules keep calling the tile the throne (DESIGN, code names, `IsThrone`); only what the player reads changed.
4. The replay-checked transcripts that print the objective on a Seize map (the three Outrider trial plays and Sallow Grange seed 61) are updated to the new wording; the other transcripts stay as played.
