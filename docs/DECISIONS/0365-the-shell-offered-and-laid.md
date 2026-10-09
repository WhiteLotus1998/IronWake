# 0365 — The shell is offered and laid; the Sim reads Lotus's three targets (slice 3 of #1403)

Date: 2026-10-09. Issue #1403, slice 3. Builds the Sim read Lotus asked for in his Obsidian rulings of 2026-10-08 (Table #1368; the drake rider, the healer, the highest-Def front-liner; how often the shell eats a jab and how often a real blow), on 0361's shell. Fixture only: no armor tome ships.

## Built

- **Legal.** `Resolver.Legal` lists an armor tome the caster can wield with a use left: on the caster, then on each ally within the tome's `range`, in id order (`Armor.Casts`). Earth Armor is listed on its caster alone.
- **Never struck with.** An armor tome is not a strike weapon (`UsableWeaponAt`), so neither planner, `Legal` nor the attack menu swings it. Before this, a fixture armor tome built on a strike tome could be equipped and attacked with.
- **The heuristic lays it** (`HeuristicPlayer.Shell`, a `ShellAim`). With a shell tome in hand, and no area cast or hunt beating it, it lays the shell in place of its best strike when that strike is under even odds to kill (`ShellOverKill`, 0.5). The wearer must wear no armor and must be either an ally that has already acted (so the tile it meets the enemy phase on is known) or the caster at its cast tile. Of the wearers whose no-crit exposure is above 0, it picks the most exposed, from the tile where the caster's own exposure is least. A shell holder plans after the rest of the company. The aims are `exposed` (the default), `drake`, `healer`, `front` (the ally of the highest Def other than the caster) and `never` (held, never laid).
- **`--shell <map> [--seeds N] [--level N]`** plays the full finale company on a `deploy: all` board, once per arm. It reports wins, games laid, and how each shell ended: broken by a jab (the breaking hit's plain damage on bare Def under a quarter of the wearer's max HP), broken by a blow, broken by a hit no forecast names (watch, line, area), the wearer killed through it, or fallen unbroken. It also reports the breaking hit's median bare damage.

## The read (100 seeds, the Warden sample; `docs/measurements/shell-1403.txt`)

- **Holding it costs nothing:** no tome 36 wins, held and never laid 35.
- **Laying it on whoever is most exposed costs wins:** 27. Pell lays it in all 100 games and gives up a strike or a Spark Storm to do it.
- **The narrow aims cost less, because they fire less:**
  - drake 35, laid in 12;
  - healer 29, laid in 46;
  - front 30, laid in 44.
- **The shell eats real blows, never jabs:** 0 jabs in every arm. The breaking hit's bare damage has a median of 11 to 17. The enemy planner does not open with a jab. Killed through it: 13 on the exposed arm.

## What this says, for the Table and Lotus

The shell works as a shell. It eats a real blow every time it breaks. What it costs is the caster's action. On the keep, the heuristic's Pell is worth more striking than shelling. That is a reading of the heuristic and the keep, not a verdict on the spell. The number is Lotus's to sign (round 499).

## Not built

- The enemy planner still does not reorder to open with a jab; the read gives it no reason to yet.
- The client's ally aim waits on a tome shipping.
