# Ironwake.Godot

The thin renderer, slices 1 and 2 and the readability pass (issues 347, 353 and 349, DECISIONS/0070). This is a Godot 4.3 .NET project, and it is outside `Ironwake.sln`, so `dotnet build`, `dotnet test` and `--smoke` at the repo root never need Godot. The renderer has no rules. `Main.cs` draws and routes input. Everything it shows comes from `Ironwake.Client.ClientSession`, which asks the core, and the event log is the console's own text.

## Build and play

```
dotnet build src/Ironwake.Godot/Ironwake.Godot.csproj
godot --path src/Ironwake.Godot -- --map the_tollgate --seed 163
```

The C# side builds with the .NET SDK alone. `Godot.NET.Sdk` comes from NuGet. Running it needs the Godot 4.3 .NET editor or a Godot 4.3 .NET export template.

- **Arguments** go after `--`: `--map <name|path>` (the default is `the_tollgate`), `--seed N`, and `--content <dir>` (the default is the repo's `content/`).
- **Mouse.** Left-click one of your units to show its reach, then hover a tile to see the forecast from that tile against each target in range, and under it the threat on your unit if it ends there: the console's `threat` text, sleeping groups, arrivals and the dark included. Click a tile to move there, an enemy to attack it, or the unit itself to wait. A right-click clears the selection.
- **Keys.** `E` ends the phase. The enemy phase then plays itself (issue 513), one event at a time at the speed `S` cycles (slow, normal, fast); `Space` shows its next event at once and `C` skips to its end. Moves walk their path, strikes lunge with each strike's number rising off its target (`miss` muted, a crit larger under `CRIT`), and a death shrinks the token into a greyed mark inside its side's ring, crossed, that stays on its tile for the rest of the phase. The rhythm is `Rhythm`'s (issue 544): walks and misses quick, a hit a beat, a crit longer, and a death holds still for 0.6 s before the next act. The forecast's slot holds the enemy-act card: who acted, what it did, and for a strike both sides' HP before and after with each strike's result; a killing act leads with the death (`Teodor falls`, `struck down by Rider`), and a move or wait leaves the last strike's card up. While the phase plays the column's log holds only the act on show (`THIS ACT`); `Tab` opens the whole log. Each event is marked on the board as its line appears, and the line is drawn in the same bone: the path a unit walked, its start tile dotted, the tile it ended on or acted from ringed, and the tile it struck bracketed in white. An act the dark hides marks nothing. `R` opens the Recall browser: the console's `recall list`, the newest 40 states, and a click on a state's row rewinds to it, with what it gave back in the status line. The rewind scrubs (issue 514): over one second the board folds back through every state it undoes, each its share, units sliding home, the dead standing back up, arrivals shrinking away and the turn chip counting down, dimmed under an amber frame with the chip reading REWIND. The RECALL chip shows a pip per charge the map opened with, filled while left. The number of a strike that kills is drawn at glyph size with a heavy outline and held through the death beat; a death of yours in the enemy phase, with a charge left, pulses the chip from the hold. The enemy-act card clears once the phase has flipped to yours. `--recall-after <index>` recalls once the enemy phase has played, for the strips. `T` hatches every tile a seen enemy could strike next phase (`--threat` opens with it on), the terrain's ink drawn over the hatch. `Tab` opens or closes the whole-column event log. `Escape` clears the selection and closes the browser.

## Sound (issue 516)

Six clips under `assets/sound/`: `hit`, `miss` and `crit` sound as a strike's number rises, `fall` as a death's beat starts, `click` on a press, and `ambient`, a wind bed that loops under everything. Which cue plays when is `Ironwake.Client.Sound`'s, read from the beats, so a beat skipped with Space or C is silent. `M` mutes and unmutes all of it (on the title, in the battle's footer and on the end card; on the campaign's screen M still marches). The clips are the project's own, synthesised by `docs/sound/make_sounds.py` (same bytes every run) and dedicated CC0 in `LICENSES`. From the source tree they are read as PCM by `WavPcm`, with no import step; an exported build plays the imported resources. A file dropped in under the same name replaces a clip with no code change, and gets its own `LICENSES` entry. Screenshots, strips and headless runs are silent.

## The screen (issue 349)

The window is 1280x720 and scales up. The board is on the left, with each terrain present on the map in the legend under it, then what the shapes and marks mean. The board, its legend and the side column are one block centred between the top bar and the footer, and the column is as tall as the board and legend. The column holds, top to bottom: the objective, the status line, the enemy phase's keys while it plays, the **forecast** (a hint until a unit is selected and a tile pointed at; then, where a strike is priced, the drawn forecast card with the threat on a stop there under it in the move preview's words, else the move preview), the drawn **unit** card (the unit pointed at, or the selected one), and the **event log**'s newest lines in whatever room is left. `Tab` gives the whole column to the log and back (`--log-open` opens with it). A token carries its name only while pointed at or selected; the unit card carries it otherwise. Long lines wrap inside the panel (`TextLayout.Wrap`). The console's own lines are in JetBrains Mono, so numbers align as they do in the console; the top bar, titles, legend, keys and the move preview are in Inter. Both are OFL, vendored under `fonts/` with their licences (see `LICENSES` at the repo root).

