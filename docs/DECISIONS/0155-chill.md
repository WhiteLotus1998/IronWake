# 0155 — Frozen iron and chill (slice 2 of #702)

Date: 2026-10-02. Issue #702's amendment (Lotus's frozen iron; Chat's round 217, Code's round 218). The rule (Mov -1 on a hit until the struck side's next phase ends, no stack, floor 1, printed, bosses too, Kinsbane never) is the Table's; the choices below are Code's implementation calls. Provisional.

## Decided

- **Frozen iron is derived, not stored.** `Frost.Shape` runs last in the weapon chain (`BattleUnit.EquippedWeapon`, `UsableWeaponAt`), after Kinsbane, the heirloom and the forge. A weapon is frozen iron when content marks it `frozenIron: true`, when it is an heirloom at its last stage, or when it refines on frozen iron (`Material.Rare`, a main-line signature) and its stack's `Refines` has reached `forge.rare`. A hungering weapon is never frozen iron, whatever its mark or stack says. The forecast, the resolver and the planners all read the one flag.
- **The load guard.** `frozenIron` on a hungering weapon is refused ("a hungering weapon is never frozen iron; it is what the iron holds"), and so is `frozenIron` on a spell. Both errors name the file, the entry and the field.
- **The clock** is `BattleUnit.Chill`, counted the way `Spent` is. A hit sets 1. The struck side's phase beginning turns 1 to 2. A phase of that side ending clears 2. So an enemy chilled on the player phase moves short through the enemy phase. A unit chilled by a counter on its own phase moves short through its *next* phase, which is the reading of "until its side's next phase ends" that never spends the chill on a phase in which it can no longer move. A second hit sets 1 again (it refreshes the clock and takes no second point). Only a unit still standing after the combat is chilled. The watch shot chills too.
- **Mov.** `Frost.Mov` takes 1 off in `BattleState.ReachOf`, floored at 1, after Press. A Canto unit acting in place gets the chilled Mov as its budget. Fall back's fixed move and the messenger's open-field estimate are untouched.
- **On screen.** ` chills` after the strike columns of a side whose weapon is frozen iron, on every forecast line, the enemy's included. The card prints `Frozen iron: a hit chills, Mov -1 until the end of the target's side's next phase` under the weapon. A chilled unit's card prints `chilled: Mov -1 until <side> phase ends`, with `the next` while the clock has not begun on its own phase. The event `unitChilled` (`unit`, `by`, `side`, `next`) reads `<name> is chilled: Mov -1 until [the next] <side> phase ends`. In the protocol, a unit carries `chill`. `threat`'s own lines don't print chill; they plan the reach it leaves.
- **A sample header,** `woken: <recruit>`, issues that recruit's bound heirloom at its last stage, the same shape as `kinsbane:`. Nothing shipped issues a frozen-iron weapon until #656, so `--smoke` is byte-identical to main. The sample is `docs/samples/the_tollgate_frost.map`.

## Not in this slice

- Gate 4 with a frozen-iron carrier: no shipped board fields one. It's read when a signature or the lance ships.
- Obsidian's loadout measurement (0154) is still owed.
