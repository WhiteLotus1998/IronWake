# ISSUES — seed backlog

The Bootstrap creates these as GitHub issues in this order. Each has a title, labels, and a body. Later issues are filed by Chat Claude and the Critic; this file is not maintained after bootstrap.

---

### 1. Bootstrap: solution, CI, labels, backlog, Design Table
Labels: `ready` `phase-1`

See docs/ROUTINES.md section 1. Close when the bootstrap PR is open.

---

### 2. Core data model and content loading
Labels: `ready` `phase-1`

Implement the types in DESIGN.md sections 3–5 as immutable records in `Ironwake.Core`: `Stats`, `Unit`, `UnitClass`, `Weapon`, `WeaponType`, `MovementType`, `Terrain`, `Inventory`. Add a `ContentLoader` in a separate `Ironwake.Content` namespace (may use System.Text.Json; Core itself must stay BCL-only) that reads `content/classes.json`, `content/weapons.json`, `content/terrain.json`, `content/units/*.json` and validates every entry with an error naming file, entry, and field.

Ship the starter content: the 9 terrain types from the table in section 4; 8 classes covering all four movement types (e.g. Cadet, Soldier, Fighter, Archer, Mage, Priest, Cavalier, Pegasus Knight — original names welcome); 2–3 weapons per type at Iron/Steel tier with Mt/Hit/Crit/Wt/Range/Durability; enemy templates for the same classes.

Acceptance: round-trip tests (load → serialize → load equals); a test per validation rule that shows it firing; a `dotnet run --project src/Ironwake.Cli -- validate` command that loads all content and prints OK or the first error.

---

### 3. Map format, parser, ASCII renderer
Labels: `ready` `phase-1`

Implement the `.map` format from DESIGN.md section 10: header, grid, `units:` block with `P`/`E`/`B` prefixes, group/behavior/trigger attributes. Parser errors name the line. Renderer prints the grid with units overlaid (player letters uppercase, enemies lowercase, boss `!`), plus a legend.

Acceptance: parse → render → parse round trip; a test for each parse error; three sample maps under `content/maps/` including the example from the design doc.

---

### 4. Movement: reachable tiles
Labels: `ready` `phase-1`

Dijkstra over the grid with per-movement-type costs from terrain, blocked by enemies, passable through allies, cannot end on an occupied tile, impassable terrain honored, walls block flyers. Return the reachable set and a path to each tile.

Acceptance: table-driven tests for every terrain × movement type cost in section 4; a test where an enemy blocks a corridor; a test where an ally in the corridor is passable; a test that a flyer crosses water and is stopped by a wall.

---

### 5. Combat: forecast and resolution
Labels: `ready` `phase-1`

Implement section 5 exactly. `CombatForecast` (damage, hit, crit, doubles, for both sides, plus who counters) and `CombatResolver` that consumes `IRng` in the documented draw order and returns a strike-by-strike event list. Implement `IRng` with a seeded xorshift or PCG; two-roll average for hit, single roll for crit.

Acceptance: table-driven tests using the exact formulas with hand-computed values; a test that draw order matches the contract; a test that a 2-range-only unit cannot counter at 1; effective damage test; a statistical test that over 100k trials displayed hit ≈ realized hit within 1%.

---

### 6. BattleState, commands, resolver, events, history
Labels: `ready` `phase-1`

Immutable `BattleState` (map, units by id, positions, turn, phase, rng state, recall charges, history). Commands: `Move`, `Attack`, `UseItem`, `Wait`, `EndPhase`, `Recall(toIndex)`. `Resolver.Apply(state, command)` returns `(next, events)` and rejects illegal commands with a typed reason, never an exception. History is the list of prior states; Recall pops back and spends a charge.

