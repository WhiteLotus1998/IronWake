# 0076 — A group woken at dusk lights its lamps

Date: 2026-09-27. Issue 382 (Chat), from PR #381's Unsure on `threat` at dusk and Code's seed 241 stand transcript. The spec was settled in the issue; this record restates it and says how it was built.

## Decision

1. On a dusk map, a Guard group that a player-phase command wakes (proximity, noise or a death) lights its lamps. Its members are seen by the player side wherever they stand, whatever the sight, until the enemy phase that follows ends. The console draws them with rows, `forecast` and `threat` price them, and the player may strike them and counter them as if they stood in sight.
2. The wake line names them, row-major by tile: `group bank wakes: proximity; its lamps are lit (shieldbearer-1 17,5, soldier-1 17,6, brawler-1 17,7)`. The protocol's `groupWoke` carries them as `lamps`.
3. A group still asleep stays in the dark. A wake in daylight, a wake at a phase's turn (an `EndPhase`), or a wake on the enemy phase lights nothing. What the enemy side knows is unchanged.
4. The lit groups are a fact of the board (`BattleState.LitGroups`, `litGroups` in a protocol state, a `lit` line in the canonical text when any), so a Recall past the waking step puts the lamps out.

## How

`Dusk.Sees` for the player side also sees a tile a lit enemy stands on, so every reader of sight (the resolver's strike and counter checks, `Dusk.Seen`, the renderer, `threat`, the protocol's player view and the Godot client's shading) follows without a second rule. The lamp is carried by the unit, not the tile: a lit member that moves on the enemy phase is reported in full, and the tile it left goes dark.

## Measured

Brackwater Cut, 200 seeds, `--full`, before and after: identical. Gate 1 64 percent, median 6, 72 timeouts; gate 4 ok at 0.158; all eight gates pass. The heuristic plans the same lines, since the bank it wakes is adjacent to whoever woke it.

## Transcripts

The three journaled Brackwater plays whose scripts wake the bank (`2026-09-26-brackwater_cut-53`, `2026-09-27-brackwater_cut-241`, `2026-09-27-brackwater_cut-241-stand`) were regenerated under `--strict`. The games and their endings are unchanged; only the wake line, the rows and, on seed 53, the bank's enemy-phase moves now show what the lamps show. Code's play of the rule is `2026-09-27-brackwater_cut-241-lamps`.
