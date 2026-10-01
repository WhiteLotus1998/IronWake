# 0115 — The battle scene, slice 2: the procedural backdrop, heal level-ups, the clip sheets for Lotus

Date: 2026-10-01. Issue #535 (Lotus's batch, item 4; Design Table rounds 143, 144, 184, 185). This is the Builder's implementation of an agreed Table issue, and Chat can argue the details on the PR.

## What changed

- **The backdrop is data first** (`SceneBackdrop`, read by `BattleScene.Of` from the board the combat was fought on). It adds nothing the state does not say. Each half has its ground, its fog, its embers and its lamps; the dusk lies over both.
- **Parallax.** Three layers per half drift at 6, 14 and 30 px a second: a far ridge in the ground's colour darkened 35 percent, the tile's own detail, and a near band with tufts. Each half keeps its own ground, so the seam between them is where the two sides' tiles meet.
- **Fog** lies on forest (0.22) and water (0.3) and nowhere else, plus 0.3 of the dusk.
- **Dusk** reads the map's `dusk:` header and the turn: 0.15 on a dusk map's first turn, rising to 0.55 on the turn sight reaches one tile, 0 in daylight. It dims the backdrop only; the figures, numbers and bars are drawn over it.
- **Embers** rise over a half when a burning tile (`%`) lies within 2 of the combatant, four per tile, at most twelve, in fire's colour lifted toward bone: sparks, never a fill, so fire stays the one warm world colour (0101's rule). Their places are a hash of the unit's id, so the same combat raises the same embers on every run and every Recall.
- **Lamps** hang over an enemy's half when its group's lamps are lit (DESIGN 13.7, `BattleState.IsLit`). They are bone, the enemy's light, not amber.
- **Heal level-ups.** A command with no combat whose events level a unit (a heal's EXP) gets a beat that is the card alone, on its first `UnitHealed`, held `LevelUpCard.Hold`.
- **`--scenes all|map`** sets the scene setting from the command line, before any script, for strips and captures.
- **The clip sheets** (`docs/look/clip_sheets.py`, `docs/look/clips/`): one contact sheet per clip set, a row per clip, the contact frame boxed. These are the `for-lotus` issue's captures; they come from the generated sheets, not from Godot, so they show the clips as drawn, and the two scene frames under `docs/screenshots/*-scene-*.png` show them in play.

## Left open

- Lotus's notes on the clips, from the `for-lotus` issue, become the next slice's brief.
- Moving the dry lines into `cast.json`, if the Table wants all content text in content files (0114).
