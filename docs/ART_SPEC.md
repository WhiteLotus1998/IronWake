# ART_SPEC — the brief an outside artist works from

Issue 532, from Lotus's batch (#503, item 4). This is what an artist needs before drawing anything: the look's rules, the token set, the battle-scene clips, the optional poses, and how to name and deliver the files. `docs/LOOK.md` is the source these rules come from; this file restates them for someone who has not read it. The name list at the end is the contract: `Ironwake.Client.ArtSpec` derives the same list from the game's content, and `ArtSpecTests` fails if the two ever differ, so a new class, weapon, boss or cast member shows up here as missing rows.

## The look, in brief

- **Cold world, warm player.** The world is peat green, salt grey, slate water and cold stone. The player's side is lamplight amber (`#E8A33D` on a deeper `#9A6420`), the only warm colour on the board. The enemy lives inside the cold: slate (`#2F3742`) with bone (`#E6E0D0`) for its silhouette, rim and marks. Threat is carried by the blade in a silhouette, never by a hot red.
- **Fire is the one warm world colour**, ember `#943C0C`, drawn as a hatch over the ground and never as a flat fill, so a burning tile never reads as ours.
- **Silhouettes over letters.** Each class is read from its weapon: cadet a short upright sword, pikeman a long diagonal shaft with a leaf head, bowman a bow with its arrow, reaver a haft with a single crescent head, adept a flame, outrider the pike with a pennant (`docs/look/silhouettes.png` draws these). Chaplain, skyrider and bulwark have no silhouette yet; the artist proposes one, a weapon or the tool of the trade, and the partners agree it before tokens are drawn. The Tollgate's enemies vary theirs by weapon: the toll warden's pike carries a hooked crossbar, and the bandit leader has a double-bitted head. A token must read at 32 px in one colour.
- **Tone: low fantasy on a frontier.** Steel, keeps, tollgates and a fraying armistice. People are young adults in worn kit, not heroes in polished plate. Magic reads as a craft: ink, breath, a worked hand. It is never a spectacle, so no glowing runes, no sparkles and no auras. It is grounded and a little dry, with warmth underneath.
- **Flat and vector-like.** Shapes are flat with hard edges and at most two values per colour (a base and a shade), with light from the north. No gradients on tokens, no outlines around terrain, no texture noise.

## Hard rule: nothing borrowed

Every design is original. No character, costume, weapon, crest, creature or pose may be recognisable from Fire Emblem, Advance Wars, Final Fantasy Tactics, Triangle Strategy, Tactics Ogre, Into the Breach, Wargroove, Darkest Dungeon, Warhammer, or any other franchise. That includes near-misses: a blue-haired lord with a tiara, a pegasus knight in white and pink, a winged-helm paladin on a white horse, a red-and-green cavalier pair, a hooded tactician in a long coat. If a reviewer can name the source, the piece is redrawn. Reference photographs of real kit (medieval and early-modern European arms, working horses, frontier forts) are welcome.

## Tokens

- One token per class per side, plus the captain's: **48 x 48 px** frames at the game's 1280x720 (delivered at 2x, 96 x 96, and downscaled by the client).
- The disc is **32 px** (two thirds of the tile), centred 3 px above the frame's centre. The bottom **8 px** are left clear for the HP bar, which the client draws.
- Player tokens: the silhouette in ink (`#15181D`) on the amber disc. Enemy tokens: the silhouette in bone on the slate disc. The client draws the 1 px bone hairline rim, the boss's dashed ring, and the captain's crown itself; do not paint them into the art.
- Facing: the silhouette is drawn upright and never mirrored, because the side is carried by value, not by direction.

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

Classes and the weapon kinds each needs, from `content/classes.json`: adept (reason), bowman (bow), bulwark (lance, axe), cadet (sword, lance, axe), chaplain (faith), outrider (lance, sword), pikeman (lance), reaver (axe, gauntlet), skyrider (lance). Named bosses from the shipped maps, one set per weapon each carries: the Bandit Leader (the Tollgate; steel axe and the Toll Axe, a two-weapon boss whose chosen axe must read at a glance), the Grange Reeve (Sallow Grange; steel lance and the Toll Spear), and the Weir Foreman (Harrow Weir; the Toll Axe).

