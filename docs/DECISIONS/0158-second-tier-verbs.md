# 0158 — The second tier's verbs (#704, slice 2)

Date: 2026-10-02. Issue #704 (Lotus's progression batch, item 1; Chat's round 216, Code's round 218; slice 1 is 0157). The shape is the Table's; the choices below are Code's implementation calls. Provisional.

## Decided

- **The verbs are class abilities**, held while in the form and lost on leaving it, as Canto is on the cavalry. Two new effect kinds: `range` (a weapon type, or `heals: true`, reaches N tiles further at its far end; read wherever a unit's weapon is read, to strike or to heal: `GameContent.WeaponOf`, `AbilityRules.Shape`) and `killheal` (a kill made with a weapon of the named type heals N, never above max HP, as a `UnitHealed` after the combat).
- **Marksman: Long Draw**, `+1 range with bows` (Iron Bow 2-3). **Warden: Far Mending**, `+1 range on healing spells` (Salve 1-2, Beacon 1-3). I read "heals reach 2" as one tile further, not a floor of 2, so Beacon also gains and the one rule covers both.
- **Scholar: Faith, strike spells only.** A class field `strikeOnly` names types the class strikes with but never heals with; `Unit.CanWield` refuses a healing spell of such a type, and the Item command says `a Scholar strikes with faith and never heals`. A second field, `grants`, raises a rank on entering the class: the Scholar enters at Faith D, because an Adept never trains Faith and Radiance, the only strike spell, is D. Without it the verb could never be used.
- **Berserker: Blood Price**, `an axe kill heals 5`, as a class ability rather than the mastery the issue's table names: a mastery takes 12 combats to earn, and the form should change a turn from the step it is taken. Its mastery is Hard to Kill (+3 HP, +1 Def).
- **Sky Captain: Mov 7.** Dusk's unseen reach (`GameContent.LongestReach`, #403) now reads only the classes an enemy template is in (every class when the content has no templates), since the dark hides enemies and only a template is one. It stays 8 on the shipped content, so Brackwater Cut's journaled dusk lines are unchanged. Slice 3's advanced templates will widen it when a map fields a Sky Captain enemy, which is the honest answer then.
- **Eight masteries, 12 points each:** Set Haft (Halberdier, +10 Evade and Crit Evade with a lance), Hard to Kill (Berserker), Still Breath (Marksman, +10 Crit with a bow), Old Texts (Scholar, +1 Mag +2 Res), Steadfast (Warden, +2 Def +1 Res), Couched Lance (Lancer, +10 Acc and Crit with a lance), Wind Rider (Sky Captain, +2 Spd), Iron Footing (Sentinel, +2 Def +2 Res). Plain English names under #701's rule.
- **On screen.** `classes <unit>` prints a form's adds on its line: `Halberdier (from Pikeman; adds axe)`, `Marksman (from Bowman; adds Long Draw)`, `Scholar (from Adept; adds faith (strike spells only))`, `Sky Captain (from Skyrider; adds +1 Mov)`.
- **Art.** The Scholar now strikes with Faith, so the spec derives eight `scholar_faith_*` clips; generated as the Adept's until it has its own.

## Open

- Whether the forecast should print Blood Price's heal on a lethal line, as it prints `(grounds)`.
- Slice 3: the advanced enemy templates on maps 7 to 10, gate 1 with and without the player's tier, the level-10 timing on the Sim's campaign median, and gate 4's ablation across the forms.
