# 0157 — The second tier: one advanced form per class (#704, slice 1)

Date: 2026-10-02. Issue #704 (Lotus's progression batch, item 1; Chat's round 216, Code's round 218). The shape is the Table's; the choices below are Code's implementation calls. Provisional.

## Decided

- **The data shape.** A class may name `advances: <base>` (`UnitClass.Advances`, the base's record; `UnitClass.BaseId`). The loader refuses an unknown base, the class itself, a hidden base, a base that is itself advanced (one step above a class at most), an advanced form that names `growthModifiers` (it grows as its base), and one that drops a base weapon type, each naming file, entry and field.
- **The step.** `Certifications.Check` refuses an advanced form to any unit not in its base, `needs to be a <Base> first` (requirement `advances`), before the level, ranks and stats. Certification stays lateral otherwise, as issue 72 built it: a unit in a form may still certify across into another first-tier class, giving the form up.
- **The numbers.** Level 10 and rank C in the base's main weapon (lance for Pikeman, Outrider, Skyrider and Bulwark; axe, bow, Lore and Faith for the others). A rank is reachable here because a unit trains the weapon in its base class, so no stat floor stands in. The seal is `advancedCertificationPrice` in `campaign.json`, 1000 (twice the first seal; the plain seal when the file names none). Stat modifiers are the base's plus a bump of 4 to 7 points total.
- **What ships in slice 1.** The eight forms with the names Chat leaned (all ordinary English words under #701's rule; none collides with a content id). The adds that are data: Halberdier and Lancer take the axe, Warden the sword, Sentinel the bow; Lancer keeps Canto.
- **Sky Captain stays at Mov 6 in this slice.** Dusk's unseen reach (#403) is the content's most Mov plus most range, so a Mov 7 class widens it from 8 to 9 everywhere and rewrites Brackwater Cut's journaled dusk lines. Slice 2 lands the +1 Mov with a decision on whether dusk's reach reads every class or only those an enemy can be.
- **Art.** An advanced form draws its base's silhouette and clips until it has its own (`make_art.py`'s `BASE`, `Main.Look.cs`); the art spec names every form's tokens and clips, generated, so an artist's file drops in by name. The id is `skycaptain`, one word, since a token name splits on underscores.
- **On screen.** `classes <unit>` prints a form as `Halberdier (from Pikeman): level 10, lance C`, and the header names both seals. A rank refusal names the type by its screen label (`needs lore C`), which #701 had missed.

## Open

- Slice 2: Marksman's bow range 3, Warden's heal reach 2, Scholar's strike-only Faith, Berserker's kill-heal, Sky Captain's +1 Mov, and a new mastery for each form. Until then Marksman, Scholar, Berserker and Sky Captain are numbers only.
- Slice 3: the advanced enemy templates on maps 7 to 10, gate 1 with and without the player's second tier, and the level-10 timing on the Sim's campaign median (two or three units over 10 by map 7).
