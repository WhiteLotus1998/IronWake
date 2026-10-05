# 0253 — `end` asks while a unit is lethal if all land; `end !` ends anyway

Date: 2026-10-05. Chat's round on #805 (#1063, https://github.com/WhiteLotus1998/IronWake/issues/1063#issuecomment-5994818406), agreed by Code in the reply; built in #1093. Restates the Table's agreement.

## Context

#558 (rounds 158, 159) made `end` print one `Lethal if all land:` line per unit the coming enemy phase kills if every priced strike lands, "then the phase ends anyway". The lines printed as the phase ended, so nobody could act on them. Three warned deaths are in the journals: Chat's Ottilie on the Counting House (980), Chat's Maud on `kestrow_water_carry` (8051, waited, T6), and Chat's free-play Maud on T3, who lived only because both riders missed.

## Decision

1. **The console.** `end` with one or more lethal lines prints them and ends nothing: `ERROR: Lethal if all land: Wren, Maud; add ! to end anyway: end !`, the attack guard's shape (#975, 0241). `end !` prints the same lines and ends the phase, so bait stays legal. Under `--strict` an unconfirmed `end` stops the script like any refused line.
2. **Only the lethal asks.** The escape count (`Count: after this phase`, #928) and the wind line (#957) still print at `end` and never refuse.
3. **The protocol.** `{"type":"end"}` while a unit is lethal answers `{"ok":false,"error":{"reason":"lethalUnconfirmed",...},"lethal":[...]}`, nothing applied; `{"type":"end","anyway":true}` ends the phase. The Core's `EndPhase` is unchanged.
4. **Scripts.** The Sim's `--trace` and campaign scripts write `end !` where the board is lethal; the client's script reader drops the `!`. Every journaled script that ended into a lethal line now reads `end !` there, edited deliberately: from the transcripts' own lethal lines first, then from replays of the scripts journaled before #558 printed the line. The transcripts' echo lines changed only where the replay prints the lethal line after them, and each such transcript is checked byte for byte by its replay test; stale transcripts journaled before #558 keep their history.

## Cost

A chair who means to bait types two more characters. A renderer that sends `end` must handle one more refusal; Godot's own end-turn confirm (`confirm-end-turn`, #677) already lists the lethal lines.
