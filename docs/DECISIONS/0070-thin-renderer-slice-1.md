# 0070 — The thin renderer, slice 1

Date: 2026-09-27. Issue 347, built in a chain run. This restates the twenty-second round (DECISIONS/0046) and the sixty-first round, and records the implementation choices, all of them the Builder's to make.

## Decision

- **Phases.** The thin renderer is Phase 3. The polish pass stays Phase 4. DESIGN.md section 12 already says so since 0046, and this record confirms it for slice 1.
- **The split.**
  - `src/Ironwake.Client/` is a plain net8.0 library in `Ironwake.sln`, and it is the whole presenter. It handles selection, reach (`Queries.Reachable`), the hover forecast against each target from any tile (`Queries.Forecast` with a tile, the console's `ForecastText`), and clicks as commands. It keeps the event log, and it holds an enemy phase that plays one event line at a time (`Step`, `Continue`).
  - `src/Ironwake.Godot/` is the Godot 4.3 .NET project, outside the solution. It draws flat tiles, units as letters in side colours with their hp, and the log, and it routes input to the presenter.
  - The client references `Ironwake.Cli` for the console's text (`PlaySession.Describe`, `ForecastText`). A second formatter would be the way a renderer falls behind the rules.
- **The gate's two logs.** The `--script` transcript also redraws the board after every command, so no event log can equal it. `ironwake play --log <file>` therefore writes the console's event log: each event line it prints, in order, with the dark line at dusk, and nothing else, ending lines in `\n` on every platform. `--log` is refused with `--protocol`. The gate is `Parity.FirstDifference(console, client)`. It is null when the two are the same bytes. Otherwise it names the first line and column that differ.
- **The scripts.** `tests/parity/brackwater_cut-53.script` is the shipped map at dusk. It is an Escape map, and its log carries the dark line. `tests/parity/sallow_grange-61.script` is a Seize with three Recalls. Both replay under `--strict`. `ClientParityTests` runs both in `dotnet test`. The falsifiers are a log one character away and a log missing its last line, and each makes the check fire.
- **Dusk.** The client follows the console, not the protocol. An enemy's Move or Wait that nobody sees prints the dark line and then any other events the command set off. The protocol answers such a command with one `unseenActs` event in place of all of its events.

## Not done here

This sandbox had no Godot binary, and it could not fetch the release to pin its checksum. So two things were left, and they are filed as #352:
- a headless Godot run in CI (the `--parity` mode exists and is documented in the project's README)
- the screenshot of the board mid-battle

The Godot C# project builds under `TreatWarningsAsErrors` with the .NET SDK alone.
