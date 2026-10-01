# 0141 — Title screen and Options, slice 2: the Godot screens

Date: 2026-10-01. Issue #677, slice 2 (slice 1 is 0140). Code's calls, recorded as implementation choices. All are reversible renderer details.

## Decided

- **`--campaign` opens the campaign's title.** The five lines are Continue, New game, Load, Options and Quit, and the words come from `Screens`. Continue appears, lit, only when a save exists, and it shows the save's name. `--from <map>` still opens straight onto the camp. The showcase's own title (Play, How to play, Options, Quit) stays the exported build's first screen.
- **New game** offers only the difficulties the profile has unlocked, lowest tier first (`Screens.NewGameDifficulties`). It opens on Captain, and you can toggle permadeath. The line for permadeath off says what it means: `the fallen come back wounded for 2 maps`.
- **Load** lists `SaveStore.Names()`, the first ten, and a click loads one. A refused load prints its refusal on the screen.
- **The Godot campaign autosaves.** At each camp, `CampaignClient` takes an optional `SaveStore` and autosaves exactly as `ironwake campaign` does, and it records a won difficulty. Without that, Continue in the client would only ever find console saves. The event log is untouched, so parity runs (which pass no store) are byte for byte the same.
- **Saves and profile location.** They live in `saves/` under Godot's user data folder, or `--saves <dir>`. The options are read at start and written on every change: the Options rows, the speed buttons, and S, B and M. A screenshot, a strip or a parity run keeps the defaults and never writes the profile, so renders do not depend on whoever ran them.
- **Instant is a fourth speed button**, drawn as three triangles ending on a bar. Its factor is 1/1000, so every beat still plays for a frame or so and every line reaches the log. "Animation never hides state" holds. A factor of 0 would divide by zero in the beat clock. S cycles all four speeds.
- **The end-turn confirm.** It counts the units that have neither moved nor acted, names them, adds #558's lethal lines (the console's `LethalLine`), and asks `E again ends the phase, Esc goes back.` It is silent when the option is off, the battle is decided, it is not the player's phase, or everyone has moved or acted (`EndTurnConfirm`).
- **Reach on hover.** With nothing selected and the player's phase waiting, hovering a seen enemy draws its reach, the one a click on it would pin (`ClientSession.ReachShown`). With the option off, only a click shows it, as before.
- **Options is on the pause menu, key O,** between Sound and Return to title. Esc on Options comes back to the menu.

## Not done here (#677 closes; the rest is filed)

- **UI scale is stored and printed, but it is not an Options row and the client does not apply it.** The window already stretches the whole 1280x720 canvas to the screen (`canvas_items`). A larger type needs a layout that reflows, and every screen is laid out in fixed pixels. Offering a row that does nothing would be worse than no row. The row lands with a reflow pass, filed as #698 (beside #349).
- **The campaign client does not learn `difficulty` yet,** so lowering from a camp is the console's only, and a parity script cannot lower it. The camp screen's view is #678.
