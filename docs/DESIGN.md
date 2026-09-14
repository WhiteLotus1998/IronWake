# IRONWAKE — Design Document

Working title: **Ironwake**. A turn-based tactics game in the Fire Emblem: Three Houses lineage — grid battles, a small cast you grow, class paths, permadeath with a rewind — but its own game, not a clone. No Fire Emblem names, places, characters, or text anywhere in the repo.

This document is the source of truth, and it belongs to both partners (Chat and Code — see CLAUDE.md). Code that disagrees with it is wrong; if the document is wrong, fix the document in the same PR and record why in `docs/DECISIONS/`. Sections 1–12 are the floor. Section 13 is where the game gets interesting, and it is meant to be rewritten as we learn what's fun.

## 1. Identity

**Premise.** Ironwake is a crumbling border keep at the seam of three rival regions. An armistice is fraying. The keep trains *wardens* — and this year, because the crown stopped paying, the three regions' cadets were dumped into one under-funded cohort. The player is the new captain of that cohort. The cast's frictions and friendships are the story; the fraying armistice is the campaign.

**Pillars, in priority order.**
1. *Every turn is a decision.* If a random legal move wins as often as a thoughtful one, the map is broken.
2. *Numbers are honest.* The combat forecast shows exactly what the resolver will do. No hidden modifiers.
3. *Growth you feel.* Units level, certify into classes, and change what they can do on the map.
4. *Loss has weight, rewind has cost.* Permadeath is on; a limited rewind ("Recall") exists so a miscount isn't a run-killer.
5. *Small cast, big personalities.* 10 recruits + captain for the whole campaign. Depth over breadth.

**Out of scope for v1.** Monastery/calendar, romance, cooking, fishing, voice, animation, any graphics beyond the console. Rendering front-end (Unity or Godot) comes after the core is done and the campaign is fun in the console.

## 2. Architecture (non-negotiable)

- **Headless core.** `Ironwake.Core` contains all rules. It has no dependency on Console, System.Random, file IO, or time. Everything it needs is injected (`IRng`, content passed in).
- **Immutable state.** `BattleState` is an immutable record. The only way to change it is `Resolver.Apply(state, command) -> (BattleState next, IReadOnlyList<GameEvent> events)`. Events are the log; the CLI renders from events, never from diffing state.
- **Determinism.** Same seed + same command sequence = same state, byte for byte. RNG draws happen in a fixed, documented order. This is tested.
- **Rewind is free.** Because states are immutable, Recall is "pop the history stack." Replays are "re-apply the command list." Every bug report is a seed + command list.
- **Content is data.** Classes, weapons, terrain, units, and maps are JSON/text files under `/content`, validated on load with clear error messages. No stats in code.

## 3. Units and stats

Nine stats, integers: **HP, Str, Mag, Dex, Spd, Lck, Def, Res, Cha**. Cha exists from day one (used by battalions in Phase 3). Each unit has base stats, per-stat growth rates (%), a level (1–30), EXP (0–99), a class, an inventory (5 slots), and a set of abilities.

Classes define: movement type (Infantry, Cavalry, Flying, Armored), Mov, stat modifiers, usable weapon types, class growth modifiers, and one mastery ability (Phase 3). Movement types matter for terrain cost and for effective damage.

Level up at 100 EXP. Each stat rolls independently against its growth (unit growth + class modifier). Level cap 30 for v1.

## 4. Terrain

Square grid, 4-connected movement. No zone of control. Units may pass through allies, never through enemies, and may not end on an occupied tile.

