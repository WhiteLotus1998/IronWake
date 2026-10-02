# 0154 — The weapon ladder, obsidian and frozen iron (slice 1 of #702)

Date: 2026-10-02. Issue #702 (Lotus's progression batch, items 6 and 7; Chat's rounds 216 and 217, Code's round 218). The ladder, obsidian's shape and the name are the Table's; the choices below are Code's implementation calls. Numbers provisional.

## Decided

- **The ladder is content.** The gate already existed (`Unit.CanWield`, issue 67). Steel and Bolt and Radiance go to D, Ridgeblade, Longpike and Beacon to C, the four obsidian weapons enter at B; the Toll weapons, the Post Maul, Kinsbane and the Family Lance stay E.
- **Templates.** The six enemy templates carrying a D weapon (`acolyte`, `grange_reeve`, `postern_keeper`, `bandit_leader`, `ford_chief`, `finale_lord`) declare it under `ranks`. The existing load error (`inventory[i].item`, "declare the rank under 'ranks'") is the guard; a test holds that every such template can wield what it carries.
- **Maud starts at Faith D**, so her opening Radiance stays hers. The only change this makes to a recorded game is the rank points printed in the record lines of four campaign transcripts (re-recorded); `--smoke` is byte-identical to main but for timings, and `--full the_mill` reads 133/200 as before.
- **Glass.** A weapon field `glass` (only on a priced physical weapon bound to no one, else a load error naming the field). Glass is never repaired (`RepairPricePerUse` is null) and never Refined (`Forge.MaterialFor` refuses it), and both refusals print the smith's line, `You don't mend glass. You buy another.`
- **Obsidian's numbers.** Steel's Mt +2, steel's hit (the issue named none), Crit 25, iron's weight, 8 uses, 1800 (twice steel). The bow keeps the flier tag until #703 settles bows. Stocked on the three screens after the raid (Sallow Grange, Brackwater Cut, the keep).
- **The ceiling's floor.** The signature ceiling compared an item with shop weapons of its own rank; with steel at D, the E Family Lance would have been held to the Iron Lance alone and read 1.41. A signature is now held to the shop's weapons of its type from its own rank up to D (`SignatureCeiling.FloorRank`): signatures reach their owners after map 2, when the ladder puts D in a fighting unit's hands. An item above D is held to its own rank alone. The lance reads 1.14 as before.
- **Frozen iron** is the screen's name for the rare material (`Forge.Label`): the camp's Stores line, the Refine line, quest payouts and the validator's line. The id `rareMaterial` and `Material.Rare` stay.

## Not in this slice

- Chill, the frozen-iron weapon state, and Kinsbane's refusal as frozen iron (#702's amendment) are slice 2: chill reaches `threat`'s reach and both planners, and ships with the contract test round 218 names.
- The campaign rank report waits on all ten maps, as the issue says.
- Obsidian's measurement against the ceiling: the Sim's `--ceiling` reads bound items only, and obsidian is a shop weapon. Its read as a loadout is owed with slice 2's chill measurement.
