# 0312: Rampart is a flat +5 Def on the ground it lays; armor is a tome-named rider with its own numbers

Date: 2026-10-07. Issue #1282. Source: Lotus's #1247 rulings and names (#1251, 6032995734, 6033061250; DECISIONS/0307), 0300, 0311. Provisional.

## Context

Lotus renamed Cairn to Rampart and gave it a flat +5 Def, its own number rather than "a hill tile". He also asked for Earth Armor (stone around the caster, heavy, about +10 Def with a Mov cost) with Obsidian Armor at the top of the ladder. The issue's lean put Rampart's number on the raise rider as a `def` field.

## Decision

- **Rampart's number lives on the terrain row the rider lays, not on the rider.** 0300 made an overlay the battle's map retiled, so every terrain reader (both combatant builders, the planners, `threat`, the heal, the Sim) reads it with no rule of its own. A rider `def` would be a second source for the same number and would teach each reader about overlays. `earthwork` in terrain.json is now Def 5, Avo 0, Res 0, flyers included (it was a fort's 15/2/2). An overlay replaces the tile's terrain and never rises on a fort, so a Rampart never stacks. The ground keeps its id and its name, Earthwork. Rampart is the spell, and no shipped tome exists yet.
- **Armor is a tome-named rider that borrows earth's raise**, as sunder does (`SchoolRider.Borrows`). Earth Armor and Obsidian Armor differ, so the tome carries `armor: { def, mov, phases }`. It is required with the rider, refused on any other tome, and refused as a school's own rider.
- **Cast on self through the Item action.** `item <unit> <slot>`, or naming the caster. Another target is refused. It spends a use and the action and earns no EXP. Art, wield (school, rank, `minMag`) and uses are checked as a raise checks them.
- **The status.** `ArmorMark` on the caster: +Def in every combat, through the `beside` stats slot that Opening and Formation use, and -Mov with a floor of 1 (the chill's floor) wherever Mov is read: reach, dash, Canto, the escape count, the hunt. It lasts through the caster's next `phases` own phases and the enemy phases between, and falls as the last ends (`armorFell`). A recast replaces it and refreshes the clock. Sunder does not strip it. It is board state, so Recall restores it. The protocol carries `armor` on the unit and the `armorDonned` and `armorFell` events.
- **Leans:** Earth Armor +10, Mov -2, two phases. Obsidian Armor +14, Mov -2, three phases, a grimoire under `minMag` (0301). The enemy never casts it, and `Legal` and the Sim do not offer it, so no gate moves.

## Kill criterion

Cut it back if a journal shows armor making a unit unkillable with no cost felt: Mov -2 never forced a choice across the phases it held. The first lever is one phase fewer. The second is a smaller Def.