| Tile | Glyph | Infantry | Cavalry | Flying | Armored | Avoid | Def/Res | Notes |
|---|---|---|---|---|---|---|---|---|
| Plain | `.` | 1 | 1 | 1 | 1 | 0 | 0 | |
| Road | `=` | 1 | 1 | 1 | 1 | 0 | 0 | reserved for later cav bonus |
| Forest | `^` | 2 | 3 | 1 | 2 | +20 | +1 Def | |
| Hill | `n` | 2 | 3 | 1 | 3 | +10 | +1 Def | |
| Mountain | `M` | 3 | — | 1 | — | +30 | +2 Def | |
| Water | `~` | — | — | 1 | — | 0 | 0 | |
| Fort | `F` | 1 | 1 | 1 | 1 | +30 | +2 Def, +2 Res | heals 20% max HP at start of owner's phase |
| Wall | `#` | — | — | — | — | — | — | blocks everything, including flyers |
| Gate/Throne | `T` | 1 | 1 | 1 | 1 | +30 | +3 Def, +3 Res | seize target; heals 20% |

`—` means impassable. Terrain avoid/def do not apply to flyers except on Fort/Throne.

## 5. Combat

3H-inspired, but these are Ironwake's formulas. All division is integer floor.

```
Burden        = max(0, WeaponWeight - Str / 5)
AttackSpeed   = Spd - Burden
Doubles       if AttackSpeed >= TargetAttackSpeed + 4

Atk           = Str + Mt   (physical)   |  Mag + Mt   (magic)
Effective     = Mt * 3 before adding Str/Mag, when weapon tag matches target movement type
Damage        = max(0, Atk - (Def | Res) - TerrainDefBonus)
Crit damage   = Damage * 3

Hit           = WeaponHit + Dex + Lck / 2
Avoid (phys)  = AttackSpeed + Lck / 2 + TerrainAvoid
Avoid (magic) = (Spd + Lck) / 2 + TerrainAvoid
HitChance     = clamp(Hit - Avoid, 0, 100)

Crit          = WeaponCrit + (Dex + Lck) / 2
CritAvoid     = Lck
CritChance    = clamp(Crit - CritAvoid, 0, 100)
```

**Rolls.** Hit uses the average of two rolls (0–99), i.e. displayed hit is a "true hit." Crit uses one roll. Draw order per strike: hit roll A, hit roll B, then crit roll only if the hit landed. This order is a tested contract.

**Sequence.** Attacker strikes; defender counters if the attacker is within the defender's weapon range and the defender has a usable weapon; then whoever doubles strikes again. Combat ends early on death. Brave/gauntlet double-strikes are Phase 3.

**Weapon types.** Sword, Lance, Axe, Bow, Reason (black magic, targets Res), Faith (white magic: heal and a few attacks). Gauntlets in Phase 3. There is **no weapon triangle**; Phase 3 adds Breaker abilities (e.g. Lancebreaker: +20 hit/avoid vs lances) as the counterplay layer. Weapons have Mt, Hit, Crit, Wt, Range (min–max), Durability, and optional effective tags.

**Ranges.** Melee 1; Bows 2 (2–3 with Phase 3 abilities); Reason/Faith 1–2. A unit with only a 2-range weapon cannot counter at 1, and vice versa.

**Durability.** Physical weapons have uses and break at 0 (broken weapons stay in inventory, usable at −5 Mt / −10 hit as a "broken" fallback so units are never helpless; repair comes with the between-map screen). Spells have uses per battle that fully refresh each map.

**Healing.** Faith heal = Mag / 2 + 5 + spell base. Healer gains 11 EXP per heal, +5 if the target was below 50%.

## 6. EXP

```
On any strike landed:  Exp = clamp(10 + 2 * (EnemyLevel - UnitLevel), 1, 30)
On a kill (added):     Exp = clamp(20 + 3 * (EnemyLevel - UnitLevel), 5, 70)
Bosses:                kill bonus +20
```

A unit earns EXP once per combat, computed from the best outcome in that combat.

## 7. Turn structure

Player phase → Enemy phase → (Ally phase if any) → turn counter increments. On a unit's turn: **Move** (optional), then one of **Attack / Item / Wait**. Cavalry get Canto (move remaining Mov after acting) in Phase 3, not v1.

