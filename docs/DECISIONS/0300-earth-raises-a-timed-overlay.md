# 0300: Earth's raise lays a timed terrain overlay, cast like a heal

Date: 2026-10-07. Issue #1245. Source: Chat round 416 (raised ground, the ward dropped), Code round 417 (agreed), round 155 (defence comes from tiles), 0297 to 0299. Provisional.

## Context

#1245 asked for a generic tile overlay and for earth's rider to use it: cast on an ally's tile as a support action, it lays fort-grade ground with no heal until the end of the caster's next phase, one per caster, never on a map fort, wall, water or impassable tile, held by whoever stands on it.

## Decision

- **The overlay is the battle's map, retiled.** A `TileOverlay` records the tile, the terrain it stands in for, the terrain under it, its owner, its side and its clock. While it lasts, the battle's copy of the map carries the overlay's terrain, the way the drake's Rime ice already does, so every rule that reads terrain reads it with no rule of its own: move cost, Def, Res and Avo, the forecast, `threat`, both planners and the Sim. The map file is never touched. The clock runs on the chill's (`1` until the side's next phase begins, `2` through it), and the overlay falls as that phase ends, giving its tile back whoever stands on it. If a map event retiles the tile meanwhile, the event's terrain stays and the overlay is dropped. A fallen owner's overlay lasts out its clock. Recall restores it with the board. The protocol carries `overlays`.
- **The rider names its terrain.** `raise` takes one field, `terrain`, so ice over a ford or fire left on the ground becomes a new rider or a new terrain entry, not new code. The loader refuses a terrain some movement type cannot enter, or one that wears or thaws. Earth's rider is `{ "kind": "raise", "terrain": "earthwork" }`. Earthwork is a new terrain: fort's Avo 15, Def 2 and Res 2, for flyers too, cost 1 for everyone, no heal, glyph `[`.
- **Cast like a heal.** A tome naming `raise` goes through the Item action with an ally as its target: `item pell <slot> teodor`. The checks are a heal's: the caster may wield it (school and rank), it has a use left, and the target is an ally in its range. The cast spends one use and the caster's action and earns no EXP. It can still be used to attack, plain.
- **Open ground only.** A tile is refused if some movement type cannot enter it (water, wall, mountain), or if it heals (a fort, the gate), burns, wears or thaws. A tile under another caster's overlay is refused too. The refusal names the terrain. Geography never retiles a tuned map.
- **One per caster.** A second cast gives the first tile back before the new one is laid, so a recast on the same tile refreshes its clock.
- **On screen.** The `groundRaised` event prints `Pell raises earthwork under Teodor at 4,7: held by whoever stands on it until the caster's next phase ends`. The board prints `earthwork ([): 4,7 raised by pell, falls as the next player phase ends`. The terrain card adds what raised ground is and each tile's owner and fall. The legend lists the glyph like any terrain.
- **Planners.** The enemy never casts it, and `Resolver.Legal` and the Sim's player do not offer it, so no Sim gate moves. Both read a raised tile as terrain, so an enemy may step onto one its owner left.
- **Content.** No shipped tome names `raise`; earth's spell waits on Lotus's list (#1247). The tests and the hand play use a fixture tome, `test_cairn`, with Pell's Adept given earth.

## Kill criterion

Cut it back if a play journals a turn where a raise made an unwinnable exchange safe with no choice spent: the same unit covered every phase with no cost felt. The first lever is a cast that cannot target the tile it raised last turn.
