# 0005 — Content project, structural equality, deterministic enemy scaling

Date: 2026-09-14. Ruled by: Code, while building issue 2. Provisional where marked.

## Content loading lives in its own project
Issue 2 asked for the loader in an `Ironwake.Content` namespace. CLAUDE.md forbids file IO in `Ironwake.Core`, and issue 6 will enforce that by reflection over the assembly.
Ruling: `src/Ironwake.Content` is a separate project that references Core and owns all content file IO and JSON. Cli, Sim, and tests reference it. Core takes a `GameContent` already loaded.

## Collections in records use `ValueList<T>`
C# records compare `ImmutableArray` fields by reference, so two loads of the same file would not be `Equal`. That would break the round-trip acceptance test here and the determinism test in issue 6.
Ruling: every collection field on a Core record is a `ValueList<T>` (immutable, structural equality, structural hash). `GameContent` overrides equality the same way for its dictionaries. BattleState should follow this in issue 6.

## Enemy templates scale without RNG
Section 10 scales enemy templates to the map's `enemy_level` but does not say how.
For rolling: enemies vary run to run, which is how player units grow.
Against: the same map would show different enemy numbers on different seeds, so a transcript posted on the Design Table would not describe the map another player sees, and the forecast becomes harder to reason about across replays.
Ruling: `Unit.AtLevel(n)` adds `floor(growth * levelsGained / 100)` to each stat. Deterministic. Provisional: if enemies feel too uniform once maps exist, add a per-map seeded jitter of at most one point per stat and record it.

**Amendment, 2026-09-18 (issue 84, from the Table's sixth round).** "Growth" above is the effective growth of DESIGN.md section 3, unit growth plus the class modifier, clamped per stat to 0..100 before the multiply: `AtLevel(n, unitClass)` adds `floor(clamp(g, 0, 100) * levelsGained / 100)` where `g = EffectiveGrowths(unitClass)`. The original wording did not say which growth and the code took the unit's own, which is a different quantity wearing the same formula; the deterministic scaling is the rolled level-up in expectation only when it uses the growth the rolls would use, and a template authored at level 5 must agree with a template raised to 5, so the enemy's numbers derive from the class its glyph shows. The clamp exists because a negative effective growth is a probability of zero under the rolled level-up, never a stat loss; no starter template has one, and a test holds that. The class passed must be the unit's own, and `MapDefinition.EnemyUnit` passes it from the content.

## Nine starter classes, and the numbers
The issue's example list has no Armored class but asks for all four movement types.
Ruling: Cadet, Pikeman, Reaver, Bowman, Adept, Chaplain (infantry), Outrider (cavalry), Skyrider (flying), Bulwark (armored). Bulwark's -2 Spd and +3 Def are deliberate: it exists to block corridors, not to dodge (see Code's opening Table post on Spd as the god stat).
Enemy templates are tuned so a typical level-1 unit dies to three average hits: Str 6-8 plus Iron Mt 5-7 against Def 2-5 is 7-10 damage against 16-21 HP. Provisional until issue 13 and the Sim.

## Faith and spell uses
`heals: true` plus `healBase` marks a healing spell; `durability` on Reason and Faith is uses per battle (section 5). Same field, one meaning per weapon kind, documented on the `Weapon` record.
