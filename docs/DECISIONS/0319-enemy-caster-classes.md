# 0319: Enemy caster classes, flagged `enemy`, dark on the Grave Caller alone

Date: 2026-10-07. Issue #1286 slice 5. Source: Lotus on enemy casters (#1251, 6033129482), his second rulings (0317 items 1 and 5), the unblock on #1286 (6046619235). Provisional.

## Context

#1286 step 1 asks for an enemy caster per school the company meets before wielding it: ice, earth, lightning (the Sworn's, late) and the dark-mage mini-boss. 0317 lifted the names (placeholders, no build waits on one) and scoped dark to the mini-boss and his followers. `classes.json` serves both sides, so a new first-tier class with no flag would be certifiable at camp, and `hidden` is not a guard because a quest may promote into a hidden class.

## Decision

- **`enemy: true`** on a class: nobody certifies, trials, is promoted or is cast into it. The loader refuses it on a cast member, as a campaign trial's class, as an advanced form's base, and with `advances`, `captain`, `hidden`, `unique` or `certification`. Certification refuses it with its own reason (`enemy`); the camp's class lists (CLI and client) leave it out.
- **Dark only on an enemy class.** The loader refuses `dark` in a non-enemy class's `schools`. The Ledger's class (0317 item 1, pitched on #1247) relaxes this when Lotus signs its shape.
- **`description`** on a class, optional, never blank; a placeholder begins `Placeholder.`.
- **Four classes**, placeholder ids and names, infantry, Mov 4, Lore only, one school each: `frost_caster` (ice; Mag +2, Res +1), `earth_shaper` (earth; Mag +1, Def +1, Res +1), `storm_caster` (lightning; Mag +3, Res +2), `grave_caller` (dark; Mag +3, Res +2), the mini-boss's class and his followers'.
- **`drops:`** (0301) is unchanged; a fixture test shows a Grave Caller on a drops tile sending its grimoire to the wagon.

No template, tome or map seats the classes: ice, earth and dark tomes wait on the signed #1247, and placement waits on STORY (#1144) and a Table round. No gate, transcript or Sim number moves.

## Kill criterion

None of its own: the flag is plumbing. If the placement round finds a school better taught by an existing class given that school (as the Hexer's Adept teaches fire), the class it replaces is deleted.
