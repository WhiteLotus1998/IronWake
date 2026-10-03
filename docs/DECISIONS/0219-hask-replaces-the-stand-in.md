# 0219 — Hask replaces the finale's stand-in lord

Date: 2026-10-03. Issue #806, slice 1 (items 1, 2 and 5's status). STORY draft 6 (Hask, the reseal) is the spine; the shapes below are the Builder's and are provisional.

## Decided (the issue, restated)

- Hask replaces `finale_lord` under the same content-test bounds (DECISIONS/0150; `FinaleStrengthTests`).
- The shard in his pommel is on the board without anyone saying so: his card and his weapon's description name it, and no mechanic rides on it.

## Decided (the Builder, provisional)

- **The template is `hask`, name Hask,** with the stand-in's class (bulwark), level, stats, growths and ranks unchanged, so 0151's finale measure and the pair play's numbers stand. A class that carries a sword (a pommel in the plain sense) would move the finale's numbers, which is a tuning round, not this issue. The name is on #622's list like every other.
- **His weapon is the Warden's Lance (`wardens_lance`):** the steel lance's numbers, rank D, no price (never stocked, like the Toll weapons), not frozen iron (no chill). Its description: "Garrison steel, thirty years carried. Frozen iron is set in the pommel."
- **A unit may carry a `description`,** one line of at most 72 characters under the item rule (issue 650), validated with file, entry and field, printed as the second line of `show <unit>` (and so of the Godot unit panel). It is a template's text; the save does not carry it. Hask's: "The Iron Warden. The shard in his lance's pommel is frozen iron."
- **A template may be `named: true`,** one person rather than a kind. Text that announces it uses the name as written: `the boss, Hask, arrives at 0,6`, `stops Hask`, never `a hask`. The first regeneration of the pair transcript printed `the boss, a hask`, which is why the flag exists.
- **The pair play's transcript is regenerated** on its seed and script (`finale_lord-1` typed as `hask-1`); the diff is the name and the board table's column width only.
- ART_SPEC's boss list says his set shows the shard, drawn as frozen iron is drawn, once a shipped map places him.

## Not built here

- Item 3, the last iron in on the reseal card ("Same name. Different iron."): #634.
- Item 4, Under the Hill's shard: #790, which reads the same object.
- Item 5, his last line: nothing in the game speaks a line when a unit falls yet, so no placeholder slot exists. It lands with the finale's ending (#634), marked and listed in the `for-lotus` note's known rough section then.

## Kill / revisit

If the partners want the pommel literal, Hask's class becomes one that wields a sword, measured by `--finale` and `FinaleStrengthTests` before it ships.
