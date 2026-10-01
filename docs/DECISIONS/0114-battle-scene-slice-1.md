# 0114 — The battle scene, slice 1: the scene is the strike beat, the pacing rule, the level-up card

Date: 2026-10-01. Issue #535 (Lotus's batch, item 4; Design Table rounds 143, 144, 184, 185). This is the Builder's implementation of an agreed Table issue, and Chat can argue the details on the PR.

## What changed

- **The scene is the strike beat, not a second clock.** A combat the setting picks carries a `BattleScene` on its `Beat` (`Beat.Scene`). `Rhythm.Length` and `Rhythm.PopTimes` read the scene's timeline when there is one. The board's HP under the overlay, the lethal hold, the death beat that follows and the sound cues therefore keep time with the clips without any new wiring, and Space or C ends a scene on the state the protocol already has.
- **The timeline** (`BattleScene.Of`) runs at the art's 12 frames a second. It opens on the attacker's `advance`. Each strike plays the striker's `strike`, `strike_crit` or `miss_recover`, and its contact frame is the clip's contact from `ArtSpec.Clips`. Hit-stop holds the contact 0.1 s on a hit and 0.22 s on a crit, and there is a 0.12 s breath between strikes. The struck unit plays `hit_react`, `dodge` or `fall` from contact, and the scene ends with 0.35 s of stillness. A two-strike combat lasts about 2.5 s.
- **Clip names** come from issue 532's rows. A boss uses its own `boss_<template>_<weapon>` first and its class's sheet after that. Everyone else uses `<class>_<kind>` for the weapon they swung. A test holds every name a scene asks for to the spec's list.
- **Effects:** a dodge raises `dust`. A hit raises `hit_spark`, plus `slash_arc` for a sword, axe or lance, or the spell's own `spell_<weapon>` instead of either. A crit adds `crit_flash`. Shake is the damage over the struck unit's max HP, times 1.5 on a crit, capped at 1.
- **Pacing (`Scenes.Plays`)** has three settings. Key moments is the default: a player's strike, a crit, a kill, a boss's strike, or any combat that levels a unit gets a scene. All gives every combat a scene, and map only gives none. B cycles the setting. A `SCENES` chip at the right end of the top bar names it, because the footer is full at 1280 (0113 measured it at 1216 px).
- **Sides.** The player stands on the left, which is the sheets' own facing, and the enemy is on the right, mirrored. The tint follows 0105: player figures are amber and enemy figures are slate lifted 45 percent toward bone. Pure slate (L 23) disappears on the panel.
- **The level-up card** (`LevelUpCard`) follows any combat that levels a unit, whatever the setting. It names each stat that rose with its amount. Two levels from one combat appear once, with their gains summed. A level where nothing rose prints the unit's one dry line from `LevelUpCard.FlatLines`, one per cast member, so the card is never blank. It holds 1.8 s.

## Left for slice 2

- The procedural backdrop: parallax layers, fog, embers, dusk darkening by turn, and lamps on a wake. Slice 1 bands each half in its tile's colour and detail.
- Heal level-ups: a heal's beat carries no card yet.
- The `for-lotus` issue with a capture of every clip. This sandbox has no Godot binary to capture with.
- Moving the dry lines into `cast.json` if the Table wants all content text in content files.
