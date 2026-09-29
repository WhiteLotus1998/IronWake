# 0089 — Overwatch, spiked

Date: 2026-09-29. DESIGN.md 13.17, issue 481, proposed by Code in round 112 on the Design Table (#470) and amended in rounds 113 to 115 (the issue's three comments carry every amendment). Provisional in the ordinary way; nothing here touches a shipped map.

## Decision

On a map with the `overwatch: on` header:

- `Watch` is a command: an action in place of Attack, Item or Wait, after a Move or without one, for a unit whose equipped weapon reaches range 2 (`Overwatch.Refusal`, `RejectionReason.CannotWatch`). It sets `BattleUnit.Watching`, emits `WatchTaken` with the best legal strike it passes up (target and displayed hit), and opens no Canto. It is not a Wait, so it never braces; a map with both `overwatch` and `brace` is refused on load.
- The ring is every tile exactly two steps from the watcher (`Overwatch.InRing`), for every weapon (round 113, Chat's A).
- After a Move, or a Canto that leaves the tile, `Resolver.FireWatches` shoots the mover once from each living watcher of the other side whose ring holds the tile and whose side sees it, in id order, until one kills: `Overwatch.Shoot`, the watcher's forecast hit, damage and crit, no counter, no double, keyed under `RollKey.Watch`. `WatchFired`. The watch is spent. The shot spends a use and earns EXP, rank and mastery; a kill is a death with its keepsake and grudges. A shove, a spawn and a Retreat are not moves.
- Any strike on a watcher, hit or miss, ends its watch (`WatchEnded`). Every watch ends when its side's next phase begins.
- The enemy planner watches in place of Waiting whenever it has no strike and its equipped weapon reaches 2, except a Guard whose group sleeps (`EnemyAi.Idle`). Its ring pricing, in the issue's body, is not built: round 114 agreed no planner change in the spike, so the bent-move count reads 0 by construction and is not printed. The enemy's plan loop stops a unit's commands when a shot kills it.
- The Sim's heuristic watches under the same rule, and the runner replans when a shot kills its mover. Gate 1 prints, on an overwatch map only, per-game means per side of watches taken, watches taken over a legal strike at 50 or more (the heuristic's is 0 by construction), and shots fired.
- On screen: a legend, a `watches:` line naming each watcher and its ring, rows marked `watching`, and the three events in the console, the protocol (`watch` command, `watching` unit field, `watchTaken`, `watchFired`, `watchEnded`) and the Sim's trace.

Samples: `docs/samples/the_tollgate_overwatch.map` (the Tollgate, Wren swapped for Ottilie) and `docs/samples/harrow_weir_overwatch.map` (the shipped crest file). Measured at 40 seeds: the Tollgate sample 8/40 against 9/40 for the same roster without the header (the shipped roster wins 32/40, so the swap, not the watch, is the drop); enemy 18.7 watches and 4.8 shots a game, player 2.6 and 0.1. Harrow Weir 20/40 against 22/40 plain; enemy 9.8 watches and 0.6 shots, player 4.4 and 0.1.

## Not built, and why

`threat` and `forecast` do not price a watch shot on a destination tile yet (the body's `watched by archer-1 ...` line), and the board grid carries no `w` glyph; the `watches:` line prints the rings. A windup blow landing on a watcher does not end its watch. Both are follow-ups if the spike survives its plays.
