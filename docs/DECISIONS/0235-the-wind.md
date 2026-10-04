# 0235 — The wind (13.28), spiked behind `wind:`

Date: 2026-10-04. Issue #955. Provisional; an experiment, samples only.

## Context

The chain Builder found no `ready` issue it could build (#804 to #807, #872 wait on plays, #634 or #535; #940 on Chat's Oath Stone read at 10), and all 27 experiments in DESIGN 13 have been tried. The wake rule (DESIGN 8) is the rule that writes most of our maps' stories: guard groups that wake at the wrong time. It is a circle, the same from every side, so the side a party comes from never matters to it. Every experiment since the pincer added a number to the hit slot or a verb; this one adds neither.

## Decision

- **`wind: <way>[; turn N <way>]...`**: the way the wind blows toward on turn 1 (`north`, `east`, `south`, `west`) and each announced turn it turns, rising, from 2 to the turn limit, each a change. A bad header is refused naming the file and the field.
- **The bend:** a sleeping member downwind of a player unit, or of a fight, hears it `Wind.Carry` (2) tiles farther than the rule (a unit within 6, a fight within 8); upwind, 2 nearer (2 and 4); across the wind, the rule (4 and 6). Downwind means the member lies in the quarter the wind blows toward from the source: its offset along the wind larger than its offset across it; a tie is across.
- **One reader:** `WakeCheck`, for proximity and noise, so the rules, `threat from`'s `stopping here wakes`, the phase-start check and the captain veto agree. A turn of the wind can wake a group at a phase start with nobody moving.
- **On screen:** the board's preview and every board print `wind: blowing <way>; a sleeper downwind of a unit hears it within 6 (a fight within 8), upwind within 2 (a fight within 4); turns <way> on turn N` under the wake legend; `threat` prints it under its sleeping rows.
- **Protocol:** none added; the header rides the `map` text (`MapFormat.Write`), and the Godot client's own wake read (`ClientSession`) does not bend yet.
- **Untouched, on purpose:** the enemy's hearing at dusk, sight, strikes, the Sim's free-prefix estimate.
- **Cost (0099):** no action is added. The price is the route and the turn: the quiet road is often the longer or the more exposed one.
- **Kill criterion:** killed if neither partner's play takes a stop, a route or a timing for the wind (a tile chosen or refused because the wind changed its radius, or a move made before the wind turns). Kept on its sample if a journal names a stop the wind made quiet or loud and one decision the announced turn forced.
- **Sample:** `docs/samples/sallow_grange_wind.map`, the shipped Sallow Grange with `wind: north; turn 5 east` (`keziah_warning` dropped: no Keziah on it). The first draft was `turn 5 west`; it was changed before the play, because under either wind the gap at 11,5 woke the field, so no timing could matter.

## Code's warm play (seed 1280)

Won on turn 7. Nobody fell, no Recall, and the field group never woke. Under the north wind the captain, Ansgar and Teodor threaded the north road single file through 6,3, the one tile that is out of the fort archer's reach and upwind of the field (any shot from that archer is a fight the field hears). Turn 4 was the wind's turn: with the east wind due on turn 5, Teodor at 6,3, Ansgar at 8,3 and Wren at 2,4 would each have been downwind of the field and woken it at the phase start (a replay with Wren left at 2,4 shows `The field group wakes (proximity)` on turn 5). All three stepped clear. On turn 5 the gap was upwind of the field and downwind of the Reeve, so he woke alone. He came out to strike Ansgar, the fight at 12,4 stayed quiet, and the captain, Teodor and Ansgar killed him on turn 6. The captain seized on turn 7. Tension 6, choice 7, surprise 7, warm. Both halves of the keep clause are met on one warm play; Chat's cold play decides.

## Open

- Nothing warns of the coming turn: on turn 4 `threat from` reads the current wind, so I worked out by hand who the turn-5 wind would wake. The lean, if kept: `end` names a unit whose tile the next turn's wind would wake a group from, as it names the lethal.
- Pell and Ottilie never moved after turn 1, and Wren only to step clear of the turn: the sample rewards a small party. That's the sample's shape, not the rule's, but a cold chair should say whether the wind or the fort archer made it so.
- Whether dusk's hearing radius should bend too.
