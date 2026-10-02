# 0166 — Unique classes, the other door: the shape, and Maud's Field Surgeon (#706, slice 1)

Date: 2026-10-02. Issue #706. The rule is the Table's (Lotus's batch, item 3; rounds 216, 217; DIALOGUE "Progression"): a unique class is offered at the second promotion beside the standard advanced form, never both, names a measure it loses on, and Rook (Scout) and Maud (Field Surgeon) are the two. Bet's Sergeant shipped with #691 (0146). The shapes below are the Builder's and are provisional.

## Decided (the Builder, provisional)

- **A unique class is data on `classes.json`.** It names `advances` (its member's base), `unique` (the member), `unlockedBy` (a campaign quest whose member is that unit) and `loses` (the measure). The loader refuses a unique class that is hidden, on the captain's ladder, without a standard form beside it, or a second for one member above one base; `unlockedBy` and `loses` without `unique`; a `unique` that is not a cast member or is the captain; an `unlockedBy` that is not that member's quest (checked where the campaign has quests).
- **The doors are kept on the unit.** `Unit.Doors` records each base whose second promotion the unit has passed and the form it took, written to the save as `doors` only when non-empty (older saves read as none). Certification refuses any other form above a base the unit has a door on. This closes the sideways loophole (Warden to Chaplain to Field Surgeon) without a history of every class held. A first-tier class records nothing.
- **The unlock is the quest's win.** `Certifications.Check` takes the won quest ids; the camp passes `CampaignRecord.WonQuestIds`. Outside a campaign nothing is won, so a unique class with `unlockedBy` is refused.
- **The measure is computed, not declared.** `SidegradeMeasure` has one member, `reach` (the farthest tile any heal the unit may cast in the class reaches). `Sidegrades.Measure` reads it, and a test asserts every unique class loses its named measure to its standard form. Rook's Scout will add `damage`.
- **The Field Surgeon** (`fieldsurgeon`): Faith, `healOnly: ["faith"]` (the mirror of 0158's `strikeOnly`, so Radiance cannot be wielded), Mov 4, modifiers Mag +2, Res +2, Spd +1, certification level 7 and Faith C, seal 1000. **Steady Hands** is a new effect kind, `mending` (factor 2, reach 1): the heal is multiplied in `Combat.Heal`, so the forecast and the resolver read one number, and a healing spell's far end is held at 1 after any `range` effect. **Ward Rounds** is Canto with `after: heal`: owed after a Use that heals another unit with a spell, never after a Wait, an item on herself or anything else. Mastery **Triage**, +2 Spd and +1 Res. It has no enemy template, since the enemy never fields a unique class; its token and clips are generated and draw the Chaplain's shape.
- **Hand play.** None this slice. The door opens at the earliest after Maud's quest 1 and at level 7, so a campaign reaches it on map 6 at the soonest; a hand play needs a save built for it. The first campaign chair that reaches it journals the choice.

## Not built here

- Rook's Scout (+2 sight on dusk maps, `threat` printing sleeping groups' numbers within 6, loses Str growth and lance power to the Sky Captain), unlocked by her quest 1, which does not exist yet (#633, #656). Slice 2.
- The Sim's gate 4 ablation of a unique class against its standard form: the Sim's player never certifies.
- The Godot client draws no class list.

## Kill / revisit

If a campaign chair takes the Field Surgeon and never misses the Warden's reach, the loss is not a loss: the first lever is the reach (Beacon to 2), not the factor.
