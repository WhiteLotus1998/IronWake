# 0104 — Generated art, slice 1: tokens and tiles from a stdlib script

Date: 2026-09-30. Issue 564 (Lotus's request, relayed): a generator that draws every sprite the game needs from coded shapes and writes it at the art spec's names; the bar is consistent and readable, not final, since Lotus or an artist fixes what needs fixing. The issue leaves the tool and the slicing to the Builder. Everything here is implementation and reversible.

## Decided

- **The tool is Python with the standard library only** (`docs/art/make_art.py`), the same shape as `docs/sound/make_sounds.py`: Pillow is not in the sandbox, and a rasteriser of discs, polygons, round-capped strokes and rings at pixel centres is under two hundred lines. It writes PNG with `zlib`, and the same bytes on every run.
- **Hard edges, no anti-aliasing.** ART_SPEC says flat shapes with hard edges and at most a base and a shade per colour. Every generated pixel is a palette value, a LOOK.md mix of one, or clear, so `ArtGeneratedTests` can hold the files to the look exactly: a token is its side's values (the enemy's ink shadow at the client's 0.55), with the HP bar's 16 rows clear; a tile is its terrain's colour with ink, frost or iron laid over it at 20 to 65 percent, so terrain never adds a hue; fire is an ember hatch on clear ground. The client's downscale from 2x does the smoothing.
- **Slice 1 is the tokens and the tiles** at 2x (96 x 96): 18 tokens (every class, both sides) and 10 tiles (every terrain). The shapes are the ones the client already draws (`Main.Look.cs`), so a file dropped in reads as the look both chairs scored. Chaplain, skyrider and bulwark keep the client's first shapes (a staff with a ring, wings over a shaft, a shield); ART_SPEC still asks an artist to propose theirs.
- **Tiles join the spec's name list** as `tile_<terrain>` (`ArtSpec.Tiles`), after the tokens. The grid and the north light's shadows stay the renderer's, since they depend on the neighbours.
- **The captain's token is not generated**: Lotus is modelling Alder Fenn himself.
- **`generated.txt`** beside the files lists what the script owns. A delivered file replaces a generated one by name and comes off the list, so the exact-colour test never judges an artist's anti-aliased edges.
- **The client does not load the files yet.** Wiring them changes the showcase's scored frames, so it is its own slice with re-rendered frames for both chairs.

## Next slices

The client loads a token or tile by name when the file exists (frames re-rendered and scored); the Tollgate's two boss variants (the warden's hooked pike, the leader's double bit) need token rows of their own before that, since one `token_pikeman_enemy` cannot carry both; then the battle clips with #535; then the effects; then the `for-lotus` contact sheet.
