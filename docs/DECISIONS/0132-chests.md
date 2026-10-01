# 0132 — Chests: a tile, an action, everything into the opener's pack

Date: 2026-10-01. Issue #649. The shape comes from Lotus's wish (round 192, item 9) and Chat's round 193, as the issue body gives them. Where the issue left an implementation choice, the Builder's leans are below; Chat can argue them on the PR.

## Decided

- **Format.** The `chests:` block sits between `units:` and `events:`, one line per chest, `x,y item [item ...]`. A chest holds one to five ids from `weapons.json` or `items.json`, and there is one chest per tile. A signature item (`boundTo`) is refused, because it is lost with its owner and never found. Chests are authored on the map (`MapDefinition.Chests`). Which ones are open is battle state (`BattleState.Opened`), so a Recall shuts a chest again.
- **The action** is `Open(unit, at)`, the shape of `recover` (13.8):
  - The opener is a player unit that has not acted, on the chest's tile or orthogonally beside it, after a Move or without one.
  - It is an action in place of Attack, Item or Wait, and no Canto follows.
  - The refusals are `CannotOpen`: none at the tile, already open, out of reach, no room, or an enemy.
- **All or nothing (lean).** No convoy exists, so everything goes to the opener's pack at full uses, and the chest stays shut unless all of it fits. A half-emptied chest would need its contents in battle state and a second action to finish. The campaign record already keeps the survivors' packs, so what is found reaches the camp with no new code.
- **On screen.** A closed chest is drawn as `$`, and `chests ($): <tile> <contents>; ...` is printed with the rule. The event line reads `<name> opens the chest at x,y: <contents>`. The protocol carries:
  - the `open` command;
  - the `chestOpened` event;
  - `chests` in the state, each with `at`, `items` and `open`.
- **The enemy never opens a chest**, and the planner and the Sim heuristic ignore chests. The random player of gates 2 and 8 can draw `open` from `Legal`. No shipped map has a chest, so no gate moves.
- **Shapes.** Four shapes work in today's engine: guarded, past a sleeping group, the tide window and the one switch. Wildfire and the ally push are not gates yet (DESIGN 10).
- **Sample.** `docs/samples/strongbox_chests.map` has one chest per supported shape. Code's warm play on seed 649 won on turn 12. The tide chest changed a turn, and the camp's lance was dead weight once Teodor fell. The guarded vault taught that a Guard elite chases, so a chest meant to be held wants a boss under the veto, who goes home.

## Not done here

- **Materials** wait on the forge (#647), and the Kinsbane chest waits on #656 and the midpoint.
- **The Godot client** does not draw chests or offer the action yet. The protocol carries everything it needs.
- **Lotus's three puzzles** are asked for on the Table against these shapes.
