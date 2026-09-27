# Ironwake.Godot

The thin renderer, slice 1 (issue 347, DECISIONS/0070). This is a Godot 4.3 .NET project, and it is outside `Ironwake.sln`, so `dotnet build`, `dotnet test` and `--smoke` at the repo root never need Godot. The renderer has no rules. `Main.cs` draws and routes input. Everything it shows comes from `Ironwake.Client.ClientSession`, which asks the core, and the event log is the console's own text.

## Build and play

```
dotnet build src/Ironwake.Godot/Ironwake.Godot.csproj
godot --path src/Ironwake.Godot -- --map the_tollgate --seed 163
```

The C# side builds with the .NET SDK alone. `Godot.NET.Sdk` comes from NuGet. Running it needs the Godot 4.3 .NET editor or a Godot 4.3 .NET export template.

- **Arguments** go after `--`: `--map <name|path>` (the default is `the_tollgate`), `--seed N`, and `--content <dir>` (the default is the repo's `content/`).
- **Mouse.** Left-click one of your units to show its reach, then hover a tile to see the forecast from that tile against each target in range. Click a tile to move there, an enemy to attack it, or the unit itself to wait. A right-click clears the selection.
- **Keys.** `E` ends the phase. The enemy phase then waits. `Space` shows its next event and `C` plays it to the end. `Escape` clears the selection.

## The parity gate

```
godot --headless --path src/Ironwake.Godot -- --map brackwater_cut --seed 53 --parity ../../tests/parity/brackwater_cut-53.script client.log
dotnet run --project src/Ironwake.Cli -- play brackwater_cut --seed 53 --script tests/parity/brackwater_cut-53.script --log console.log
cmp console.log client.log
```

`--parity` plays the script through the client, writes the client's event log and quits. `play --log` writes the console's event log: every event line it prints, and nothing else. The two must match byte for byte. The same check runs in `dotnet test` without Godot (`ClientParityTests`), together with its falsifier. CI's `godot-parity` job runs the headless Godot side on every script in `tests/parity/` (#352), with Godot 4.3-stable .NET checked against the SHA-512 pinned in `.github/workflows/ci.yml`.

## The screenshot

```
xvfb-run -a godot --rendering-driver opengl3 --path src/Ironwake.Godot -- --map brackwater_cut --seed 53 \
  --script $PWD/docs/screenshots/brackwater_cut-53-turn3.script --select 11,3 --hover 11,3 --screenshot $PWD/shot.png
```

`--script` opens the battle with a script's commands already played, `--select` and `--hover` place the pointer as a click and a hover would, and `--screenshot` saves one rendered frame and quits. It needs a display, so CI runs it under xvfb. Paths are best given absolute, since Godot runs from the project folder.
