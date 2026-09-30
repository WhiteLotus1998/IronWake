# 0109 — Generated art, slice 6: the client loads the tokens and tiles

Date: 2026-09-30. Issue 564, slice 6. Implementation; reversible.

## Decided

- **The board reads its art from files.** `Main.Art.cs` reads `assets/art/<name>.png` from the source tree, or the imported resource in an exported build, and scales each file once per size with Lanczos, so a 96 px file drawn on the Tollgate's 45 px tile is not resampled every frame. A name with no file keeps the vector placeholder, which is how the captain (Lotus's own token) still draws.
- **Which file a unit tries is `ArtSpec.TokenFiles`**, pure and tested: the captain only his own token, never the class's disc; an enemy with a tell its variant, then its class token; everyone else their class token for their side.
- **ART_SPEC's geometry is the board's geometry now.** The disc is 32 px centred at y 21 of the 48 px frame, as the files draw it; the placeholder moves to the same disc (it was 28 px at y 19), so the captain matches the others. The HP bar takes the frame's last 8 px, and the name shown on hover or selection sits under it, a few pixels onto the tile below.
- **The client keeps drawing what depends on state or neighbours:** the enemy's hairline rim, the boss's dashed ring, the captain's crown, selection, the acted sink (a modulate toward the ink, the placeholder's own 0.45), the grid, the north light's shadows and the wall's lit top edge.
- **Round 138 holds with baked tiles.** A tile file is its ground and its ink detail in one image, so the detail pass draws the same file again with its ground colour cleared, over the threat hatch; the pines still sit on the danger.
- **Fire** is forest ground with `tile_fire`'s hatch laid over it, as before.
- **The scored frames are re-rendered** (`docs/screenshots/render.sh`, Godot 4.3 .NET under xvfb), so both chairs score the board again from them.

## Unsure

- Whether the name spilling onto the tile below reads cleanly next to a unit there; the lever is the name over the disc instead of under the bar.
