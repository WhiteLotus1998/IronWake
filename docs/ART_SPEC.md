# ART_SPEC — the brief an outside artist works from

Issue 532, from Lotus's batch (#503, item 4). This is what an artist needs before drawing anything: the look's rules, the token set, the battle-scene clips, the optional poses, and how to name and deliver the files. `docs/LOOK.md` is the source these rules come from; this file restates them for someone who has not read it. The name list at the end is the contract: `Ironwake.Client.ArtSpec` derives the same list from the game's content, and `ArtSpecTests` fails if the two ever differ, so a new class, weapon, boss or cast member shows up here as missing rows.

## The look, in brief

- **Cold world, warm player.** The world is peat green, salt grey, slate water and cold stone. The player's side is lamplight amber (`#E8A33D` on a deeper `#9A6420`), the only warm colour on the board. The enemy lives inside the cold: slate (`#2F3742`) with bone (`#E6E0D0`) for its silhouette, rim and marks. Threat is carried by the blade in a silhouette, never by a hot red.
- **Fire is the one warm world colour**, ember `#943C0C`, drawn as a hatch over the ground and never as a flat fill, so a burning tile never reads as ours.
- **Silhouettes over letters.** Each class is read from its weapon: cadet a short upright sword, pikeman a long diagonal shaft with a leaf head, bowman a bow with its arrow, reaver a haft with a single crescent head, adept a flame, outrider a lance couched flatter than the pike, its pennant under the head and a horseshoe at its foot (round 170; `docs/look/silhouettes.png` draws these). Chaplain, skyrider and bulwark have no silhouette yet; the artist proposes one, a weapon or the tool of the trade, and the partners agree it before tokens are drawn. The Tollgate's enemies vary theirs by weapon: the toll warden's pike carries a hooked crossbar, and the bandit leader has a double-bitted head. These tells have token rows of their own, `token_<class>_enemy_<tell>`, one per tell a shipped enemy carries (`ArtSpec.TokenVariants`, the client's rule): `hooked` for a pikeman whose weapon reaches 2 (the toll warden, the Grange Reeve), `double` for a reaver placed as a boss (the Bandit Leader, the Weir Foreman). A missing variant falls back to the class's token. A token must read at 32 px in one colour.
- **Tone: low fantasy on a frontier.** Steel, keeps, tollgates and a fraying armistice. People are young adults in worn kit, not heroes in polished plate. Magic reads as a craft: ink, breath, a worked hand. It is never a spectacle, so no glowing runes, no sparkles and no auras. It is grounded and a little dry, with warmth underneath.
- **Flat and vector-like.** Shapes are flat with hard edges and at most two values per colour (a base and a shade), with light from the north. No gradients on tokens, no outlines around terrain, no texture noise.

## Hard rule: nothing borrowed

Every design is original. No character, costume, weapon, crest, creature or pose may be recognisable from Fire Emblem, Advance Wars, Final Fantasy Tactics, Triangle Strategy, Tactics Ogre, Into the Breach, Wargroove, Darkest Dungeon, Warhammer, or any other franchise. That includes near-misses: a blue-haired lord with a tiara, a pegasus knight in white and pink, a winged-helm paladin on a white horse, a red-and-green cavalier pair, a hooded tactician in a long coat. If a reviewer can name the source, the piece is redrawn. Reference photographs of real kit (medieval and early-modern European arms, working horses, frontier forts) are welcome.

## Tokens

- One token per class per side, plus the captain's: **48 x 48 px** frames at the game's 1280x720 (delivered at 2x, 96 x 96, and downscaled by the client).
- The disc is **32 px** (two thirds of the tile), centred 3 px above the frame's centre. The bottom **8 px** are left clear for the HP bar, which the client draws.
- Player tokens: the silhouette in ink (`#15181D`) on the amber disc. Enemy tokens: the silhouette in bone on the slate disc. The client draws the 1 px bone hairline rim, the boss's dashed ring, and the captain's crown itself; do not paint them into the art.
- Facing: the silhouette is drawn upright and never mirrored, because the side is carried by value, not by direction.

## Tiles

- One tile per terrain in `content/terrain.json`: **48 x 48 px**, delivered at 2x (96 x 96), a flat square of the terrain's LOOK.md colour with its detail laid over it: ink at 25 to 35 percent (forest's three pines, hill's swell, mountain's ridge, fort's battlement, the gate's arch), frost ripples on water, iron coursing on wall. No outline, no grid, no shadow: the client draws the hairline grid and the north light's shadows, which depend on the neighbours.
- Fire (`tile_fire`) is never a fill: an ember hatch on clear ground, which the client lays over whatever burns.

