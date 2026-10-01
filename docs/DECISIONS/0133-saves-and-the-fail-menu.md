# 0133 — Saves and the fail menu: camp autosaves, named saves, a suspend, Load / New game / Quit

Date: 2026-10-01. Issue #663. The shape is Lotus's round 200 with Chat's round 201 section 5, agreed by Code in round 202, as the issue body gives it. Where the issue left an implementation choice, the Builder's leans are below; Chat can argue them on the PR.

## Decided

- **A save is the record's protocol file.** `SaveStore` (in `Ironwake.Content`, the only project that touches files) writes `ProtocolJson.Campaign` to `<dir>/<name>.json`. Core is unchanged.
- **Autosaves** are taken at the start of every camp screen, before anything is bought, as `auto-1`; the older ones shift to `auto-2` and `auto-3`, and the fourth is dropped. Named saves are 1 to 32 of `a-z0-9-_`, never starting `auto-`, and replace a save of the same name.
- **The suspend is a replay (lean).** A mid-battle `BattleState` would need Recall's history and the event log to round-trip. Every roll is keyed on the seed, so the suspend is the record the battle began from plus the lines typed in the battle; `--resume` replays them unseen, prints where it stands, and deletes the file before play goes on. `quit` in a campaign battle writes it; so does the input ending while the battle is not left. `quit` is refused once the battle is decided (leave it instead).
- **The fail menu.** A lost map prints its lost line, the bad-ending card (one placeholder paragraph, its heading marked as a placeholder until #656), then `load <name>`, `saves`, `new` (map 1 on the same seed and difficulty) or `quit`.
- **Where (lean).** `--saves <dir>`; at the keyboard it defaults to `saves/`; a scripted run writes nothing unless the flag is given, so tests and CI leave no files. `--load` and `--resume` exclude each other and `--from`.

## Not done here

- Quitting inside a trial or a side map writes no suspend; the camp's autosave is the way back.
- The Godot client's load and fail screens.
- The bad-ending card's text (#656).
