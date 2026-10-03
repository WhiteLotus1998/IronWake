# LOOK — how Ironwake is drawn

The showcase's style sheet (epic #509, slice 0 #510, DECISIONS/0092). Provisional until Lotus reads the mocked frame; the partners' scores decide after a week of silence. Tone from CLAUDE.md: grounded, a little dry, warm underneath. The mocked frame: `docs/look/the_tollgate-turn1.png` (seed 113, turn 1, the captain selected, 4,9 hovered). From slice 1 (#511) the client draws in the look: `docs/screenshots/the_tollgate-113-turn1.png`, `-turn3.png` (the forecast priced), `-threat.png` (the enemy phase with the hatch on) and the 4x crops beside them.

## The one rule

**The world is cold, the player is warm, the enemy lives inside the cold.** Lamplight amber is the only warm colour on the board, so "whose side" is one temperature, not two hues. The enemy's threat is carried by the blade in its silhouette and the threat overlay, never by a hot red. Amber against slate is the blue-yellow axis, the one deuteranopia and protanopia keep.

Fire is the one warm thing in the world (wildfire, 13.15, not on the showcase's Tollgate). It is never a flat fill: a burning tile is its ground colour under an ember hatch that flickers, so no burning tile reads as one of ours (round 129).

## Palette

**Lighter plain (issue 533).** Plain was lifted from `#748A66` (CIELAB L 54.9) to `#7E9470` (L 58.9), its hue and chroma held, one step under where `LookPaletteTests` first fails. The limit is hill, not road or amber: at `#7E9470` plain sits 12.1 from hill at its worst simulated vision, and at L 59.4 it would sit 11.8, under the 12 terrain floor. Against road it stays 14.0 apart and against amber (L 72) 43.1, so neither binds. A lighter plain than this needs hill moved too.

**Cooled hill (issue 564, round 170).** Hill was `#B89E6C`, a tan-gold that read as ours under every amber token and turned the bone hatch peach (#578 drew the hatch in slate over it). It is now `#C8C8A0`, a pale dun (L 79.7, chroma 21.1, red over blue 40, not warm), the swell kept. Plain no longer binds on it: its nearest ground is road at 13.1. With no warm ground left the slate-hatch rule is retired; `NoGroundButFireIsWarm` keeps it from being needed again.

`Ironwake.Client.LookPalette` holds these values; `LookPaletteTests` fails if this table, the class, or any colour in `docs/look/*.svg` disagree, and runs the readability pass's CIE76 and colour-vision checks on them (terrain 12 apart, sides 40, a side 20 from any ground it stands on, marks 12), plus the warmth rule: the player at chroma 55 or more, every other token at 32 or less, fire exempt as hatched. The client draws with it since slice 1 (#511); `Palette` stays for the colour-vision arithmetic it holds and its tests.

| token | value | use |
|---|---|---|
| `terrain.plain` | `#7E9470` | peat green, the board's ground; lightened from `#748A66` (issue 533) |
| `terrain.road` | `#B3AE9C` | salt grey |
| `terrain.forest` | `#4F6E54` | peat, between plain and water, three ink pines |
| `terrain.hill` | `#C8C8A0` | dry heath, one ink swell |
| `terrain.mountain` | `#77767C` | iron, an ink ridge |
| `terrain.water` | `#41667F` | slate water, two frost ripples |
| `terrain.fort` | `#9FB0C4` | cold stone, an ink battlement |
| `terrain.wall` | `#23272E` | near-black, faint iron coursing |
| `terrain.throne` | `#E4E7EA` | the gate: the palest tile, an ink arch, the eye's target on a seize |
| `terrain.fire` | `#943C0C` | ember, hatched over the tile's ground, never a fill |
| `terrain.planks` | `#585030` | dark weathered boards, ink seams (rotten planks, 13.25, samples only) |
| `terrain.split_planks` | `#383010` | the same boards near black, wet and sagging, a broken seam |
| `terrain.rime` | `#6090C0` | water frozen pale, frost cracks (the drake's breath, issue 805, samples only) |
| `player` | `#E8A33D` | lamplight amber: the player's tokens, HP, numerals |
| `player.deep` | `#9A6420` | the amber token's base and shadow |
| `enemy` | `#2F3742` | slate: the enemy's token |
| `enemy.bone` | `#E6E0D0` | bone: the enemy's silhouette, its hairline rim, HP and numerals, the enemy phase's marks |
| `mark.selected` | `#F6D38A` | the selected unit, the hovered tile (dashed), the planned path (dotted) |
| `mark.reach` | `#BFD9EA` | frost: tiles the selected unit can reach, a mark laid on the tile (a wash at about 16 percent, an inset edge), never a surface |
| `mark.threat` | `#EDE6D6` | the enemy's reach, drawn as a diagonal hatch, never a fill; a sleeping group's reach at alpha 0.2 against 0.5, its wake ring a dashed edge (issue 533); bone over every ground, since no ground but fire is warm (issue 564 retired #578's slate over the old hill); on a tile the dark hides it is laid again over the veil at alpha 0.4, so dark never swallows the priced part (issue 601) |
| `mark.struck` | `#FFFFFF` | a unit just struck: a one-beat ring flash |
| `mark.captain` | `#FFC61A` | gold: the captain's ring between two ink edges and his crown, outlined in ink, rising from the disc's top edge; no other token wears either (issue 601) |
| `ui.ink` | `#15181D` | the screen behind everything; glyphs on amber |
| `ui.panel` | `#1E232A` | cards and chips |
| `ui.text` | `#E9ECEF` | text |
| `ui.muted` | `#8C96A3` | labels and secondary text |
| `ui.lost` | `#5A6270` | the empty part of a bar, an empty pip, rules |

## Shape language

- **Tiles** are flat squares with a hairline ink grid at 18 percent. Detail is ink at 25 to 35 percent over the tile's own colour, so terrain never adds a hue. No outlines around terrain; edges come from value. The board's one depth cue is light from the north: every wall and mountain casts an ink shadow onto the tile south of it (deep at the foot, fading down the tile), a wall's north edge catches a line of light, and the fort's battlement throws its own small shadow. The board sits on a lifted mat (`ui.panel`, an ink hairline at its edge), so the map is an object and the walls never merge with the screen.
- **Tokens** are discs, about two thirds of a tile. The player's: an amber disc on a deeper amber base offset downward, its silhouette in ink. The enemy's: a slate disc with a one-pixel bone rim (a hairline; round 130 found a thicker rim lightened the token) and a bone silhouette, an ink shadow. A boss adds a dashed bone ring outside the rim. The captain wears a gold ring on his disc between two ink edges and a gold crown outlined in ink rising from its top edge, both inside his own tile, so the unit whose death loses the map is the first one found (issue 601; the amber crown above the disc read as a dark tick, and a neighbour's HP bar or name covered it). His selection ring sits outside the gold one. The unit's name sits under its HP bar, its last word when the whole name is wider than the tile (Brigand, Warden). A unit that has acted sinks toward the ink.
- **Silhouettes, by class** (`docs/look/silhouettes.png`): cadet, a short upright sword; captain, the cadet's sword and the crown; pikeman, a long diagonal shaft with a leaf head; toll warden, the pike with a hooked crossbar (the Toll Spear reaches 2); bowman and archer, a bow with its arrow; reaver, a haft with a single crescent head; bandit leader, a double-bitted head and the boss ring; outrider, a lance couched flatter than the pike with its pennant under the head and a horseshoe at its foot (round 170, apart from the pike at 32 px); adept, a flame. Each silhouette is a weapon, since the weapon is what the forecast is about. Classes not on the Tollgate get theirs in the slice that first draws them.
- **HP** sits under the token as a thin bar in the side's colour on ink. Numbers live in the panels, not on the board.
- **The forecast** (`docs/look/crop-forecast-4x.png`, real numbers: seed 113, turn 3, Teodor on 7,5 against the Toll Brigand on 6,5) is the screen's centrepiece. Both tokens, names, weapon and tile; two HP bars with the damage shaded in as a hatch before you commit (the attacker's shaded by the counter's damage, labelled "if countered"); hit, damage and crit as big numerals, ours amber on the left, theirs bone on the right, one label between each pair; strikes as pips, a second pip filled only when a side doubles, "no counter" as an empty slot.
- **Motion** (slices 3 and 4) serves reading: a move slides along its path, a strike lunges a quarter tile, the number rises off the target (a miss says "miss", a crit is larger and holds a beat), a death fades to the ground colour. Recall scrubs back through every state. Speed, fast-forward and skip always; nothing hides state.

## Type

- **UI:** Inter (OFL), tabular numerals (`font-variant-numeric: tabular-nums`), 700 for names and numerals, 400 for the rest, labels in spaced capitals.
- **Log:** JetBrains Mono (OFL), the console's text as it is.
- Both are vendored in `src/Ironwake.Godot/fonts/` from their own GitHub releases (Inter 4.1, JetBrains Mono 2.304; the sandbox's proxy refuses Google Fonts), each with its OFL beside it and listed in `LICENSES`. The client reads them from the source tree, or as imported resources in an export, and falls back to a system face. The console's own lines (the forecast and unit text until slice 2 draws them, the log) stay in the monospace; everything else is Inter. The mocked frames in `docs/look/` still rasterise with DejaVu.

## Layout at 1280x720

- **Top bar:** the map's name, then chips: turn, phase (amber when it is ours), goal in player words, Recall charges.
- **Board, left:** 48-pixel tiles (the Tollgate's 14x12 is 672x576), on an ink mat.
- **Right column:** the forecast card on top and largest; hovering a tile with no strike shows the move preview there instead, one line (#511): terrain and tile, move spent of Mov, avoid, and the verdict as a dot (frost safe, bone struck) with two words, the strikers' total if all land, and the sleeping groups named without numbers; the unit card under it (name, class, HP bar, the stats in one row, the weapon); the log as a one-line bar showing the newest line, `L` to open it full height over the column. The log stays parity-checked.
- **Legend** under the log: only the marks on screen.
- **Footer:** the keys, as keycaps.

## Files

- `docs/look/draw.py` draws the three SVGs; `docs/look/shot.js` rasterises them with the preinstalled Chromium (`NODE_PATH=$(npm root -g) node docs/look/shot.js $PWD/docs/look`). The SVGs are committed so the frame diffs; the PNGs are what the eyes read.
- `the_tollgate-turn1.svg` and `.png`: the frame. `forecast.svg`: the forecast card. `silhouettes.svg` and `.png`: every token on the Tollgate. `crop-player-4x.png`, `crop-enemy-4x.png`, `crop-forecast-4x.png`: the 4x crops.
