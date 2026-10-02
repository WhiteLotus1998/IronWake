# 0167 — Rook's Scout, the second unique class (#706, slice 2)

Date: 2026-10-02. Issue #706. The class is the Table's (Lotus's batch, item 3; rounds 216, 217; the issue body): Rook's Scout, a flier that gains +2 sight on dusk maps and `threat` numbers for sleeping groups within 6 of her, and loses Str growth and lance power to her Sky Captain. Unlocked by her quest 1. The shapes below are the Builder's and are provisional; they follow 0166.

## Decided (the Builder, provisional)

- **The class** (`scout`): `advances: skyrider`, `unique: rook`, `unlockedBy: rook_1`, `loses: damage`; flying, Mov 7, lance, the Sky Captain's certification (level 7, Lance C) and seal (1000). Modifiers Spd +3, Res +2: the Sky Captain's without its Str +1. Mastery Lookout (+1 Spd, +2 Lck).
- **High Watch** is a new effect kind, `sight` (`tiles`: 2). A unit carries its extra sight as `BattleUnit.ExtraSight`, read from its abilities when it is placed and again when a state is read from the protocol, so `Dusk.Sees` needs no content and every caller of it (strikes, counters, the planner's knowledge, the unseen glyph) reads one rule. Her side sees what she sees, as dusk sight is already shared. Nothing in daylight. The dusk line names it: `dusk: sight 1; it gets no darker; Rook sees 3; ...` (a rule on screen, round 42).
- **Headcount** is a new effect kind, `headcount` (`radius`: 6). `Queries.SleepingThreats` marks a group with a member within the radius of a player unit holding it (that unit read on the asked tile when the threat is hers) as `CountedBy` that unit and prices each member's strike on the board with the group woken, exactly as `Threats` would price it awake (a test compares the two). `threat` prints the group as `The y group is asleep; Rook counts it, if woken:`, a row per member, and `If woken and all land: N against H hp (asleep, not in the total)`. The protocol adds `countedBy`, `priced` and `ifWokenAllLand` to that group only; `ifAllLand` never counts it. Every other sleeping group stays named and unpriced (issue 248).
- **The losses.** `SidegradeMeasure.Damage` is Str (Mag for a spell) plus the Mt of the strongest weapon the unit may strike with in the class, at its level. The Scout loses it by 1 on every lance. A unique class alone may name `growthModifiers`, added to its base's (every other advanced form still grows as its base, 0157); the Scout's is Str -10, so her Str growth is 10 under the Sky Captain's. Both held by tests.
- **The unlock before the quest exists.** Rook's quest 1 waits on the branch (#633) and the story (#656). `unlockedBy` may now name an id of the member's shape (`rook_1`) the campaign does not carry; a quest the campaign does carry must still be that member's, and an id of another member's shape is refused. The door is shut until a campaign wins `rook_1`, so nothing ships the Scout into play yet.
- **Art.** `make_art.py` draws the Scout as the Skyrider (its base) until it has a shape of its own; tokens and lance clips are generated, the contact sheets and the sheet for Lotus rebuilt.

## Not built here

- No hand play: no campaign reaches the Scout until her quest is authored. The first chair that takes her journals the choice against the Sky Captain.
- The Sim's player never certifies, so gate 4's ablation of a unique class against its standard form is still unread (as 0166).
- The Godot client draws neither the extra sight's line nor a counted group's rows (the panel reads `text`).

## Kill / revisit

If a chair takes the Scout and never reads a counted group's numbers before deciding whether to wake it, Headcount is scenery: the first lever is the radius (6 to 8), then printing it on the board, not more damage.
