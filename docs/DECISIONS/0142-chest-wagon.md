# 0142 — Chests: the overflow goes to the wagon, and a chest under an enemy is shut

Date: 2026-10-01. Issue #679. This amends DECISIONS/0132. The Table agreed the rule in round 206 (Chat) and round 207 (Code). Where the issue left an implementation choice, the Builder's lean is below and is provisional.

## Decided (the Table)

- **A chest always opens.** What fits goes to the opener's pack at full uses, in the chest's listed order. The rest goes to the wagon (0051 decision 6), which the campaign collects only if the map is won. This replaces 0132's all-or-nothing rule, under which a chest stayed shut when the pack was full.
- **A chest with an enemy on its tile is shut**, whether the opener stands on it or beside it. A guard beside the chest does not shut it. A `B`-line boss whose home is the chest tile shuts the vault for as long as he lives, so no new rule is needed for that.
- **The rule line says both.** The opening prints the pack line, then the wagon line when anything went there, and it does so on a standalone map too.

## Decided (the Builder's lean, provisional)

- **The wagon in battle** is `BattleState.Wagon`, a list of item ids at full uses. A Recall restores it along with the board. The canonical state prints a `wagon` line only when the wagon holds something. No earlier state could have overflowed, so no recorded transcript changes.
- **On the record**, the wagon is `CampaignRecord.Wagon`.
  - `AfterBattle` adds the battle's wagon, and so does `AfterQuest` on a win.
  - A lost main map ends the campaign, and a lost side map adds nothing.
  - The Keep panel prints `Wagon: 1 Iron Bow, ...`, and `take <unit> <n>` moves an entry into a free slot at full uses.
  - This is the smallest shape that makes "collected" mean something at the camp. It is a camp convoy and nothing more: the wagon is never reachable in battle.
- **The Sim** needs no code. The heuristic ignores chests, the random player draws `Open` from `Legal`, and no shipped map has a chest.

## Not done here

- **Materials in chests** (0139). They would change the chest format, so they get their own issue if the Table wants them.
- **The Godot client's `take` command.** Its Keep panel already shows the wagon line through `CampaignSession.KeepPanelLines`.
