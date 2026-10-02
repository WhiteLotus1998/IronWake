# 0177 — The Vanguard's verb is Opening: built, read at 200 seeds, stepped once (#772)

Date: 2026-10-02. Issue #772, from Chat's round 236 on the Design Table (#731). The verb, its board facts and its kill criterion were agreed before the read. This record restates them, says how they were built, and applies the criterion. The numbers are provisional.

## Decided

- **Opening is a class ability of the Vanguard** (`opening` in `abilities.json`, a new effect kind `opening` with `def` and `res`, listed in the Vanguard's `abilities`). When the Vanguard attacks and any of its strikes hits an enemy that lives through the combat, including a hit for 0, the enemy is open until the phase ends (`BattleUnit.Open`, an `OpenMark` naming the opener and the numbers). Every strike on it by a unit of another side than its own, other than the opener, reads its Def and Res lower, each never below 0 (`Opening.Lowered`). The resolver opens only on the attack command, never on a counter. A miss opens nothing. A second opening refreshes the mark and does not stack. Every phase end clears every mark.
- **One number.** The mark is read where the struck side answers (`BattleUnit.ToCombatant` with `countering`), so the forecast, both planners, `threat` and the resolver all read the same lowered stats. The open mark is board state, so Recall restores it.
- **On screen.** The board row reads `open: Def -N Res -N to allies of <id>`. The card reads `open: allies of <name> strike at Def -N, Res -N until player phase ends`. An ally's forecast prints `open: <target> opened by <name>: Def -N, Res -N in this forecast`, and the opener's own forecast prints that the mark is not his. The event `unitOpened` (`unit`, `by`, `def`, `res`) reads `<name> is open: ...`. The protocol carries `open` on the unit (`by`, `def`, `res`). The loader refuses an opening that lowers nothing or names a negative number.
- **The protocol shape.** #772 asked for `opened` on the strike event. It is a separate `unitOpened` event instead, the shape `unitChilled` and `unitGrounded` already use for an after-combat mark, so a renderer reads all three the same way.
- **The Champion does not inherit Opening** (a form's abilities are its own). At tier 2 the Champion reads 97.5, 98.5, 68, 84.5 and 95.5 on the five campaign maps, mean 88.8, against the Commander's 87.9 and the Pathfinder's 94.3. No form reads under its base, so the exception in #772 does not apply.
- **The Sim's sequencing.** The player plans in roster order and the captain is first in the roster, so on most phases the captain strikes before any ally can reach the same target. The chooser does not price the mark. It scores kills and damage as before, but it reads the lowered numbers once the mark is set, so an ally planning after the captain goes for the open enemy wherever the lowered numbers make that the best line. So the read measures the verb as a side effect of the captain going first, not as a choice to open. `--ladder` prints `opened N converted M` in the captain's mix: the enemies his attacks opened, and the kills an ally then made on an enemy he had opened.

## Measured

At Def and Res -3, 200 seeds, tier 1 (party and enemies +2), the five campaign maps (Tollgate, Mill, Saltmarsh, Harrow, Brackwater):

| Class | Gate 1 per map | Mean | Captain share per map | Share mean |
|---|---|---|---|---|
| Marshal | 76 / 63.5 / 62 / 96 / 81 | 75.7 | 38 / 58 / 67 / 31 / 51 | 49.0 |
| Ranger | 74 / 73 / 67.5 / 94 / 68 | 75.3 | 38 / 57 / 68 / 31 / 48 | 48.4 |
| Vanguard | 75.5 / 80 / 30 / 71.5 / 65 | 64.4 | 33 / 54 / 72 / 19 / 53 | 46.2 |

- **Opened 1103, converted 748 (68 percent),** far above #772's 10 percent floor. Per map: Tollgate 212/181, Mill 470/283, Saltmarsh 107/38, Harrow 284/246, Brackwater 30/0.
- **The share is the lowest of the three** (46.2 against 48.4 and 49.0), and lowest on three of five maps. The hoarder kill does not fire.
- **The mean is 11.3 under the Marshal**, just outside the bar. Without the verb, 0171 read 63 (100 seeds), so the verb bought about one point. Saltmarsh carries the gap: 30 against 62 and 67.5, with gate 4 failing under the Vanguard as it fails under the unpromoted captain.
- That is #772's third case, "under the bar with a low share": step -3 to -4 once, read again, then stop.

STEP_RESULT

## Open

- **The hand plays decide.** #772's keep clause asks each chair's Vanguard-captain Saltmarsh play to name a turn where the captain struck to open rather than to kill. Code's warm play (seed 772, PLAYTEST) names turn 8, where the open sum (27) reached the boss and the plain sum (21) did not. Chat's play is owed.
- Saltmarsh is where the Vanguard trails, and it trails there under every reading so far: the durability lever (0174), the cadet, and now Opening. A Vanguard who opens the boss on the braced fort eats the counter at -15 hit. Whether that is the map or the class is a question for the Table, not a third step.
- The mark ends with the phase, so a Vanguard's counter kill on the enemy phase still feeds the captain. That is as specified.
