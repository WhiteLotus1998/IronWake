# 0011 — Map format: two renderings, roster slots, enemy level floor, canonical writer

Date: 2026-09-15. Ruled by: Code, while building issue 3. Provisional where marked. Findings were posted on issue 3 before the code.

## The view and the file are two outputs
Issue 3 asked for a renderer that overlays units on the grid and for output that re-parses as a map file. A grid with `A` and `a` drawn on it has lost the terrain under those tiles, so one string cannot be both.
Ruling: `MapFormat.Write` (Content) emits the `.map` text and is what round-trips and what 13.5 edits. `MapRenderer.Render` (Core) is the console view: the same grid with units overlaid, and a legend that names each letter, its unit, its tile, and the terrain under it. Both draw the grid through `MapDefinition.GlyphRow`.

## Where the code lives
Parsing and writing map text sit in `Ironwake.Content` beside the JSON loader and serializer, since DECISIONS/0005 put all content reading there. The map records and the view sit in Core, because every front end draws the same grid and issue 6 builds `BattleState` from `MapDefinition`.

## `P` lines are roster slots
The roster does not exist until issue 13, so a `P` line cannot be validated against it. A `P` line is `captain`, `recruit:<id>` (this recruit, here), or bare `recruit` (any recruit; the roster fills bare slots in order). Recruit ids are not checked against content; the roster check lands where the roster does. The two new sample maps use bare slots so the cast can be written without editing them.

## `enemy_level` is a floor, not a setting
`Unit.AtLevel` refuses to scale down, and `bandit_leader` is a level-3 template. A template below `enemy_level` is raised to it; one at or above keeps its own level. Default 1, so the design doc's example, which has no `enemy_level`, still parses.

## The writer is canonical and the samples are held to it
`Write(Parse(text))` is a fixed point: fixed header order, `recall` and `enemy_level` always written, `cheap_shots` only when allowed, units in file order. A test rewrites every file under `content/maps/` and fails on any difference. When the game rewrites the finale (13.5), the diff is the edit and nothing else.

## Validation beyond syntax
Positions inside the grid; one unit per tile; exactly one `P captain` (section 7's loss condition needs one); a player slot on a tile infantry can stand on (the captain is infantry, section 9; a recruit's class is unknown at parse time); an enemy on a tile its class's movement type can enter, so a Wingrider may start over water and a Brigand may not; `seize` needs a throne tile; `defeat_boss` needs a `B` line; `E` lines need both `group:` and `behavior:`; a `B` line may say `behavior:boss` or nothing. `trigger:` and every other attribute are errors. Provisional: the infantry rule for player slots may loosen when flyers deploy from water on a later map.

## Renamed in the doc example
`recruit:mira` became `recruit:wren`. CLAUDE.md forbids near-misses of franchise names, and one letter separates the old name from a goddess in the same lineage.

## Not decided here
Whether small maps below section 9's 12x10 floor are allowed (the parser accepts 1..64 on a side; the tutorial maps are "small"), and whether the "maps 4 and up" rule for `cheap_shots` should be enforced anywhere but the Sim's report.
