# 0288 — A map names its seize tile (`seize_name:`)

Date: 2026-10-06. Issue #1208; Design Table #1187, round 408 (Chat's cold Shrine 2240). Restores #569's per-map header, which #574 settled with one content-wide name instead.

## Context

#574 renamed the `throne` terrain to "Gate" in `content/terrain.json`, so every Seize map's tile printed as a gate. On the First Shrine the card sends Maud to the altar while the objective line said `Get Maud to the gate alive` and her row printed `Gate (heals 20 percent, 3 hp)`. No card or scene text could fix it.

## Decided

- **`seize_name: <words>`**, an optional map header, Seize maps only, lowercase words a to z separated by single spaces. A bad value or a non-Seize map is refused naming the file and line. Absent, the name is the throne terrain's, lowercased, as before, so no other map changes.
- **Two choke points.** `Objective.SeizeName(map, content)` feeds the objective line, `help`'s rules, both loss verdicts, the Seize notices, the protocol's `seizeName` and the client's end card. `MapDefinition.Terrain(id, content)` (read by `TerrainAt` and `TerrainCard.Text`) renames the throne terrain with a capital first letter, so unit rows, `look`, `inspect`, forecasts and the terrain card agree. No rule reads the name.
- **The map writer emits it right after `win:`**, so a canonical map round-trips.
- **The First Shrine sets `seize_name: altar`.** Other names belong to the Fable pass; this is a hook, not new story text.

## Tests

`SeizeNameTests`: the header's name in the objective, rules, tile, terrain card, protocol, map writer, verdict and end card; a map without it still says gate; four refused values; the Shrine reads altar. Transcripts 875, 1530 and 2130-chat were regenerated with `rejournal.py`: the same plays, with only the objective line and Maud's tile row changed.
