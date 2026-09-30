# LOOK — how Ironwake is drawn

The showcase's style sheet (epic #509, slice 0 #510, DECISIONS/0092). Provisional until Lotus reads the mocked frame; the partners' scores decide after a week of silence. Tone from CLAUDE.md: grounded, a little dry, warm underneath. The frame: `docs/look/the_tollgate-turn1.png` (seed 113, turn 1, the captain selected, 4,9 hovered), beside the debug client's `docs/screenshots/the_tollgate-113-turn1.png`.

## The one rule

**The world is cold, the player is warm, the enemy lives inside the cold.** Lamplight amber is the only warm colour on the board, so "whose side" is one temperature, not two hues. The enemy's threat is carried by the blade in its silhouette and the threat overlay, never by a hot red. Amber against slate is the blue-yellow axis, the one deuteranopia and protanopia keep.

Fire is the one warm thing in the world (wildfire, 13.15, not on the showcase's Tollgate). It is never a flat fill: a burning tile is its ground colour under an ember hatch that flickers, so no burning tile reads as one of ours (round 129).

## Palette

`Ironwake.Client.LookPalette` holds these values; `LookPaletteTests` fails if this table, the class, or any colour in `docs/look/*.svg` disagree, and runs the readability pass's CIE76 and colour-vision checks on them (terrain 12 apart, sides 40, a side 20 from any ground it stands on, marks 12), plus the warmth rule: the player at chroma 55 or more, every other token at 32 or less, fire exempt as hatched. The debug client keeps `Palette` until slice 1 swaps it.

| token | value | use |
|---|---|---|
| `terrain.plain` | `#748A66` | peat green, the board's ground |
| `terrain.road` | `#B3AE9C` | salt grey |
| `terrain.forest` | `#3E5A45` | dark peat, three ink pines |
| `terrain.hill` | `#B89E6C` | dry heath, one ink swell |
| `terrain.mountain` | `#77767C` | iron, an ink ridge |
| `terrain.water` | `#41667F` | slate water, two frost ripples |
| `terrain.fort` | `#9FB0C4` | cold stone, an ink battlement |
| `terrain.wall` | `#23272E` | near-black, faint iron coursing |
| `terrain.throne` | `#E4E7EA` | the gate: the palest tile, an ink arch, the eye's target on a seize |
| `terrain.fire` | `#943C0C` | ember, hatched over the tile's ground, never a fill |
| `player` | `#E8A33D` | lamplight amber: the player's tokens, HP, numerals |
| `player.deep` | `#9A6420` | the amber token's base and shadow |
| `enemy` | `#2F3742` | slate: the enemy's token |
| `enemy.bone` | `#E6E0D0` | bone: the enemy's silhouette, rim, HP and numerals |
| `mark.selected` | `#F6D38A` | the selected unit, the hovered tile (dashed), the planned path (dotted) |
| `mark.reach` | `#BFD9EA` | frost: tiles the selected unit can reach, filled at about 40 percent |
| `mark.threat` | `#EDE6D6` | the enemy's reach, drawn as a diagonal hatch, never a fill |
| `mark.struck` | `#FFFFFF` | a unit just struck: a one-beat ring flash |
| `ui.ink` | `#15181D` | the screen behind everything; glyphs on amber |
| `ui.panel` | `#1E232A` | cards and chips |
| `ui.text` | `#E9ECEF` | text |
| `ui.muted` | `#8C96A3` | labels and secondary text |
| `ui.lost` | `#5A6270` | the empty part of a bar, an empty pip, rules |

## Shape language

- **Tiles** are flat squares with a hairline ink grid at 18 percent. Detail is ink at 25 to 35 percent over the tile's own colour, so terrain never adds a hue. No outlines around terrain; edges come from value.
- **Tokens** are discs, about two thirds of a tile. The player's: an amber disc on a deeper amber base offset downward, its silhouette in ink. The enemy's: a slate disc with a bone rim and a bone silhouette, an ink shadow. A boss adds a dashed bone ring outside the rim. The captain wears a small amber crown above the disc (the old crown, kept).
- **Silhouettes, by class** (`docs/look/silhouettes.png`): cadet, a short upright sword; captain, the cadet's sword and the crown; pikeman, a long diagonal shaft with a leaf head; toll warden, the pike with a hooked crossbar (the Toll Spear reaches 2); bowman and archer, a bow with its arrow; reaver, a haft with a single crescent head; bandit leader, a double-bitted head and the boss ring; outrider, the pike with a pennant; adept, a flame. Each silhouette is a weapon, since the weapon is what the forecast is about. Classes not on the Tollgate get theirs in the slice that first draws them.
- **HP** sits under the token as a thin bar in the side's colour on ink. Numbers live in the panels, not on the board.
- **The forecast** (`docs/look/crop-forecast-4x.png`, real numbers: seed 113, turn 3, Teodor on 7,5 against the Toll Brigand on 6,5) is the screen's centrepiece. Both tokens, names, weapon and tile; two HP bars with the damage shaded in as a hatch before you commit (the attacker's shaded by the counter's damage, labelled "if countered"); hit, damage and crit as big numerals, ours amber on the left, theirs bone on the right, one label between each pair; strikes as pips, a second pip filled only when a side doubles, "no counter" as an empty slot.
- **Motion** (slices 3 and 4) serves reading: a move slides along its path, a strike lunges a quarter tile, the number rises off the target (a miss says "miss", a crit is larger and holds a beat), a death fades to the ground colour. Recall scrubs back through every state. Speed, fast-forward and skip always; nothing hides state.

## Type

- **UI:** Inter (OFL), tabular numerals (`font-variant-numeric: tabular-nums`), 700 for names and numerals, 400 for the rest, labels in spaced capitals.
- **Log:** JetBrains Mono (OFL), the console's text as it is.
- Both come from their own GitHub repositories with their licence files (the sandbox's proxy refuses Google Fonts) when slice 1 vendors them. Until then the mocked frames name them first and rasterise with DejaVu Sans and DejaVu Sans Mono.

## Layout at 1280x720

- **Top bar:** the map's name, then chips: turn, phase (amber when it is ours), goal in player words, Recall charges.
- **Board, left:** 48-pixel tiles (the Tollgate's 14x12 is 672x576), on an ink mat.
- **Right column:** the forecast card on top and largest; hovering a tile with no strike shows the move preview there instead (tile, move spent, avoid, and `threat`'s verdict for that stop in words); the unit card under it (name, class, HP bar, the stats in one row, the weapon); the log as a one-line bar showing the newest line, `L` to open it full height over the column. The log stays parity-checked.
- **Legend** under the log: only the marks on screen.
- **Footer:** the keys, as keycaps.

## Files

- `docs/look/draw.py` draws the three SVGs; `docs/look/shot.js` rasterises them with the preinstalled Chromium (`NODE_PATH=$(npm root -g) node docs/look/shot.js $PWD/docs/look`). The SVGs are committed so the frame diffs; the PNGs are what the eyes read.
- `the_tollgate-turn1.svg` and `.png`: the frame. `forecast.svg`: the forecast card. `silhouettes.svg` and `.png`: every token on the Tollgate. `crop-player-4x.png`, `crop-enemy-4x.png`, `crop-forecast-4x.png`: the 4x crops.
