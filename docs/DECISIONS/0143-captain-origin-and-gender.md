# 0143 — The captain's origin and gender

Date: 2026-10-01. Issue #681 (split from #648). The shape is the Table's (rounds 192 to 194, 201, 206, 207). The numbers and the interface are the Builder's lean and are provisional.

## Decided (the Table)

- **Origin sets only the captain's card**: stats and growths. No support or rapport number depends on it (round 201). Its Commander's Word variant stays on #648.
- **Gender is chosen**, and pronouns follow it wherever text names the captain.
- **Gate 1 lands within 5 points across the four origins.**

## Decided (the Builder's lean, provisional)

- **The data.** `campaign.json` `origins`, each an `id`, `name`, and `stats` and `growths` as deltas on the cast's captain. The loader holds each delta set to sum 0, so the four cards are the same size. It also refuses a stat below 0, a growth outside 0 to 100, and an id listed twice. The origin's id becomes the captain's `region`.
- **The numbers move only Str, Dex, Lck and Res.**
  - The first draft, which followed round 193's leans, failed The Mill by 42 points. +1 Spd lets the captain double every Spd 5 enemy, and +2 HP crosses a survival edge, so either one moved The Mill from 67 to 94 or 97.
  - Spd, HP and Def stay off the table, and so does Cha, because Cha sets the rapport rate (`Rivalry.RateOf`).
  - The four origins:

    | Origin | Stats | Growths | Lean |
    |---|---|---|---|
    | Aldmere | Dex +2, Lck -1, Res -1 | Dex +10, Lck -5, Res -5 | Acc |
    | Sallow | Str +1, Lck +1, Res -2 | Str +5, Lck +5, Res -10 | crit |
    | Kestrow | Str -1, Lck +2, Res -1 | Str -5, Lck +10, Res -5 | Evade, via Lck |
    | the outlands | Str +2, Dex -1, Lck -1 | Str +10, Dex -5, Lck -5 | Power |

- **Gate 1 (Release, 200 seeds, `--full <map> --origin <id>`):**

  | Map | Cast card | Aldmere | Sallow | Kestrow | Outlands | Spread |
  |---|---|---|---|---|---|---|
  | The Tollgate | 74 | 76 | 74 | 77 | 76 | 3 |
  | The Mill | 67 | 67 | 66 | 66 | 68 | 2 |

- **Gender** is `Unit.Pronoun`, a per-unit override that falls back to the cast file's `pronoun`.
  - It's set on the roster's captain at the start, so battles, the camp, and the refusal lines read it through `UnitNames` and `Referent.For(content, unit)`.
  - The captain's personality line lost its "his".
  - Saves write `pronoun` on the unit and `origin` on the record, each only when chosen.
- **Choosing.**
  - The console takes `campaign --origin <id> --captain he|she`. A choice prints `Captain: Alder Fenn, from Sallow (she/her)` under the opening line.
  - The Godot client's New game screen cycles the origin with O and the captain with G, defaulting to the first origin and he.
  - A campaign without a choice is the cast file's captain, so every transcript, test and Sim default stays as it was.

## Not done here

- **Starting rapport by region:** withdrawn by round 201 (friction is faith, not region). A test holds every recruit, and the captain's Cha, unchanged across origins.
- **The origin-variant first support line:** waits on #77, since no supports exist yet.
- **A Mov aura, and the Commander's Word variants:** both are #648's.