Since showcase slice 2 (#512, `Main.Forecast.cs`) the forecast is drawn as `docs/look/forecast.svg` draws it: both sides at one weight, each with its weapon and the source of its avoid beside its tile (`Forest 7,5 +20`); each HP bar in three states (what is left if every strike lands, this strike's cost hatched, HP already lost in `ui.lost`); hit, damage and crit in big numerals, ours in amber and theirs in bone, an empty slot when there is no counter; strikes as pips. Its numbers are `ClientSession`'s `ForecastCard`, read from the `CombatForecast` the console's forecast line prints, and `ClientForecastCardTests` holds each drawn number to that line.

Since showcase slice 1 (#511) the board is drawn in `docs/LOOK.md`'s look (`Main.Look.cs`): your units are amber discs with an ink silhouette, enemies slate discs with a bone silhouette and rim, so the side reads by value and by the silhouette's inversion when hue fails; each silhouette is the class's weapon. At dusk an unseen tile is shaded and an unseen enemy is a `?`. The colours live in `Ironwake.Client.LookPalette`, checked by `LookPaletteTests`; the debug colours in `Ironwake.Client.Palette` are no longer drawn. `PaletteTests` holds every terrain pair, the two sides, and each side on each terrain apart by a CIE76 distance, in full colour and under simulated deuteranopia and protanopia (Machado 2009). That is a check by arithmetic, not by eye, and it has a falsifier.

## A build to double-click (Windows)

`export_presets.cfg` holds a `Windows Desktop` preset. On a machine with the Godot 4.3 .NET editor:

1. Install the export templates once: Editor > Manage Export Templates > Download and Install (4.3.stable.mono).
2. Export from the repo root: `mkdir -p build/windows`, then `godot --headless --path src/Ironwake.Godot --export-release "Windows Desktop"`. The export refuses a folder that does not exist. It publishes the C# through `Ironwake.Godot.sln`, which Godot requires beside the project, and it exits 0 even when that publish fails, so check that `build/windows/data_Ironwake.Godot_windows_x86_64/Ironwake.Godot.dll` exists. The preset writes `build/windows/Ironwake.exe` beside its .NET data folder, and `build/` is ignored by git.
3. Copy the repo's `content/` folder into `build/windows/`, next to `Ironwake.exe`. An exported build has no source tree, so it reads `content/` from beside its executable.

Double-click `Ironwake.exe`. It opens the Tollgate on seed 1. To pick a map and seed, run `Ironwake.exe -- --map sallow_grange --seed 61` from a shell. CI builds the same folder (#361): the `godot-windows-export` job fetches the 4.3-stable .NET export templates against a pinned SHA-512, exports, copies `content/` in, and uploads it as the `ironwake-windows` artifact on every run. The `godot-windows-launch` job then downloads that artifact on a Windows runner and holds the exported `Ironwake.exe`'s `--parity` log on each `tests/parity/*.script` to `play --log`, byte for byte.

## The parity gate

```
godot --headless --path src/Ironwake.Godot -- --map brackwater_cut --seed 53 --parity ../../tests/parity/brackwater_cut-53.script client.log
dotnet run --project src/Ironwake.Cli -- play brackwater_cut --seed 53 --script tests/parity/brackwater_cut-53.script --log console.log
cmp console.log client.log
```

`--parity` plays the script through the client, writes the client's event log and quits. `play --log` writes the console's event log: every event line it prints, and nothing else. The two must match byte for byte. The same check runs in `dotnet test` without Godot (`ClientParityTests`), together with its falsifier. CI's `godot-parity` job runs the headless Godot side on every script in `tests/parity/` (#352), with Godot 4.3-stable .NET checked against the SHA-512 pinned in `.github/workflows/ci.yml`.

The campaign (#360) plays the same way. `--campaign` opens the between-map screen, starting at the first map or at `--from <map>`. A click on a unit's row selects it, and a click on a ware then buys it for that unit. B benches or unbenches the selected unit, M or the march row marches, and L leaves a decided battle. `--campaign-parity` is the campaign's gate. It must match `ironwake campaign --log` byte for byte, and `CampaignClientTests` runs the same check in `dotnet test`. CI does not run the Godot side yet.

```
godot --headless --path src/Ironwake.Godot -- --campaign --from the_tollgate --seed 113 --campaign-parity $PWD/tests/parity/campaign/the_tollgate-113.script client.log
dotnet run --project src/Ironwake.Cli -- campaign --from the_tollgate --seed 113 --script tests/parity/campaign/the_tollgate-113.script --log console.log
cmp console.log client.log
```

## The screenshots

`docs/screenshots/render.sh` renders every review screenshot: each shipped map at turn 1 with the captain selected and a tile pointed at, and mid enemy phase with one event marked, plus the earlier slices' shots. It needs `GODOT` set and runs under xvfb. `--enemy-steps N` ends the player phase after any `--script` and reveals N enemy-phase events.

```
xvfb-run -a godot --rendering-driver opengl3 --path src/Ironwake.Godot -- --map brackwater_cut --seed 53 \
  --script $PWD/docs/screenshots/brackwater_cut-53-turn3.script --select 11,3 --hover 11,3 --screenshot $PWD/shot.png
```

`--script` opens the battle with a script's commands already played, `--select` and `--hover` place the pointer as a click and a hover would, `--recall` opens the Recall browser (`docs/screenshots/sallow_grange-61-recall.png` comes from `sallow_grange-61-recall.script` with it), and `--screenshot` saves one rendered frame and quits. It needs a display, so CI runs it under xvfb. Paths are best given absolute, since Godot runs from the project folder.