## Battle-scene clips

One combatant per sheet, facing **right**. The client mirrors the sheet for the left side. The frame is **256 x 256 px**, the pivot is the feet's centre at (128, 232), and the art runs at 12 frames a second. One sheet is delivered per (class, weapon kind, clip), with an extra set for each named boss by weapon. The contact frame is where the blow lands: hit-stop holds there and the damage number fires. Clip lengths are a guide; the contact frame's index is the contract.

| clip | frames | contact | what it shows |
|---|---|---|---|
| `idle` | 8 | none | breathing on guard, loops |
| `advance` | 6 | none | two steps in, ends in striking range |
| `strike` | 8 | 5 | the ordinary blow, ends back on guard |
| `strike_crit` | 12 | 8 | a wind-up held a beat longer, a heavier blow |
| `miss_recover` | 8 | 5 | the blow that finds nothing, a stumble and reset |
| `dodge` | 6 | none | a step or lean out of the line |
| `hit_react` | 4 | 1 | taking the blow |
| `fall` | 10 | none | down and still; the last frame holds |

Classes and the weapon kinds each needs, from `content/classes.json`: adept (reason), bowman (bow), bulwark (lance, axe), cadet (sword, lance, axe), chaplain (faith), outrider (lance, sword), pikeman (lance), reaver (axe, gauntlet), sergeant (lance, sword; issue 691, a hidden class earned on the Postern, the pike crossed with a short sword), skyrider (lance). Named bosses from the shipped maps, one set per weapon each carries: the Bandit Leader (the Tollgate; steel axe and the Toll Axe, a two-weapon boss whose chosen axe must read at a glance), the Grange Reeve (Sallow Grange; steel lance and the Toll Spear), and the Weir Foreman (Harrow Weir; the Toll Axe).

## Effects

