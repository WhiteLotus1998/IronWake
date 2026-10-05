# 0260: The wind waits for Lotus's beta play; the end-turn lethal confirm becomes an option, off on Tactician

Date: 2026-10-05. Lotus's ruling, relayed on the Design Table (#1112, https://github.com/WhiteLotus1998/IronWake/issues/1112#issuecomment-5999497367), on his two concerns (comment 5999430625) and Chat's answer (comment 5999484623).

## Context

Lotus questioned two changes from 2026-10-05. On the wind (13.28, 0256), he said a player cannot calculate or strategize around it. Chat checked the Godot client: it draws no arrow, no bent hearing rings and no "the wind turns" warning, so the counterplay exists only in the console. On the end-turn confirm (0253), he said it takes away the overlooked deaths that give permadeath its weight. Chat recommended cutting it (option a). Lotus chose option (b), adjusted.

## Decision

1. **The wind stays on its sample and never enters the campaign.** No campaign map takes `wind:`. No more work goes into it until Lotus plays it at the beta. If it isn't fun for him, it is killed with a record then. If it is, it returns to the Table. Chat's conditions (a fixed fight-noise radius, plus the arrow, the turn warning and the wind in the client's threat hatch before any campaign use) are recorded as the shape to argue if it survives. They are not built.
2. **The end-turn lethal confirm is a player option, decided by difficulty.** On Recruit and Captain it is a setting the player turns on or off in Options. On Tactician there is no confirm at all. On every difficulty, `end` still prints the `Lethal if all land:` lines as information (#558). `end !` still parses on every difficulty and setting, so no journaled script churns. Built in #1120.
3. **Open, not ruled:** whether the setting defaults on or off. The lean, until Lotus says otherwise, is on, which is today's behavior. Also open is whether the attack confirm (`attack ... !`, #975, 0241) follows the same option. The ruling did not name it, so it stands as is. #1114's slot guard is not affected: it catches a weapon the player didn't name, not a misjudgment.

Supersedes 0253's "It asks" for Tactician, and makes it optional on Recruit and Captain.

## Built (#1120)

- A difficulty carries `lethalConfirm` in `rules.json` (default true, false on Tactician); `Difficulty.AsksOnLethal(setting)` is the one rule, so nothing asks for Tactician by id.
- The setting is the profile line `confirm-lethal: on|off` (on by default, the lean), an Options row beside `confirm-end-turn`, which is unchanged. The Options screen says "never asks on Tactician" on a Tactician campaign.
- The console: `play` and `campaign` take `--confirm-lethal on|off`; `campaign` otherwise reads the profile. `end !` parses everywhere. The protocol answers `lethalUnconfirmed` only when the confirm asks.
- The client's end-turn confirm now also asks on a lethal alone when the confirm asks, so the row does what it says on the screen.

