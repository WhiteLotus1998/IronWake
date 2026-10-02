# 0179 — Rotten planks (13.25): ground the board spends by walking on it, provisional

Date: 2026-10-02. Proposed on the Design Table as Code's lean (chain-mode experiment run, every listed experiment already tried); Chat argues it on the Table or the PR.

## Why

Wildfire spends ground off a strike (13.15) and the tide on an announced clock (13.21). Nothing yet spends ground by use. A plank bridge is a shared budget of crossings the player can read before turn 1: who crosses, in what order, who goes the long way, and when to drop it behind you are decisions, and an enemy's crossing spends the same budget. It respects round 155 (defence comes from tiles) and adds no player action, so 0099's cost rule does not apply; the cost is the bridge itself.

## The build

- `terrain.json` takes an optional `wearsTo` (validated: it must name another terrain). Planks (`+`) wear to Split planks (`:`, cavalry and armour pay 2 to enter), which wear to Water. Non-letter glyphs, because letters are units on the board.
- `Planks.AfterWalk` (Core): after a Move, a Canto or a retreat, every tile the unit walked off (its start and its path but the last tile) wears one step, two under a horse or armour, none under a flyer; a tile another unit stands on does not wear. One `TerrainChanged` per step, in walk order. No header: a map without wearing terrain is unchanged, and no shipped map has any.
- On screen: the board's `wears:` line (both the map view and the battle view) and the terrain card's `Wears:` sentence. Walks print `7,5 becomes Water` under the move.
- Client: both palettes, LOOK.md, the art spec, the generated tiles (`make_art.py`) and the sheet for Lotus carry the two new terrains. LookPaletteTests' token regex now admits an underscore.
- The enemy planner and the Sim's heuristic are blind to the wear to come, as they are to the tide.

## Kill criterion (agreed before Chat's play, Code's lean)

Killed if in both partners' plays no route, crossing order or stop is chosen for the wear (nobody sent the long way to spare a plank, nobody crosses or holds a tile to drop or keep one), or if either journal says the cut was always free. Kept as an authoring tool for maps if a journal shows one; a map that ships it needs a reason to keep the bridge standing (something wanted on the far side), which the sample lacks.

## The first play

Code, warm, seed 251 on `docs/samples/rotten_bridge_planks.map`: won turn 8, all six out, one Recall (my own misread of a printed lethal counter, not the planks). Turn 1: the bridge holds four foot crossings and Dunstan's armour spends two, so he was sent to the ford. Turn 2: Pell and Ottilie walked off the split planks and the river closed under the rider's nose, which sent the whole chase to the one-file ford, where the captain corked the mouth for two turns. First clause met; the second clause is named against it: once everyone was across, dropping the bridge cost nothing. The next lever is the board, not the rule.
