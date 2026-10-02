# 0156 — Grounding: bows trade the effective tag for crit against fliers (#703)

Date: 2026-10-02. Issue #703 (Lotus's progression batch, item 5; Chat's round 216, Code's round 218). Arm B, the rule and its printing are the Table's; the choices below are Code's implementation calls. Provisional.

## Decided

- **Arm B.** The Iron, Steel and Obsidian Bows lose `effective: [flying]` and carry `critAgainst: [flying]`, `critBonus: 20`, two new weapon fields read in `Combat.CritChance` (`Weapon.CritBonusAgainst`). The loader refuses a `critBonus` without `critAgainst`, a `critAgainst` without a bonus of 1 to 100, and a repeated movement type, each naming file, entry and field. Gust keeps its tag.
- **Who grounds.** A strike from a bow that crits a flier still standing after the combat sets `BattleUnit.Grounded` to 1 (`Grounding.AfterCombat`, after the chill's step, the watch shot included). The clock counts as the chill's (`Frost.AtPhaseChange`). A second crit refreshes it.
- **How a grounded flier moves.** `Grounding.MovementOf` answers infantry for a grounded flier. `BattleState.ReachOf` and the Canto and Fall back reaches read it, so `threat`, Move and both planners plan the walk; so do the enemy planner's `Approach`, `March` and objective march, the exposure check and the Sim heuristic's approach. Terrain avoid and Def stay a flier's: the issue names movement costs and terrain rules for moving, and a flier knocked into a wood is not hiding in it. Listed as Unsure on the PR.
- **Stranded.** `Movement.Reach` refuses an origin the movement cannot stand on, so a grounded flier on a tile infantry cannot enter gets its own tile as its whole reach (`Grounding.Stranded`). `EnemyAi.AttackTiles` now always admits the unit's own tile, so a stranded flier still strikes from where it fell; every other unit's own tile was already passable to it, so nothing else changes.
- **On screen.** ` (grounds)` after a side's crit column when its bow can crit a flier (crit above 0), the counter's included; the unit card's `grounded until [the next] <side> phase ends, moves on foot` or `, stranded: no move, may still act`; the event `unitGrounded` (`unit`, `by`, `side`, `next`), `<name> is grounded: moves on foot until [the next] <side> phase ends`; the item card's `Crit +20 against flying. A crit grounds a flier.`; the protocol unit's `grounded`.
- **No new enemy template.** The issue's premise was out of date: `wingrider` (skyrider, Mov 6, Iron Lance) already exists and holds Saltmarsh Ford's north bank above the river row, with Ottilie's Iron Bow in the party. It is the board the issue asked for.
- **The old bow on samples.** Journaled plays on Saltmarsh Ford and Brackwater Cut diverge under the new bow (Rook, and the wingrider). The header `effective_bows: on` restores the bow before this record on a map (`Grounding.ForMap`, applied wherever a combatant is built from a board, and no grounding), on the same pattern as `exit_after_move`. It sits on `saltmarsh_ford_0030`, `_0090`, `_0091`, `_brace`, `_brace_signatures`, `_chief`, `brackwater_cut_daylight` and `brackwater_cut_exit_after_move`, and on `saltmarsh_ford_0093.map`, a new copy of the shipped Saltmarsh Ford that replays Code's 547. No map under `content/maps` carries it.

## Measured

`--full` on Saltmarsh Ford, 200 seeds (`docs/measurements/saltmarsh_ford-703.txt`): gate 1 47/200 under both arms (old bow on the 0093 copy, arm B shipped); Ottilie's gate 4 drop 0.055 under both. Brackwater Cut and the other maps with fliers were not re-measured. `--smoke` is green.

## Open

- **Answered by 0168 (#723, round 220): a bow's crit on a flier now deals plain damage and grounds.** The original note follows.
- **A crit still kills the flier it would ground.** Crit is damage x3. Ottilie's Iron Bow deals 9 to the level 2 wingrider (17 HP), and the enemy archer deals 9 to Rook (17 HP), so a crit is 27 and kills either. Grounding fires only where a bow's plain hit is under a third of the flier's HP. Code's warm play 703 is the case: the forecast said `crit 23% (grounds)` twice and the wingrider died to a plain hit over the river. The lever is on the Table: a bow's crit on a flier grounds in place of tripling, or the bonus becomes the measured one.