## Optional: level-up poses and portraits

Both are out of the showcase and listed so the names are reserved. There is one **level-up pose** per cast member (256 x 256, one frame, three-quarter view, the pose that says who they are) and one **portrait** per cast member (192 x 192 bust, facing right, a plain panel-colour `#1E232A` background). The cast, in roster order: the captain Alder Fenn, Wren, Teodor, Ottilie, Pell, Dunstan, Maud, Ansgar, Rook, Keziah, Brannock. Their lines are in `content/units/cast.json`.

## Naming, palette and delivery

- **Files:** `<name>_<nn>.png` per frame, two-digit frame numbers from `00`, where `<name>` is a row of the list below (for example `cadet_sword_strike_05.png`, the contact frame). A token or single pose is `<name>.png`. Alternatively, deliver a sprite sheet `<name>.png` in one row, left to right, with a sidecar `<name>.json`: `{ "frame": [256, 256], "frames": 8, "pivot": [128, 232], "contact": 5 }`.
- **Format:** PNG, RGBA, sRGB, no premultiplied alpha, delivered at 2x.
- **Palette:** the values in `docs/LOOK.md`'s palette table, plus at most one shade and one highlight per value. The player keeps chroma (the amber); every enemy and world colour stays grey-leaning. `LookPaletteTests` checks the palette by arithmetic, and a new colour goes into LOOK.md before it goes into art.
- **Licence and ownership:** the delivery carries a written grant letting the project use, modify and redistribute the art in the game and its promotional material, with the artist's credit line. Each file set is listed in `LICENSES` at the repository root with its source and terms, as the fonts are.
- **Placeholders:** until a file is delivered, the client draws its own vector placeholder under the same name, so real art drops in without code changes. The map tokens have placeholders now (`Main.Look.cs`). The clips get theirs with the battle scene (#535).

## The name list

Every asset, one per line, in order: tokens, class clips, boss clips, then the optional level-up poses and portraits. A clip row names a sheet, whose frames follow the clip table above.

```names
token_adept_player
token_adept_enemy
token_bowman_player
token_bowman_enemy
token_bulwark_player
token_bulwark_enemy
token_cadet_player
token_cadet_enemy
token_chaplain_player
token_chaplain_enemy
token_outrider_player
token_outrider_enemy
token_pikeman_player
token_pikeman_enemy
token_reaver_player
token_reaver_enemy
token_skyrider_player
token_skyrider_enemy
token_captain_player
adept_reason_idle
adept_reason_advance
adept_reason_strike
adept_reason_strike_crit
adept_reason_miss_recover
adept_reason_dodge
adept_reason_hit_react
adept_reason_fall
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
chaplain_faith_idle
chaplain_faith_advance
chaplain_faith_strike
chaplain_faith_strike_crit
chaplain_faith_miss_recover
chaplain_faith_dodge
chaplain_faith_hit_react
chaplain_faith_fall
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
pikeman_lance_idle
pikeman_lance_advance
pikeman_lance_strike
pikeman_lance_strike_crit
pikeman_lance_miss_recover
pikeman_lance_dodge
pikeman_lance_hit_react
pikeman_lance_fall
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
skyrider_lance_idle
skyrider_lance_advance
skyrider_lance_strike
skyrider_lance_strike_crit
skyrider_lance_miss_recover
skyrider_lance_dodge
skyrider_lance_hit_react
skyrider_lance_fall
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
boss_weir_foreman_toll_axe_idle
boss_weir_foreman_toll_axe_advance
boss_weir_foreman_toll_axe_strike
boss_weir_foreman_toll_axe_strike_crit
boss_weir_foreman_toll_axe_miss_recover
boss_weir_foreman_toll_axe_dodge
boss_weir_foreman_toll_axe_hit_react
boss_weir_foreman_toll_axe_fall
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
