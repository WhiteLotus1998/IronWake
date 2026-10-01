# 0140 — Title screen and Options, slice 1: the profile's options, Continue, and lowering the difficulty at a camp

Date: 2026-10-01. Issue #677 (Chat's spec from round 205). Slice 1 is everything the issue asks that a test can hold without a renderer; the Godot client's title and Options screens are slice 2.

## Decided

- **Options live in the profile.** `profile.txt` keeps its won-difficulty lines (one id a line, issue 664) and gains one `key: value` line per option once any is set: `speed`, `scenes`, `confirm-end-turn`, `reach-on-hover`, `ui-scale`, `sound`, `volume`. A line with a colon is an option and one without is a win, so a profile written before this reads unchanged and a win alone still writes no option lines. A bad option line keeps the default and is reported naming the line and the key; a profile is the player's file, not content, so it never refuses to load. `Options` and `SaveStore.ReadOptions`/`WriteOptions` are in `Ironwake.Content`.
- **Speed keeps Lotus's names (lean).** The issue says normal, fast, instant; the top bar already has 1x, 2x, 5x from Lotus's note (#625). The option takes those three plus `instant`, so the Options line and the buttons name the same thing.
- **Continue is the newest save by write time**, autosave or named (`SaveStore.Newest`), a tie going to the earlier name in the list, so `auto-1` wins a same-instant tie. `Screens.CampaignTitle(hasSave)` gives the five lines with Continue first and the default only when a save exists; `Screens.TitleFooter` is `Ironwake, rules version 1`.
- **Lowering is the lean the issue records, provisional.** A difficulty carries a `tier` in `rules.json` (Recruit -1, Captain 0 by default, Tactician 1), because the ladder's order was nowhere in the data. `difficulty <name>` on the camp screen moves the campaign to a lower tier; a higher or the same tier is refused, and in a battle the command is refused with `the difficulty is lowered only at a camp, never mid-battle`. The record keeps every difficulty it left (`loweredFrom`, protocol field written only when not empty) and the rules line prints the first: `difficulty Recruit (lowered from Tactician), permadeath on`.
- **No new console surface beyond `difficulty`.** The console keeps its flags; the options are a renderer's.

## Not done here (slice 2)

- The Godot client's title screen (Continue, New game with difficulty and permadeath, Load over #663's list, Options, Quit) and its Options screen; applying speed, scenes, sound and UI scale from the profile at start and writing them on change; `instant` speed; the end-turn confirm and the reach-on-hover switch.
- The campaign client (`CampaignClient`) learning `difficulty`, so a parity script can lower it.
