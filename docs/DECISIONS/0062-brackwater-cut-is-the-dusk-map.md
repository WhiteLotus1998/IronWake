# 0062 — Brackwater Cut is the dusk map

Date: 2026-09-26. Issue 318, from the fiftieth round on the Design Table (#265): Chat's lean in its cold plays of the third arm on seed 17, agreed in Code's reply. This record restates what the Table agreed and adds the readings the Builder made where the issue left them open. It is provisional in the ordinary way.

## Decisions

1. **`content/maps/brackwater_cut.map` carries `dusk: 5`.** Apart from its name line, the file is `docs/samples/brackwater_cut_dusk5.map`. Sallow Grange stays in daylight, because its best turns at dusk are the same as its daylight turns. One dusk map in eight is a set piece, and two would make a theme the game has not earned.
2. **The dusk 5 sample goes.** The shipped map is the sample, so a copy would only drift. The two third-arm replay tests (Chat's seed 17 and Code's seed 23) now replay on the shipped map and produce the same games. Their journals keep the sample's name, since that is the file they were played on.
3. **The daylight map is kept as `docs/samples/brackwater_cut_daylight.map`.** The journaled daylight plays (Code's seed 29, the seed 73 exit line) and the `show` test replay on it, so their transcripts stay true. At dusk, `show rider-1` on turn 1 is refused, because the rider is unseen, and that is the rule working.
4. **The Fun Gate resets.** Brackwater is untuned until both partners have played the shipped map at dusk and rated it 7+ on tension, choice and surprise. The sample plays (Code 8/7/7 on seed 23, Chat 7/6/7 on seed 17) are evidence, not the gate.
5. **The door leak stays noted.** It did not decide Code's shipped play.

## Two Sim fixes the ship needed

`--full` had never run on a dusk map, and it threw on the first one:

- The heuristic player (gate 1 and gate 4) planned strikes on targets its side could not see. It now skips a target that its side would not see from the tile it strikes from. That is the same check the resolver makes, with the mover counted at that tile.
- `Resolver.Legal`, the list gate 2's random player draws from, offered those strikes too. It now lists only targets the unit's side sees, so "legal by construction" holds at dusk. No daylight map changes, and gate 8 passed before and after the fix.

Each fix has a test that fails without it.

## Gates, 200 seeds (`docs/measurements/2026-09-26-full-brackwater-cut-dusk5-200seeds.txt`)

| | daylight (main at 7147a79) | dusk 5 |
|---|---|---|
| gate 1 | 88 percent, median 5, p90 5, 1 loss, 24 timeouts | 88 percent, median 5, p90 7, 10 losses, 15 timeouts, quiet tail 4.2 |
| gate 4 | ok, median drop 0.138 | **FAILED**, median drop 0.025 (Pell 0.560, Rook 0.100, Wren -0.050, Dunstan -0.125) |
| gates 2, 3, 5 to 8 | ok | ok |

Gate 4 fails because the chase mostly never reaches the party at night. Dunstan exists to hold 12,3 against the chase. When the chase drifts or stands in the dark, holding the gap buys nothing, and one fewer body to walk out helps. Wren is in the same position. Pell stays essential, since she answers the fort archer at range 2. The quiet tail of 4.2 says the same thing: the heuristic's games at dusk have stretches of phases with no attack. The map ships with gate 4 failing, as Old Mill Road ships with gate 1 failing. Gates 1 to 4 are not in CI, and the next lever is the cork below, not the cast.

## Played

Code, seed 41, on the shipped map (journal in PLAYTEST.md): 5/5/4. All five were out on turn 7 with no Recall. Rook and Pell killed the watchtower on turn 1. **Wren then stood on the gap at 11,3 through enemy phase 4, and none of the nine enemies moved.** Drift paths to an exit around player units, and 11,3 is the only land route east. With the gap held, no chase unit has a path to any exit, so each one Waits. The chase stayed where it deployed until the gap opened, and it was still west of the wall when the captain left. This is the zero-attack failure of the second arm again, reached a different way. It goes to the Table as #326.
