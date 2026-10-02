# 0152 — UI scale in the Godot client: one factor, a layout that reflows

Date: 2026-10-02. Issue #698 (split from #677, DECISIONS/0141). Code's calls, recorded as implementation choices. All are reversible renderer details; no rule, number or protocol line moves.

## Decided

- **One factor, applied by the window.** The client sets the window's content scale to the scale's factor (1, 1.25, 1.5), so every type size, card, chip and stroke grows together and clicks arrive already in the canvas's coordinates. The layout then reflows into the smaller canvas the window now holds: 1280x720 at 100, 1024x576 at 125, 853x480 at 150 (`UiLayout`, pure and tested in `Ironwake.Client`).
- **What reflows.** The column narrows from 500 to 420 and 360 and its console lines wrap at the new width. The top bar puts the speed buttons and the scenes chip on a second row, and the key strip wraps onto a second row (`UiLayout.Wrap`). The board's tile shrinks to what is left (the Tollgate's 45 at 100 becomes 28 and 20 canvas pixels, 35 and 30 on screen). The column may run below a short board's legend so the forecast card still fits, never into the key strip.
- **The cards adapt in place.** The forecast's weapon and ground stack under each name when the two sides would collide, its numerals step down with the column, and the counter's pips move left of the doubling words. The unit card's HP and EXP bars share the narrower width and its weapon line wraps to two rows. The move preview's line wraps inside its card. On the player's phase, a closed log the cards have filled the column with is left out rather than drawn over them.
- **Composed screens stay at full size.** The showcase title, how to play, the campaign's title, New game, Load and the battle scene are laid out for the whole window with type 12 and up, and an 853-high canvas would push them off it. They draw under the scale's inverse at the 100 layout (`AtFullSize`), and their click regions are brought back into canvas coordinates. Options and the pause menu reflow. Options closes its rows up to fit.
- **The Options row.** `ui-scale` is a row between reach on hover and sound, cycling 100, 125, 150 and reading `125%`. It applies the moment it changes and the profile keeps it.
- **At 100 nothing moves.** Renders of the Tollgate's turns 1 and 3, the open log, Brackwater at dusk, the Recall browser, the camp, the titles, New game and a battle-scene frame are byte-identical to main's. Only Options differs, because it has gained the row.
- **Renders name their scale.** `--ui-scale N` sets it for a screenshot or strip without touching the profile. `render.sh` adds the Tollgate's turn 1, turn 3's forecast, the pause menu and Options at 125 and 150.

## Not done here

- The camp screen (the between-map text screen) reflows by width but not by height: at 150 its longest lists run past the window's foot, as long lists already did at 100. M still marches. The camp's own view is #678's.
- The legend under a short board can sit close to the key strip at 150. It does not overlap.
