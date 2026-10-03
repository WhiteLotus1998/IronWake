# 0223 — Figures keep their own colour; the tint is for placeholders (amends 0105)

Date: 2026-10-03. Issue 891, from Lotus's art direction (relayed on #875) and rounds 298 (Chat) and 299 (Code), both of which agreed it. Docs only.

## Decided

- **A delivered figure carries its own colours.** This covers a battle clip, a level-up pose and a portrait made by hand, whether Lotus renders it in Blender or it comes from a human-made pack. Every colour stays at chroma 32 or less, the world ceiling `LookPaletteTests` already holds the board to. A player-side recruit wears one amber accent (`player` or `player.deep`), such as a scarf, a lamp or a binding. Enemies are slate and bone. Kinsbane and Rook's drake are the only exceptions: they may use pure black and the highest contrast on screen.
- **The side is read from the token base and rim** the client already draws, and from the accent. The accent is a help, never the only cue, so a pose that hides it still reads, and the colour-vision guarantee never depends on a scarf.
- **The tint stays for the generated placeholder set only.** 0105 drew the clips in three greys so that one sheet could serve both sides. A tint over a hand-painted render would flatten the detail it was made for.
- **The style that goes with it** is in ART_SPEC's Figures section, summarised in LOOK. A cold north key, a warm low fill for the player only, a bone rim for the enemy, shading posterized to 3 or 4 steps, an ink outline heavier on the silhouette, oversized weapons and hands, and heads about a sixth of the body. The Blender-to-sprite note sits beside it, and #816's pack filter sits in the delivery rules.

## Not yet in the client

`Main.Battle.cs` still tints every sheet it draws. The follow-up issue (filed with this record) changes it to tint only the names on `generated.txt` and draw every other sheet at a white modulate with its alpha kept. It must land before the first delivered sheet goes in.
