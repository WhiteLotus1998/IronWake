# 0227 — The grounds land per region; outland sand is a scoped warm exception (#916)

Date: 2026-10-04. Built by the chain Builder. Restates Lotus's pick on #892 (relayed on #875, comment 5975277196) and rounds 307 to 310 (Chat 307 and 310, Code 309). The code shape below is the build's.

## What was agreed

- Frost `#949A9A`, with snow as detail, is the seam's ground. Cold moss `#649470` is every other region's ground. Peat `#7E9470` is retired, and heather never ships.
- Sand `#A89670` is the outland region's ground. It is not a terrain id (round 309, agreed in 310). It is the one warm ground, a scoped exception: the warm-ground test names it, and on it the threat hatch and the player's discs carry a 1px `ui.ink` edge. The exception counts as kept only once both partners have looked at a frame that shows the awake and sleeping hatch on sand next to the old hill, with a road across the sand (round 310).

## What is built

- **`region: <seam|sallow|aldmere|kestrow|outland>`** (DESIGN 10; `MapRegion`, `MapDefinition.Region`). Absent means the seam, so the writer omits it there. An unknown word is refused, naming the file and the field. No rule reads it. The protocol carries it inside the state's `map` text.
- **Tags, from #892's list.** `sallow`: Saltmarsh Ford, Sallow Grange, Brackwater Cut. `aldmere`: Harrow Weir. Every other campaign map, quest and sample stays on the seam. No layout changed. Three sample-diff tests drop the shipped map's `region:` line, the same way `keziah_warning` is already dropped: the samples are the rule experiments, not the place.
- **The palette** (`LookPalette`). `Grounds` holds frost, moss and sand. `GroundOf(region)` and `TerrainIn(id, region)` give the colour plain is drawn in. `Terrain["plain"]` is frost, the seam's ground. LOOK.md's table lists `ground.frost`, `ground.moss` and `ground.sand` in place of `terrain.plain`. `OutlandGroundHolds` (chroma 32 or less, L under 66) refuses `#C0A878`. `EdgedOn(ground)` is true exactly where `IsWarmGround` is, including the old hill `#B89E6C`.
- **Tests.** Each region's ground stands 12 from every other terrain, 20 from both sides and 12 from every mark and bone, and stays under chroma 32. `NoGroundButFireAndTheOutlandSandIsWarm` replaces `NoGroundButFireIsWarm`: the warm regions are exactly `{outland}`, and a second warm ground fails it.
- **The client** (Godot). Plain is drawn in the map's ground. The seam's frost gets snow drifts of the throne's white at 0.38, placed by tile coordinate. On a warm ground each hatch stroke sits on an ink stroke two pixels wider, and player discs get an ink hairline. The terrain legend, the dusk legend and the battle backdrop use the region's ground.
- **Art and frames.** `make_art.py`'s plain is frost (`tile_plain.png` regenerated). `draw.py`'s plain is frost (the three `docs/look/*.svg` and their PNGs redrawn). `docs/look/sand.py` draws the gate frame `docs/look/sand-916.png`.

## What the frame shows (Code's look; Chat's owed)

On the old hill without the edge, the bone hatch reads as a cream-peach tint at 0.5 and a faint warm wash at 0.2, which is round 166's failure again. On the sand with the edge, it reads as a grey mark at both alphas and never as a tint. The road across the sand stays legible under it. The edge makes the 0.5 hatch heavier on sand than bone is on frost or moss. That costs some of its lightness, but it never reads as ours. The exception is not kept until Chat has looked too.

## If it fails

If the sleeping hatch still reads peach on sand to either chair, the next lever is the hatch drawn in slate on sand (#578's rule, scoped to warm ground).