**Win conditions** (one per map): Rout, Seize (captain on `T`), Defeat Boss, Survive N turns, Escape (all living units reach exit tiles).
**Loss conditions:** captain dies; or a map-specific protect-target dies.

**Recall.** 3 charges per map on Normal. Rewinds to any previous state in the current map's history. Charges do not refresh mid-map.

## 8. Enemy AI

Every enemy belongs to a *group* with one behavior:

- `Aggressive` — moves toward and attacks the best target every turn.
- `Hold` — never moves; attacks anything in range.
- `Guard` — behaves as Hold until any member of its group is attacked or a player unit enters the group's trigger zone; then the whole group becomes Aggressive. This is the main pacing tool for maps.
- `Boss` — Hold, plus never leaves its tile.

Target scoring, deterministic (ties broken by lowest unit id):
```
score = 100 if the attack can kill
      + expectedDamageDealt * hitChance/100
      - expectedDamageTaken * counterHitChance/100 * 0.5
      + 10 if the target cannot counter
      + 5  if the target is a healer
```
Tile choice: among tiles that allow the best-scoring attack, prefer highest terrain avoid, then fewest player units that can reach the tile next phase.

## 9. Content plan for v1

- **Cast.** Captain (fixed sword unit, Cha-heavy) + 10 recruits, 3–4 from each region, covering every weapon type and movement type at least once. Each recruit has a one-line personality, a home region, and two people they get on with (support hooks for Phase 3). Names are original.
- **Campaign.** 8 maps, escalating: 2 tutorials-in-disguise (small maps, one new idea each), 4 mid maps introducing Guard groups, reinforcements, forts, and a Seize, 2 finale maps. Maps are 12×10 to 20×16.
- **Difficulty.** Normal only in v1. Hard is a data change (enemy stat multipliers, fewer Recall charges) — leave the hook.
- **Between maps.** Minimal: repair, shop with fixed stock, level/class screen. Text only.

## 10. Map file format

A `.map` file is a header of `key: value` lines, then the grid, then a `units:` block. Glyphs per section 4. Example:

```
name: Old Mill Road
size: 12x10
win: rout
turn_limit: 20
recall: 3

............
..^^....n...
..^^..F.nn..
============
..~~........
..~~.....#..
.........#..
..........^^
............
............

units:
P captain 1,8
P recruit:mira 2,8
E soldier 9,1 group:mill behavior:guard trigger:6,0-11,4
E archer 10,2 group:mill behavior:guard
E brigand 6,5 group:road behavior:aggressive
B bandit_leader 10,1 group:mill behavior:boss
```

Enemy generic units are templates from `/content/units/enemies.json` scaled to the map's `enemy_level`.

## 11. Quality bar — what "not janky" means when no human is watching

The Sim harness (`Ironwake.Sim`) exists so quality is measured, not felt. Every campaign map must pass these gates before it is marked `tuned` in STATE.md:

1. **Beatable.** The heuristic AI player wins ≥ 60% of 200 seeded runs within the turn limit.
2. **Decisions matter.** The random-legal-move player wins ≤ 5% of 200 runs.
3. **No cheap shots.** Running the enemy phase from deployment positions with no player moves kills zero player units.
4. **No dead weight.** Across the 200 AI-player runs, every deployed recruit gets ≥ 1 kill in at least 30% of runs.
5. **Forecast honesty.** For 10,000 random combats, forecast damage/hit/crit equals resolver behavior (statistically for hit/crit, exactly for damage).
6. **Determinism.** 100 random seeds × 100 random commands replay byte-identical.
7. **Speed.** A full AI-vs-AI map resolves in under 1 second.
8. **Crash-free.** 10,000 random legal command sequences across all maps throw nothing.

Gates 5–8 run on every PR (`Ironwake.Sim --smoke`). Gates 1–4 run when maps change.

## 12. Phases

