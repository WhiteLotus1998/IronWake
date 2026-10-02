# 0160 — The captain's three classes (#705, slice 1)

Date: 2026-10-02. Issue #705 (Lotus's progression batch, item 2; Chat's round 216). The shape is the Table's. The calls below are Code's implementation choices. Provisional.

## Decided

- **The data shape.** A class may carry `captain: true` (`UnitClass.Captain`). An advanced form must match its base's flag, so the loader refuses a mismatch, naming file, entry and field. A unit other than the cast's first, whether a template, cast member or hire, standing in a captain's class is refused on load.
- **The rule.** `Certifications.Check(unit, target, from, captain)` refuses in three cases, each with requirement `captain`:
  - anyone but the captain entering a captain's class (`only the captain takes <X>`);
  - the captain entering any other class (`<X> is not on the captain's ladder`), hidden classes and the eight's forms included;
  - the captain, once on the ladder, moving anywhere but up into the held class's own form (`the captain's ladder is one-way; <X> leads only to its own form`).
  The older overloads read "not the captain". The campaign record, the camp's class list and the Sim's `--levels` readiness pass `CampaignRecord.IsCaptain`, which means the cast's first.
- **The numbers.** The three certify at level 3 with no rank or stat floor (the captain starts a Levy at level 1), for the first seal (500). The forms need level 10 and sword C, since the sword is the one weapon all three keep, for the advanced seal (1000).
  - Vanguard: +2 HP, +1 Str, +2 Def, growth +10 HP and Def.
  - Marshal: +2 Mag, +1 Res, +2 Cha, growth +10 Mag and Cha. Mag rises because the Marshal casts Lore.
  - Ranger: Mov 5, +2 Dex, +1 Spd, growth +10 Dex and +5 Spd.
  - Champion adds the axe. Commander is cavalry at Mov 6 and adds the lance.
- **The camp row.** The captain's row ends `promotes at 3: Marshal, Ranger or Vanguard` while the captain is off the ladder, then `ladder: Ranger, then Pathfinder at 10` (the form row prints the same). The shipped campaign transcripts' captain rows change by exactly that suffix.

## Open (slice 2)

- New effect kinds: the Vanguard's adjacency +1 Def and Res, the Marshal's +10 Acc aura within 2, and the Pathfinder's forest and hill at cost 1. Until those ship, Vanguard and Marshal have no mastery and Pathfinder is numbers only.
- A mastery for each form.
- The Sim bar: gate 1 within 5 points across the three at both tiers, and gate 4's captain verdict positive under all three.
- The twelve origin-by-class combinations through `--smoke`.
- The art for the six. Until then each draws the Levy's art.