Laid over the battle scene, not part of a combatant: one-row sheets of **256 x 256 px** frames at 12 frames a second, delivered as a clip is, with a sidecar whose `pivot` is **(128, 128)**, the point the scene lays the effect on (the struck body's centre; for dust, the ground under the feet), and whose `contact` is `null`. An effect is not per side, so it is drawn in LOOK.md's own values and never tinted: white (`mark.struck`) for sparks and the crit flash, text (`ui.text`) for the slash arc and Radiance, frost (`mark.reach`) for Gust, Bolt and the heal, salt grey (`terrain.road`) for dust and smoke, and ember (`terrain.fire`) only for Cinder and the embers, since fire is the one warm thing. The tone rule holds here too: a heal is a few motes rising, not a halo; Radiance is a hard-edged fan of rays, not a glow.

| effect | frames | what it shows |
|---|---|---|
| `hit_spark` | 5 | a blow that lands: short rays out from the point |
| `slash_arc` | 5 | the edge's path, a crescent that sweeps and thins |
| `crit_flash` | 6 | a four-point flash and a ring thrown outward |
| `heal` | 10 | motes rising through the body |
| `dust` | 6 | puffs thrown sideways from the feet |
| `embers` | 12 | sparks rising from a burning tile, loops |
| `spell_<weapon>` | 8 | one burst per Reason or Faith weapon that strikes (Salve and Beacon use `heal`) |

## Optional: level-up poses and portraits

Both are out of the showcase and listed so the names are reserved. There is one **level-up pose** per cast member (256 x 256, one frame, three-quarter view, the pose that says who they are) and one **portrait** per cast member (192 x 192 bust, facing right, a plain panel-colour `#1E232A` background). The cast, in roster order: the captain Alder Fenn, Wren, Teodor, Ottilie, Pell, Dunstan, Maud, Ansgar, Rook, Keziah, Brannock. Their lines are in `content/units/cast.json`.

## Naming, palette and delivery

- **Files:** `<name>_<nn>.png` per frame, two-digit frame numbers from `00`, where `<name>` is a row of the list below (for example `cadet_sword_strike_05.png`, the contact frame). A token or single pose is `<name>.png`. Alternatively, deliver a sprite sheet `<name>.png` in one row, left to right, with a sidecar `<name>.json`: `{ "frame": [256, 256], "frames": 8, "pivot": [128, 232], "contact": 5 }`.
- **Format:** PNG, RGBA, sRGB, no premultiplied alpha, delivered at 2x.
- **Palette:** the values in `docs/LOOK.md`'s palette table, plus at most one shade and one highlight per value. The player keeps chroma (the amber); every enemy and world colour stays grey-leaning. `LookPaletteTests` checks the palette by arithmetic, and a new colour goes into LOOK.md before it goes into art.
- **Licence and ownership:** the delivery carries a written grant letting the project use, modify and redistribute the art in the game and its promotional material, with the artist's credit line. Each file set is listed in `LICENSES` at the repository root with its source and terms, as the fonts are.
- **Generated set (issue 564):** `docs/art/make_art.py` draws every token but the captain's (Lotus models him), every tile, every class and boss clip, and every effect in the list below from coded shapes. The output is hard-edged and the same bytes every run. Each file goes to `src/Ironwake.Godot/assets/art/<name>.png`, and each clip and effect also gets its sidecar `<name>.json`. The script lists what it owns in `generated.txt` there, and `ArtGeneratedTests` holds those files to the look. The clips are drawn in three neutral greys, and the client tints each sheet with the side's colour when it plays it (0105). A delivered file replaces a generated one by name and comes off the list. `docs/art/contact-tokens.png` shows the tokens and tiles at 2x and 1x, `docs/art/contact-clips.png` shows one frame of every clip, and `docs/art/contact-effects.png` every frame of every effect.
- **Loading and placeholders:** the client reads each token and tile from `src/Ironwake.Godot/assets/art/<name>.png` (`Main.Art.cs`, issue 564), scaled once to the board's tile, and draws its own vector placeholder (`Main.Look.cs`) only for a name with no file, so real art drops in without code changes. A unit tries `ArtSpec.TokenFiles` in order: the captain only `token_captain_player`, an enemy with a tell its variant then its class token. The client still draws the rim, the boss's ring, the crown, the HP bar, the grid and the shadows over the files, and lays a tile's detail again over the threat hatch. The clips and effects load with the battle scene (#535).

## The name list

Every asset, one per line, in order: tokens, tiles, class clips, boss clips, effects, then the optional level-up poses and portraits. A clip row names a sheet, whose frames follow the clip table above.

```names
token_adept_player
token_adept_enemy
token_berserker_player
token_berserker_enemy
token_bowman_player
token_bowman_enemy
token_bulwark_player
token_bulwark_enemy
token_cadet_player
token_cadet_enemy
token_champion_player
token_champion_enemy
token_chaplain_player
token_chaplain_enemy
token_commander_player
token_commander_enemy
token_fieldsurgeon_player
token_fieldsurgeon_enemy
token_halberdier_player
token_halberdier_enemy
token_lancer_player
token_lancer_enemy
token_marksman_player
token_marksman_enemy
token_marshal_player
token_marshal_enemy
token_outrider_player
token_outrider_enemy
token_pathfinder_player
token_pathfinder_enemy
token_pikeman_player
token_pikeman_enemy
token_ranger_player
token_ranger_enemy
token_reaver_player
token_reaver_enemy
token_scholar_player
token_scholar_enemy
token_scout_player
token_scout_enemy
token_sentinel_player
token_sentinel_enemy
token_sergeant_player
token_sergeant_enemy
token_skycaptain_player
token_skycaptain_enemy
token_skyrider_player
token_skyrider_enemy
token_vanguard_player
token_vanguard_enemy
token_warden_player
token_warden_enemy
token_captain_player
token_pikeman_enemy_hooked
token_reaver_enemy_double
tile_fire
tile_forest
tile_fort
tile_hill
tile_mountain
tile_plain
tile_planks
tile_road
tile_split_planks
tile_throne
tile_wall
tile_water
adept_reason_idle
adept_reason_advance
adept_reason_strike
adept_reason_strike_crit
adept_reason_miss_recover
adept_reason_dodge
adept_reason_hit_react
adept_reason_fall
berserker_axe_idle
berserker_axe_advance
berserker_axe_strike
berserker_axe_strike_crit
berserker_axe_miss_recover
berserker_axe_dodge
berserker_axe_hit_react
berserker_axe_fall
berserker_gauntlet_idle
berserker_gauntlet_advance
berserker_gauntlet_strike
berserker_gauntlet_strike_crit
berserker_gauntlet_miss_recover
berserker_gauntlet_dodge
berserker_gauntlet_hit_react
berserker_gauntlet_fall
bowman_bow_idle
bowman_bow_advance
bowman_bow_strike
bowman_bow_strike_crit
bowman_bow_miss_recover
bowman_bow_dodge
bowman_bow_hit_react
bowman_bow_fall
bulwark_lance_idle
bulwark_lance_advance
bulwark_lance_strike
bulwark_lance_strike_crit
bulwark_lance_miss_recover
bulwark_lance_dodge
bulwark_lance_hit_react
bulwark_lance_fall
bulwark_axe_idle
bulwark_axe_advance
bulwark_axe_strike
bulwark_axe_strike_crit
bulwark_axe_miss_recover
bulwark_axe_dodge
bulwark_axe_hit_react
bulwark_axe_fall
cadet_sword_idle
cadet_sword_advance
cadet_sword_strike
cadet_sword_strike_crit
cadet_sword_miss_recover
cadet_sword_dodge
cadet_sword_hit_react
cadet_sword_fall
cadet_lance_idle
cadet_lance_advance
cadet_lance_strike
cadet_lance_strike_crit
cadet_lance_miss_recover
cadet_lance_dodge
cadet_lance_hit_react
cadet_lance_fall
cadet_axe_idle
cadet_axe_advance
cadet_axe_strike
cadet_axe_strike_crit
cadet_axe_miss_recover
cadet_axe_dodge
cadet_axe_hit_react
cadet_axe_fall
champion_sword_idle
champion_sword_advance
champion_sword_strike
champion_sword_strike_crit
champion_sword_miss_recover
champion_sword_dodge
champion_sword_hit_react
champion_sword_fall
champion_lance_idle
champion_lance_advance
champion_lance_strike
champion_lance_strike_crit
champion_lance_miss_recover
champion_lance_dodge
champion_lance_hit_react
champion_lance_fall
champion_axe_idle
champion_axe_advance
champion_axe_strike
champion_axe_strike_crit
champion_axe_miss_recover
champion_axe_dodge
champion_axe_hit_react
champion_axe_fall
chaplain_faith_idle
chaplain_faith_advance
chaplain_faith_strike
chaplain_faith_strike_crit
chaplain_faith_miss_recover
chaplain_faith_dodge
chaplain_faith_hit_react
chaplain_faith_fall
commander_sword_idle
commander_sword_advance
commander_sword_strike
commander_sword_strike_crit
commander_sword_miss_recover
commander_sword_dodge
commander_sword_hit_react
commander_sword_fall
commander_reason_idle
commander_reason_advance
commander_reason_strike
commander_reason_strike_crit
commander_reason_miss_recover
commander_reason_dodge
commander_reason_hit_react
commander_reason_fall
commander_lance_idle
commander_lance_advance
commander_lance_strike
commander_lance_strike_crit
commander_lance_miss_recover
commander_lance_dodge
commander_lance_hit_react
commander_lance_fall
fieldsurgeon_faith_idle
fieldsurgeon_faith_advance
fieldsurgeon_faith_strike
fieldsurgeon_faith_strike_crit
fieldsurgeon_faith_miss_recover
fieldsurgeon_faith_dodge
fieldsurgeon_faith_hit_react
fieldsurgeon_faith_fall
halberdier_lance_idle
halberdier_lance_advance
halberdier_lance_strike
halberdier_lance_strike_crit
halberdier_lance_miss_recover
halberdier_lance_dodge
halberdier_lance_hit_react
halberdier_lance_fall
halberdier_axe_idle
halberdier_axe_advance
halberdier_axe_strike
halberdier_axe_strike_crit
halberdier_axe_miss_recover
halberdier_axe_dodge
halberdier_axe_hit_react
halberdier_axe_fall
lancer_lance_idle
lancer_lance_advance
lancer_lance_strike
lancer_lance_strike_crit
lancer_lance_miss_recover
lancer_lance_dodge
lancer_lance_hit_react
lancer_lance_fall
lancer_sword_idle
lancer_sword_advance
lancer_sword_strike
lancer_sword_strike_crit
lancer_sword_miss_recover
lancer_sword_dodge
lancer_sword_hit_react
lancer_sword_fall
lancer_axe_idle
lancer_axe_advance
lancer_axe_strike
lancer_axe_strike_crit
lancer_axe_miss_recover
lancer_axe_dodge
lancer_axe_hit_react
lancer_axe_fall
marksman_bow_idle
marksman_bow_advance
marksman_bow_strike
marksman_bow_strike_crit
marksman_bow_miss_recover
marksman_bow_dodge
marksman_bow_hit_react
marksman_bow_fall
marshal_sword_idle
marshal_sword_advance
marshal_sword_strike
marshal_sword_strike_crit
marshal_sword_miss_recover
marshal_sword_dodge
marshal_sword_hit_react
marshal_sword_fall
marshal_reason_idle
marshal_reason_advance
marshal_reason_strike
marshal_reason_strike_crit
marshal_reason_miss_recover
marshal_reason_dodge
marshal_reason_hit_react
marshal_reason_fall
outrider_lance_idle
outrider_lance_advance
outrider_lance_strike
outrider_lance_strike_crit
outrider_lance_miss_recover
outrider_lance_dodge
outrider_lance_hit_react
outrider_lance_fall
outrider_sword_idle
outrider_sword_advance
outrider_sword_strike
outrider_sword_strike_crit
outrider_sword_miss_recover
outrider_sword_dodge
outrider_sword_hit_react
outrider_sword_fall
pathfinder_sword_idle
pathfinder_sword_advance
pathfinder_sword_strike
pathfinder_sword_strike_crit
pathfinder_sword_miss_recover
pathfinder_sword_dodge
pathfinder_sword_hit_react
pathfinder_sword_fall
pathfinder_bow_idle
pathfinder_bow_advance
pathfinder_bow_strike
pathfinder_bow_strike_crit
pathfinder_bow_miss_recover
pathfinder_bow_dodge
pathfinder_bow_hit_react
pathfinder_bow_fall
pikeman_lance_idle
pikeman_lance_advance
pikeman_lance_strike
pikeman_lance_strike_crit
pikeman_lance_miss_recover
pikeman_lance_dodge
pikeman_lance_hit_react
pikeman_lance_fall
ranger_sword_idle
ranger_sword_advance
ranger_sword_strike
ranger_sword_strike_crit
ranger_sword_miss_recover
ranger_sword_dodge
ranger_sword_hit_react
ranger_sword_fall
ranger_bow_idle
ranger_bow_advance
ranger_bow_strike
ranger_bow_strike_crit
ranger_bow_miss_recover
ranger_bow_dodge
ranger_bow_hit_react
ranger_bow_fall
reaver_axe_idle
reaver_axe_advance
reaver_axe_strike
reaver_axe_strike_crit
reaver_axe_miss_recover
reaver_axe_dodge
reaver_axe_hit_react
reaver_axe_fall
reaver_gauntlet_idle
reaver_gauntlet_advance
reaver_gauntlet_strike
reaver_gauntlet_strike_crit
reaver_gauntlet_miss_recover
reaver_gauntlet_dodge
reaver_gauntlet_hit_react
reaver_gauntlet_fall
scholar_reason_idle
scholar_reason_advance
scholar_reason_strike
scholar_reason_strike_crit
scholar_reason_miss_recover
scholar_reason_dodge
scholar_reason_hit_react
scholar_reason_fall
scholar_faith_idle
scholar_faith_advance
scholar_faith_strike
scholar_faith_strike_crit
scholar_faith_miss_recover
scholar_faith_dodge
scholar_faith_hit_react
scholar_faith_fall
scout_lance_idle
scout_lance_advance
scout_lance_strike
scout_lance_strike_crit
scout_lance_miss_recover
scout_lance_dodge
scout_lance_hit_react
scout_lance_fall
sentinel_lance_idle
sentinel_lance_advance
sentinel_lance_strike
sentinel_lance_strike_crit
sentinel_lance_miss_recover
sentinel_lance_dodge
sentinel_lance_hit_react
sentinel_lance_fall
sentinel_axe_idle
sentinel_axe_advance
sentinel_axe_strike
sentinel_axe_strike_crit
sentinel_axe_miss_recover
sentinel_axe_dodge
sentinel_axe_hit_react
sentinel_axe_fall
sentinel_bow_idle
sentinel_bow_advance
sentinel_bow_strike
sentinel_bow_strike_crit
sentinel_bow_miss_recover
sentinel_bow_dodge
sentinel_bow_hit_react
sentinel_bow_fall
sergeant_lance_idle
sergeant_lance_advance
sergeant_lance_strike
sergeant_lance_strike_crit
sergeant_lance_miss_recover
sergeant_lance_dodge
sergeant_lance_hit_react
sergeant_lance_fall
sergeant_sword_idle
sergeant_sword_advance
sergeant_sword_strike
sergeant_sword_strike_crit
sergeant_sword_miss_recover
sergeant_sword_dodge
sergeant_sword_hit_react
sergeant_sword_fall
skycaptain_lance_idle
skycaptain_lance_advance
skycaptain_lance_strike
skycaptain_lance_strike_crit
skycaptain_lance_miss_recover
skycaptain_lance_dodge
skycaptain_lance_hit_react
skycaptain_lance_fall
skyrider_lance_idle
skyrider_lance_advance
skyrider_lance_strike
skyrider_lance_strike_crit
skyrider_lance_miss_recover
skyrider_lance_dodge
skyrider_lance_hit_react
skyrider_lance_fall
vanguard_sword_idle
vanguard_sword_advance
vanguard_sword_strike
vanguard_sword_strike_crit
vanguard_sword_miss_recover
vanguard_sword_dodge
vanguard_sword_hit_react
vanguard_sword_fall
warden_faith_idle
warden_faith_advance
warden_faith_strike
warden_faith_strike_crit
warden_faith_miss_recover
warden_faith_dodge
warden_faith_hit_react
warden_faith_fall
warden_sword_idle
warden_sword_advance
warden_sword_strike
warden_sword_strike_crit
warden_sword_miss_recover
warden_sword_dodge
warden_sword_hit_react
warden_sword_fall
boss_bandit_leader_steel_axe_idle
boss_bandit_leader_steel_axe_advance
boss_bandit_leader_steel_axe_strike
boss_bandit_leader_steel_axe_strike_crit
boss_bandit_leader_steel_axe_miss_recover
boss_bandit_leader_steel_axe_dodge
boss_bandit_leader_steel_axe_hit_react
boss_bandit_leader_steel_axe_fall
boss_bandit_leader_toll_axe_idle
boss_bandit_leader_toll_axe_advance
boss_bandit_leader_toll_axe_strike
boss_bandit_leader_toll_axe_strike_crit
boss_bandit_leader_toll_axe_miss_recover
boss_bandit_leader_toll_axe_dodge
boss_bandit_leader_toll_axe_hit_react
boss_bandit_leader_toll_axe_fall
boss_grange_reeve_steel_lance_idle
boss_grange_reeve_steel_lance_advance
boss_grange_reeve_steel_lance_strike
boss_grange_reeve_steel_lance_strike_crit
boss_grange_reeve_steel_lance_miss_recover
boss_grange_reeve_steel_lance_dodge
boss_grange_reeve_steel_lance_hit_react
boss_grange_reeve_steel_lance_fall
boss_grange_reeve_toll_spear_idle
boss_grange_reeve_toll_spear_advance
boss_grange_reeve_toll_spear_strike
boss_grange_reeve_toll_spear_strike_crit
boss_grange_reeve_toll_spear_miss_recover
boss_grange_reeve_toll_spear_dodge
boss_grange_reeve_toll_spear_hit_react
boss_grange_reeve_toll_spear_fall
boss_sworn_captain_steel_lance_idle
boss_sworn_captain_steel_lance_advance
boss_sworn_captain_steel_lance_strike
boss_sworn_captain_steel_lance_strike_crit
boss_sworn_captain_steel_lance_miss_recover
boss_sworn_captain_steel_lance_dodge
boss_sworn_captain_steel_lance_hit_react
boss_sworn_captain_steel_lance_fall
boss_weir_foreman_toll_axe_idle
boss_weir_foreman_toll_axe_advance
boss_weir_foreman_toll_axe_strike
boss_weir_foreman_toll_axe_strike_crit
boss_weir_foreman_toll_axe_miss_recover
boss_weir_foreman_toll_axe_dodge
boss_weir_foreman_toll_axe_hit_react
boss_weir_foreman_toll_axe_fall
fx_hit_spark
fx_slash_arc
fx_crit_flash
fx_heal
fx_dust
fx_embers
fx_spell_bolt
fx_spell_cinder
fx_spell_gust
fx_spell_pell_commonplace
fx_spell_radiance
levelup_captain
levelup_wren
levelup_teodor
levelup_ottilie
levelup_pell
levelup_dunstan
levelup_maud
levelup_ansgar
levelup_rook
levelup_keziah
levelup_brannock
portrait_captain
portrait_wren
portrait_teodor
portrait_ottilie
portrait_pell
portrait_dunstan
portrait_maud
portrait_ansgar
portrait_rook
portrait_keziah
portrait_brannock
```