Acceptance: determinism test (100 seeds × 100 random legal commands replay byte-identical via a canonical serializer); illegal-command tests for every rule; Recall test proves the restored state equals the historical one and the charge is spent; no `System.Random`, `Console`, or IO referenced anywhere in Core (enforce with a reflection test over the assembly's referenced types).

---

### 7. Turn loop and win/loss conditions
Labels: `ready` `phase-1`

Phases, per-unit acted flags, Fort/Throne healing at phase start, end-of-phase transitions, turn counter, and the five win conditions plus the two loss conditions from section 7. Expose `BattleOutcome` on the state.

Acceptance: one test per win/loss condition; a test that a unit cannot act twice; a test that fort healing rounds and caps correctly.

---

### 8. EXP, level-ups, growths
Labels: `ready` `phase-1`

Section 6. EXP awarded once per combat from the best outcome; level up at 100 with independent growth rolls using the injected RNG (draw order: HP, Str, Mag, Dex, Spd, Lck, Def, Res, Cha); level cap 30; healer EXP.

Acceptance: table tests for the EXP formula at level differences −10, 0, +10; a level-up test with a fixed RNG showing exactly which stats rose; a cap test.

---

### 9. Inventory, equipment, items, durability
Labels: `ready` `phase-1`

Five-slot inventory, equipped weapon, `Vulnerary` (heals 10, 3 uses), durability decrement per strike, broken-weapon fallback per section 5, spell uses per battle with refresh at map start.

Acceptance: durability hits 0 and the fallback stats apply; spells refresh; UseItem on an empty slot is a typed illegal-command reason.

---

### 10. Enemy AI v1
Labels: `ready` `phase-2`

Section 8: groups, the four behaviors, Guard trigger zones, deterministic target scoring, tile choice. The AI produces a command list for the enemy phase; it does not mutate state itself.

Acceptance: a test where Guard does not move until triggered; a test where the kill option beats a higher-damage non-kill; a test that ties resolve by unit id; a test that a Boss never leaves its tile; a full enemy phase on the sample map replays identically for the same seed.

---

### 11. Playable CLI
Labels: `ready` `phase-2`

`dotnet run --project src/Ironwake.Cli -- play content/maps/<name>.map [--seed N] [--script file]`. Commands: `move <unit> <x>,<y>`, `attack <unit> <target>`, `item <unit> <slot> [target]`, `wait <unit>`, `end`, `recall <n>`, `forecast <unit> <target>`, `show <unit>`, `map`, `help`. Print the map after every command, the forecast before an attack, and every event in plain ASCII. `--script` feeds commands from a file for tests and the Critic.

Acceptance: a scripted playthrough of the sample map runs to a win in the test suite; every command has a usage error path; output contains no emoji or box-drawing characters.

---

### 12. Sim harness and quality gates
Labels: `ready` `phase-2`

`Ironwake.Sim` with two player controllers: `HeuristicPlayer` (uses the same scoring as the enemy AI plus "keep the captain alive" and "heal below 50%") and `RandomLegalPlayer`. Implement gates 5–8 from section 11 behind `--smoke` (fast, runs in CI) and gates 1–4 behind `--full <map>` and `--full --all`. Print a metrics table and exit non-zero on any failed gate.

Acceptance: `--smoke` runs under 30 seconds in CI; `--full` on the sample map prints all eight metrics; a deliberately broken map (enemy in one-shot range of deployment) fails gate 3 in a test.

---

### 13. Starter cast and the first three maps
Labels: `ready` `phase-2` `content`

Author the captain and 10 recruits per section 9 with original names, one-line personalities, regions, base stats, growths, and starting classes/inventory. Author maps 1–3 (small; one new idea each: basic combat, a Guard group and a fort, a Seize). Tune until all eight quality gates pass on each map and mark them `tuned` in STATE.md. Record every tuning decision that changed a design number.

Acceptance: `--full --all` passes; STATE.md map table updated; a decision record explaining the cast's weapon/movement coverage.

---

### 14. First Critic pass
Labels: `ready` `critic` `phase-2`

Run the Critic checklist from docs/ROUTINES.md against everything merged so far. File findings as separate issues. This issue exists so the first Critic run happens even before the routine is scheduled.

---

### 15. Fun Gate and playtest journal
Labels: `ready` `phase-2`

Add `docs/PLAYTEST.md` entries for maps 1–3 from Code's own hand play (`--script`), with tension/choice/surprise scores and a best turn each. Post the same on the Design Table and ask Chat for its scores. A map moves to `tuned` in STATE.md only when both partners clear the Fun Gate in DESIGN.md section 13. If a map fails, file a `content` issue with what specifically was dull and a proposed fix.

---

### 16. First experiment
Labels: `ready` `experiment` `phase-2`

Pick one experiment from DESIGN.md section 13 — whichever the Design Table has leaned toward, or Commander's Word if the Table is silent — and spike it on `experiment/<name>`. Play it on map 2. Post the journal on the Table. Keep or kill within two sessions; either way, a decision record.

---

## Phase 3 backlog (filed by Chat after Phase 2 gates and the Fun Gate pass)

Combat arts · weapon skill ranks and certification · mastery abilities · Breaker abilities · gauntlets · battalions and gambits · supports · Canto · Recall UI polish · maps 4–8 · between-map screen · Hard difficulty data.