- **Phase 1 — Core rules.** Sections 2–7 with tests. ASCII renderer. Done when a scripted skirmish resolves end to end.
- **Phase 2 — Playable.** Enemy AI, CLI with forecasts and a command language, Sim harness, first three maps and the cast. Done when all gates pass on three maps.
- **Phase 3 — The 3H layer.** Combat arts (durability cost, bonus stats), weapon skill ranks E–S gained by use, class certification (rank requirements + a certification roll), mastery abilities, Breaker abilities, gauntlets, battalions and gambits driven by Cha, supports (adjacency tally → C/B/A bonuses), Canto, Recall UI. Full 8-map campaign. Between-map screen.
- **Phase 4 — Presentation.** Front-end in Unity or Godot consuming `Ironwake.Core` as a library. Not started until Phase 3 gates pass.

## 13. Fun — direction and experiments

The gates in section 11 catch jank. They cannot tell us whether a turn was *worth taking*. This section is the creative direction and a starting list of things to try. Both partners add to it; killed ideas get one line in `docs/DECISIONS/` so we don't circle.

### What we're chasing
- **The tense turn.** The turn where you can see two good moves and one of them is wrong. If a map has none of those, it's a spreadsheet.
- **The story the map tells by itself.** Guard groups that wake up at the wrong time, a fort you didn't take, a rival who remembers. Emergent drama from rules, not cutscenes.
- **Growth with a face.** A recruit who was useless on map 1 and carries map 5 should feel like *your* doing, and the class they ended up in should be a choice you can point to.
- **Rewind you argue with yourself about.** Recall should be something you're slightly ashamed to use.

### Starting experiments (build on `experiment/<name>`, play, keep or kill)

1. **Rapport and Rivalry.** Adjacent allies build rapport (the support base). But recruits from *different* regions start with rivalry, and a rival adjacent gives +10 crit and -5 hit — they show off. Rapport eventually overwrites rivalry. Cohort friction as a mechanic, not a cutscene.
2. **Commander's Word.** Once per map the captain calls one order, scaled by Cha: *Hold* (+15 avoid to all allies this enemy phase), *Press* (+1 Mov this phase), *Rally* (heal 15% all). A gambit before battalions exist, and a reason Cha matters on day one.
3. **Recall scars.** Rewinding is free of charge-count cost only three times, but the unit whose death you undid earns no EXP for the rest of the map — the near-miss shakes them. Cheap, thematic, and makes charge use a real decision.
4. **Named rivals who level.** A crown cohort — six named enemies — appears on maps 2, 4, 6, and 8 with levels tracking the player's average, carrying grudges: a rival whose ally you killed targets that unit first. Data-driven recurring antagonists.
5. **Ironwake itself.** The finale is the defense of the keep. Between maps the player spends repair points on the keep's `.map` — rebuild a wall, dig a ditch (water), raise a fort — editing the finale's terrain. Your base is a level you've been building all game.
6. **Certification as a puzzle.** Instead of a roll, certifying into a class is a one-turn micro-map: "kill the dummy in one turn with this loadout." Pass and the class is yours. Tiny puzzle maps are cheap to author and genuinely satisfying.
7. **Dusk maps.** Vision radius shrinks by one tile each turn. Not fog of war — a countdown. Pressure without RNG.
8. **Carry the fallen.** Permadeath stays, but an ally who reaches a fallen recruit's tile recovers their weapon, and the weapon keeps their name. Loss with a keepsake.
9. **Map events.** A minimal trigger system: on turn N or when a tile is entered, open a gate, collapse a bridge, spawn reinforcements from an edge. The single most reusable tool for making maps tell stories.
10. **Enemy retreat and regroup.** Enemies below 30% HP with an escape route fall back to the nearest fort and heal. Enemies that act like they want to live make players commit.

### The Fun Gate (part of `tuned`, alongside gates 1–8)
Both partners play the map by hand and independently rate it 1–10 on *tension*, *choice*, and *surprise*, and name their best turn in `docs/PLAYTEST.md`. All three scores ≥ 7 from both, or it goes back. If we disagree by 3+ on any axis, the Design Table gets a thread before anyone touches the map.
