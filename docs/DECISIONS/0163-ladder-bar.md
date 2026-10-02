# 0163 — The captain's ladder's bar (#705, slice 3)

Date: 2026-10-02. Issue #705 (Chat's round 216 set the bar: gate 1 within 5 points across the three at both tiers, gate 4's captain verdict positive under all three, origin by class through `--smoke`). DECISIONS/0160 and 0162 are slices 1 and 2. The method is Code's; provisional.

## Decided

- **`--ladder [--seeds N] [--map <id>]`** (`LadderRun`) measures the bar per content map. It fields the cast's captain unpromoted and in each ladder class, reads gate 1 and gate 4 under each, prints a row per class with the captain's own action mix, then the tier's spread against 5 points. Exits non-zero on a miss. Not in CI (about seven minutes a map at 100 seeds).
- **How a class is fielded.** The captain is raised on his own class's growths to the level the class certifies at (3 for the base classes, 10 for the forms), given the ranks it asks for, certified through `Certifications.Certify(..., captain: true)` (so a refusal throws), with each step's mastery earned, since the bar weighs a class as the player has it once its twelve combats are in. He carries one starting weapon of each type the class adds and he lacks (lance, Cinder, bow, axe), so a Ranger has a bow to draw and a Marshal a spell.
- **Each tier on its own board.** The cast and the map's enemy level are raised by as many levels as the tier stands over the captain's (+2 for tier 1, +9 for tier 2), the curve's `party +N` proxy (0161). A level 10 captain on a level 1 board carries it alone and fails gate 4 under every form, which measures the board, not the class (seen at `the_tollgate`: 100, 100 and 98.5, all three failing gate 4).
- **Gate 4's captain verdict, read.** The captain is never benched (0026), so gate 4 cannot judge him directly. A class misses the bar when gate 4 fails under it on a board where it passes under the unpromoted captain (the class pulled the cast's weight onto the captain), or when its captain never attacks off an Escape map (the unit hidden at the back). On Escape the captain leaving is the map's ask.
- **The smoke's row.** `origin by class`: every origin by every base class (4 x 3), each captain built and played in one AI-vs-AI game on the Tollgate twice; fails on a refusal, an exception or a replay that differs.

## The first reading (100 seeds; `docs/measurements/ladder-705.txt`)

Tier 1 misses everywhere it was read. The Vanguard leads by a distance: the Tollgate 78 against the Ranger's 57 (cadet 70); the Mill 85 against the Marshal's 46 (cadet 39); Brackwater 97 against 39. Tier 2 sits at 92 to 100 on the raised boards, inside the bar on the Mill and Brackwater, 8 points wide on the Tollgate, where the Commander also loses gate 4. On Brackwater the Commander never attacks; that reads as leaving, which Escape asks, so its tier 2 passes.

## Open (slice 4)

- Tune the six to the bar. The first levers, one at a time and measured: the Vanguard's modifiers (+2 HP, +1 Str, +2 Def at level 3 on a level 3 board is the gap), then the Ranger's and the Marshal's. Content only; no rule changes.
- Harrow Weir (stopped unfinished; a 15-turn map is the slowest read), Saltmarsh, Sallow, Starting Alone, Old Mill Road and the side maps are unread.
