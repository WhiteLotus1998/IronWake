# ART_SHOPPING: the art list in tiers from $0 (issue 816)

Draft, researched 2026-10-03 in a desktop session whose network reached kenney.nl, itch.io,
opengameart.org, quaternius.com, game-icons.net and craftpix.net. patreon.com returned HTTP 403,
so Elthen's full licence could not be read (marked below). Every licence quoted here was read on
the item's own listing page through a page fetch. The quotes are short extracts of the key line.
Before anyone buys, re-open each page: prices and terms change. Nothing here was bought or
downloaded, and no account was made.

Sources for the brief: issue #816, `docs/ART_SPEC.md` (Figures, the Blender note, the name list),
`docs/LOOK.md` (palette, "the world is cold, the player is warm"), `content/classes.json`,
`content/units/cast.json` and `content/units/enemies.json`.

## What has to be drawn (from ART_SPEC's name list)

| group | count | size | notes |
|---|---|---|---|
| Tokens | 55 (26 classes x 2 sides, captain, 2 enemy tells) | 48x48, delivered 96x96 | One-colour silhouette on a disc. The client already draws these as vectors. Chaplain, skyrider and bulwark still lack a silhouette. |
| Tiles | 13 | 48x48, delivered 96x96 | Already drawn by the client from LOOK's palette. Ground is now Frost grey (north) or Cold moss (elsewhere). Sand is allowed only on a few southern outland side-quest maps. |
| Class clip sets | 47 (class x weapon kind), 8 clips each | 256x256 frames, 62 frames a set | Side view, facing right, pivot (128,232), 12 fps. 47 x 62 = 2,914 frames. |
| Boss clip sets | 6 (Bandit Leader x2, Grange Reeve x2, Sworn Captain, Weir Foreman); Hask joins when placed | same | 6 x 62 = 372 frames |
| Effects | 11 | 256x256, 84 frames total | Drawn in LOOK values, never tinted per side. No glow, no sparkle. |
| Level-up poses | 11 (cast) | 256x256, 1 frame | Optional |
| Portraits | 11 (cast), 12 with a second captain | 192x192 bust, facing right, `#1E232A` panel | Optional |

The rule for figures: blocked shading in 3 or 4 steps, an ink outline heavier on the silhouette,
a cold north key light, chroma 32 or less, one amber accent on the player side, never chibi.
Only Kinsbane and Rook's drake may use pure black or break the tile. The ART_SPEC pack filter
applies too: blocked shading, a matching outline weight, no warm world art, and one artist's
family over three better packs.

**An honest summary.** No free or cheap 2D pack family I found covers the roster: cadet,
pikeman, reaver, bowman, adept, chaplain, outrider (mounted), skyrider (flying mount), bulwark
and their promotions, at 256 px, in a grounded, blocked-shade style. The free 2D sprites that
are human-made and commercially usable are small pixel-art platformer figures, about 26 to 58 px
tall, in brighter palettes. At 256 px they would look chunky and would not match. The strongest
$0 route for the battle clips is **Lotus's Blender pipeline on CC0 human-made base meshes and
animations** (Quaternius, below), lit and toon-shaded by the ART_SPEC rig. A palette-map recolour
in code then brings anything else into LOOK's values.

---

## Tier 0: $0

The board is already done: silhouettes on discs and palette tiles, drawn by the client. Kenney's
Tiny Battle is listed only as an option, not a recommendation, as the issue asks.

Public repo column: **Yes** = the licence allows redistribution, so the files may sit in this
public repo. **Keep out** = the licence forbids redistribution, so the files stay out of git and
live locally or are fetched at build time. This is a reading of the licence text, not legal advice.

### 0-A. For Lotus's Blender pipeline (human-made 3D, rendered to sprites)

| item | author | URL | licence (key line, quoted) | price | covers | style / resolution | replaces | public repo |
|---|---|---|---|---|---|---|---|---|
| Universal Base Characters (Standard) | Quaternius | https://quaternius.itch.io/universal-base-characters | "Creative Commons Zero v1.0 Universal"; page says "No generative AI was used" | Free (Standard: 2 base models, 5 hairstyles) | Rigged humanoid bodies, Regular / Teen / Superhero proportions (Standard has 2 of the bases) | Stylised realistic-proportion 3D, humanoid rig | The bodies under every class clip set (`<class>_<weapon>_*`) and boss set, once Lotus adds kit and weapons | Yes (CC0) |
| Universal Animation Library (Standard, Pro) | Quaternius | https://quaternius.com/packs/universalanimationlibrary.html | "CC0" | Free (Standard and Pro tiers); Source tier paid, price not shown on the page I read (unverified) | 120+ animations: locomotion, combat, death | Same rig as the bases | idle, advance, dodge, hit_react, fall | Yes (CC0) |
| Universal Animation Library 2 (Standard) | Quaternius | https://quaternius.itch.io/universal-animation-library-2 | "Free to use in personal, educational and commercial projects. (CC0 License)"; "No generative AI was used" | Free (42 animations) | Sword light/regular combos split into hits and recoveries, melee, throws | Same rig | strike, strike_crit, miss_recover for sword and axe sets. Lance, bow and staff motions would need hand keying (unverified whether any exist) | Yes (CC0) |
| LowPoly RPG Characters | Quaternius | https://opengameart.org/content/lowpoly-rpg-characters | "CC0" | Free | 6 rigged, animated fantasy characters (wizard, warrior, rogue, monk, ranger, +1), .blend included | Low-poly, chunky | Kit and weapon references only. The proportions are toy-like, so not delivered figures. | Yes (CC0) |

Fit: these are human-made, CC0, one artist, with one rig across bodies and animations. That
matches "one artist's family". The look comes from Lotus's toon ramp, ink hull and light rig, not
from the meshes, so the style risk is low. Use the **Regular** proportions; Superhero is too
heroic for "young adults in worn kit" (my judgement). Nothing covers horses, the skyrider's mount
or the drake.

### 0-B. 2D figure sprites (placeholder clips only; none passes the style filter)

| item | author | URL | licence (key line, quoted) | price | covers | style / resolution | replaces | public repo |
|---|---|---|---|---|---|---|---|---|
| Medieval Warrior Pack (1, 2, 3) | LuizMelo | https://luizmelo.itch.io/medieval-warrior-pack (also `-2`, `-3`) | "This package can be used freely and commercially - CC0(creative commons zero)."; "No generative AI was used" | Name your own price, free | Sword warriors: idle, run, attacks, hit, death (pack 2 has 4 weapons) | Side-view pixel art; pack 3 figure is 26x38 px | cadet / sergeant sword clips as placeholders | Yes (CC0) |
| Hero Knight, Fantasy Warrior, Martial Hero, Medieval King Pack | LuizMelo | https://luizmelo.itch.io/hero-knight, /fantasy-warrior, /martial-hero, /medieval-king-pack | Each: "can be used freely and commercially - CC0" | Free | Sword fighters with idle, attacks, take hit, death | Side-view pixel; Fantasy Warrior 27x45 px | sword sets; the King as a commander or marshal stand-in | Yes (CC0) |
| Wizard Pack, Evil Wizard | LuizMelo | https://luizmelo.itch.io/wizard-pack, /evil-wizard | "can be used freely and commercially - CC0" | Free | Caster: idle, attacks, hit, death | Wizard canvas 190x190, figure 58x86 px | adept / scholar reason clips (player and enemy) | Yes (CC0) |
| Huntress | LuizMelo | https://luizmelo.itch.io/huntress | "can be used freely and commercially - CC0" | Free | One fighter, 3 attacks, hit, death | Frame about 162x160 (from a user comment, unverified) | a stand-in, weapon unconfirmed | Yes (CC0) |
| Monsters Creatures Fantasy | LuizMelo | https://luizmelo.itch.io/monsters-creatures-fantasy | "CC0 (Creative Commons Zero). Credits are not required" | Free | Skeleton, mushroom, goblin, flying eye | Side-view pixel | Nothing in the roster. Listed only for completeness of the family. | Yes (CC0) |
| Medieval Pixel Characters | AnGav | https://kiendko.itch.io/medieval-pixel-characters | "You can used this assets in both free and commercial projects... You can not re-sell this assets or the adapted version in anyway." | Free | Archer, spearman: idle, run, attack, death | Side-view pixel, 32x32 | pikeman lance, bowman bow placeholders | Keep out |
| Medieval Pixel Knight (and horse) | AnGav | https://kiendko.itch.io/medieval-pixel-knight | Same terms as above; "No generative AI was used" | Free | Mounted knight and horse: idle, walk, gallop, attack, death | Side-view pixel, 48x48 | outrider lance placeholder (the only mounted figure I found free) | Keep out |

Fit verdict: **LuizMelo is the only CC0 2D family large enough to matter, and it does not fit.**
It is a platformer family: side view, which is right, but the figures are small, sized
inconsistently from pack to pack (26x38 up to 58x86), and brightly coloured. It has no lance, no
mount and no flyer. As a step up from the generated grey clips it would need integer upscaling
and a palette-map recolour, and it would still read as "pixel platformer", not "stylized and
detailed". I would not ship it. It is useful only to test the clip loader with real
multi-frame art.

### 0-C. Silhouettes, effects, UI

| item | author | URL | licence (key line, quoted) | price | covers | style | replaces | public repo |
|---|---|---|---|---|---|---|---|---|
| game-icons.net | Lorc, Delapouite and 50+ others | https://game-icons.net/about.html | "Creative Commons 3.0 BY"; "A mention like 'Icons made by {author}. Available on https://game-icons.net' is fine." | Free | 4,500+ one-colour weapon, tool and creature icons (SVG) | Flat single-colour silhouettes, which is exactly the token rule "reads at 32 px in one colour" | Starting points for the missing silhouettes: `token_chaplain_*`, `token_skyrider_*`, `token_bulwark_*`, and promotions without a tell yet | Yes, with credit (CC-BY) |
| Particle Pack | Kenney | https://kenney.nl/assets/particle-pack | "Creative Commons CC0" | Free | 80 particle sprites, 512x512 | White and greyscale shapes, so they can be tinted at load to LOOK values; soft-glow sprites break the no-glow rule, so pick hard-edged ones (unverified which) | `fx_hit_spark`, `fx_crit_flash`, `fx_dust`, `fx_embers` | Yes (CC0) |
| UI Pack: RPG Expansion | Kenney | https://kenney.nl/assets/ui-pack-rpg-expansion | "Creative Commons CC0" | Free | 85 panels, buttons, bars | Clean flat UI | Nothing needed; the client draws the UI. Option only. | Yes (CC0) |
| Tiny Battle | Kenney | https://kenney.nl/assets/tiny-battle | "Creative Commons CC0" | Free | 190 tiles and units | 16x16 pixel | An option for tokens and tiles, not a recommendation; the client's board look is ours | Yes (CC0) |
| Medieval RTS | Kenney | https://kenney.nl/assets/medieval-rts | "Creative Commons CC0" | Free | 120 buildings and map pieces | Flat 2D | Map props, an option only | Yes (CC0) |

Kenney's support page: "all game assets on the asset pages are public domain licensed (CC0)...
Attribution is not required" (https://kenney.nl/support).

### 0-D. Portraits (placeholders for `portrait_*`)

| item | author | URL | licence (key line, quoted) | price | covers | style / resolution | fit | public repo |
|---|---|---|---|---|---|---|---|---|
| 30 Painted Portraits | farrer (from Diego Velazquez) | https://opengameart.org/node/73098 | "CC0" | Free | 30 portraits | 256x256, one painter's oil portraits | **Best mood fit at $0.** Grounded, dark and serious, and one painter keeps them consistent. They are 17th-century court faces, so recruits in worn kit are a stretch. | Yes (CC0) |
| public domain portraits | qubodup | https://opengameart.org/node/3714 | "CC0"; the download includes a `license-all.zip` with source information | Free | 80 or more portraits from public-domain paintings (Goya, Velazquez, Bouguereau and others) | 200x200 or larger, mixed painters | More choice, less consistency | Yes (CC0) |
| 200 Free Lorestrome Portraits | Hyptosis | https://opengameart.org/node/73514 | "CC0"; "Attribution to Hyptosis is optional" | Free | 200 male and female faces | Drawn portraits; the page says they are "random results" from an unfinished "portrait gen tool" the artist built. My reading is a parts-combiner of hand-drawn layers, not generative AI, but the page does not say how it worked: **unverified, check before use.** | Varied, consistent within the set | Yes (CC0) |
| RPG Fantasy icons portraits | Pan dluga ryba | https://opengameart.org/content/rpg-fantasy-icons-portraits-monsters-heroes-vampires | "CC0" | Free (sample) | 27 portraits and icons | 64x64 and 128x128 JPG, too small for 192 | Weak | Yes (CC0) |
| Fantasy Librarian Portrait | pennomi | https://opengameart.org/content/fantasy-librarian-portrait | "CC0"; "painted in the GIMP using a Wacom Tablet" | Free | 1 portrait | 1000x1500 painted | One-off only | Yes (CC0) |
| Flare Portrait Packs | Justin Nichol | https://opengameart.org/content/flare-portrait-pack-number-one (and packs two to five) | "CC-BY-SA 3.0, GPL 3.0, GPL 2.0" | Free | 6 heroes per pack, painted | High-resolution painted fantasy | Good mood. **Share-alike**: edited versions must stay CC-BY-SA, and the credit is due. Not CC0 or CC-BY, so it falls outside the brief's preference. | Yes (SA terms travel with the files) |

---

## Tier 1: under about $100 total

These are paid items that clearly beat the free ones where it shows: the Blender source rigs, and
portraits drawn as one family.

| item | author | URL | licence (key line, quoted) | price (listing) | covers | why it beats Tier 0 | public repo |
|---|---|---|---|---|---|---|---|
| Universal Base Characters, Source | Quaternius | https://quaternius.itch.io/universal-base-characters | "Creative Commons Zero v1.0 Universal" | **$19.99** or more | All 8 bases, 20 hairstyles, rigged .blend, engine projects | The .blend rig and every body: Lotus can kit 10 classes from one base set | Yes (CC0; large files, so better kept out of git anyway) |
| Universal Animation Library 2, Source | Quaternius | https://quaternius.itch.io/universal-animation-library-2 | "(CC0 License)" | **$14.99** or more | All 130+ animations with the .blend rig | Every sword combo and recovery, editable to put contact on frame 5 or 8 | Yes (CC0) |
| Knights Pack | LuizMelo | https://luizmelo.itch.io/knights-pack | "can be used in commercial and non-commercial projects - CC0" | **$7.75** or more | 3 knights: one-handed sword, sword and shield, two-handed sword; block and hold-shield clips | A shield figure for a bulwark placeholder in the free family. Optional, same style caveat as 0-B. | Yes (CC0) |
| A Bunch of Portraits | Subotai | https://subotai-khudozhnik.itch.io/a-bunch-of-portraits | "Purchasing this pack allows you to use these portraits in both commercial and personal projects. You may NOT resell the assets as is, or without modifications."; "I require author credit"; "No generative AI was used" | **$8.00** or more | 55 medieval fantasy portraits | Hand-made **greyscale** on transparent backgrounds: already the cold world, and one amber accent can be laid over a player face. One artist, consistent. Size is "roughly 1/4th an A5 page" (pixel size unverified). | Keep out (credit required) |
| The DARK Series: Dialog Portraits | Penusbmic | https://penusbmic.itch.io/the-dark-series-dialog-portraits | "Feel free to use for commercial projects and modify the characters if needed... Please do not resell the assets individually."; "No generative AI was used" | **$3.00** or more | 54 pixel portraits (27 dark, 27 coloured) | A dark-fantasy mood, cheap. Pixel, so it is an alternative to Subotai, not both. | Keep out |
| The DARK Series: Dark Bandits | Penusbmic | https://penusbmic.itch.io/the-dark-series-dark-bandits | Same Penusbmic terms; "No generative AI was used" | **$3.99** or more | Dagger bandit, archer bandit: idle, attack, death and more | Grounded bandits for the Tollgate's enemies, darker than LuizMelo. Side-view pixel, frame size unstated. | Keep out |
| Elthen's Pixel Art Shop (Royal Spearman $5, Knight with horse $3, Drake $5) | Elthen | https://elthen.itch.io/2d-pixel-art-royal-spearman-sprites, /2d-pixel-art-knight-sprites, /2d-pixel-art-drake-sprites | Listing: "Feel free to use the sprites in commercial/non-commercial projects!" Full terms are on Patreon (post 27430241), which returned 403. **Licence detail unverified.** | $3 to $5 each | Spearman 96x96 frames; knight 32x32 with horse clips; drake 96x32 | The only cheap family I found with a spearman, a horse and a drake from one artist. All small pixel art, so a placeholder family only. | Unverified; assume keep out |

**A suggested Tier 1 basket: about $43.** UBC Source $19.99 + UAL2 Source $14.99 + Subotai $8.00
+ Penusbmic portraits $3.00 (optional instead of Subotai). Add the UAL 1 Source tier if Lotus
needs its .blend (price not shown on the page I read). That leaves room under $100 for tipping
the free CC0 creators whose work ships (LuizMelo, Quaternius, Kenney all ask for optional
support).

---

## Tier 2: one commission at a time, in order of impact

The order follows issue #816's round 255 (the drake and Kinsbane first, as one commission from
one artist, for the Harrow Weir choice screen), then the brief's list.

### Rate evidence (read on each page)

| source | URL | rates quoted | commercial terms stated |
|---|---|---|---|
| prdprd (illustrator price sheet) | https://prdprd.carrd.co | Headshot $30, bust $40, half body $50, full body $70, character design $80 to $160 | "+100% of the total price for any commercial use" |
| saikotikkk (price sheet) | https://saikotikkk.carrd.co | Bust $45, half body $60, thigh up $75 | "Commercialized commissions are double the final price"; an indie discount mentioned |
| Pamperloth (itch [For Hire]) | https://itch.io/post/13654134 | $8/hour; headshot $35, bust $55, half body $80, knee-up $90, full body $120 | Not stated |
| miagameart (itch [For Hire], 2024-07-22) | https://itch.io/post/10361496/view-in-topic | Pixel character from $15 (up to 32 px) or $25 (48 px and up); **$3 to $8 a frame**; tilesets from $35 | Not stated |
| Jammie / dojaemie (itch, 2025-01-10) | https://itch.io/t/4455928/for-hire-pixelart-and-animator-looking-for-paid-projectworkcommission | Character with idle/walk/attack $20 to $70+; extra animation $10+; icons $10 to $15; design concept $50; $10/hour | Not stated |
| Atraament (itch, 2025-01-16) | https://itch.io/t/4472219/for-hire-pixel2d-artist-character-background-ui-animation | Characters $15 to $25; animation about $5 a frame | Not stated |
| MOMONGA (itch) | https://itch.io/post/11484074 | $21/hour for larger projects | Not stated |
| Kazecat (itch) | https://itch.io/post/12384929 | Hand-drawn animation **$20 a drawing/frame**; about $100 to $250 per animation | Not stated |
| RocketBrush (studio blog, 2025-09) | https://rocketbrush.com/blog/character-concept-art-cost-a-full-guide | Sketch $200 to $300; coloured draft $300 to $500; detailed concept $500 to $1,000; studio $35 to $37/hour | Studio rate |

How the ranges below are built: the low end is the indie sheets above **doubled for commercial
use**, since two of the sheets double for it and the itch posts do not say. The high end is
RocketBrush's studio rates, or $20 a frame for hand-drawn animation. An artist good enough for
"Darkest Dungeon crossed with Unicorn Overlord" will likely sit at or above the high end; that is
my judgement, unverified. One full clip set is 62 frames (8 clips, ART_SPEC's table).

### Tier 2 list

| # | piece | what is delivered | price range per piece | basis |
|---|---|---|---|---|
| 1 | **Rook's cold drake, 3 growth stages**, with **Kinsbane** (scythe and hound), from one artist | Concept and turnaround per drake stage (Lotus can model from it) + Kinsbane design + the choice-screen image | Drake design $160 to $500 a stage, so **$480 to $1,500** for 3; Kinsbane design **$160 to $500**; choice-screen illustration **$140 to $1,000** | prdprd design $80 to $160 x2 / RocketBrush coloured draft to detailed |
| 1b | (optional) the drake animated as clips instead of modelled by Lotus | 62 frames per stage | **$186 to $496 a stage** pixel ($3 to $8 a frame); **about $1,240 a stage** hand-drawn ($20 a frame) | miagameart; Kazecat |
| 1c | Kinsbane's effects | e.g. 3 sheets of 8 to 12 frames, 256 px | **$24 to $96 a sheet** pixel; **$160 to $240 a sheet** hand-drawn | $3 to $8 or $20 a frame |
| 2 | Portraits: captain (two genders), Keziah, Rook | 4 busts, 192x192 | **$80 to $300 each**, so **$320 to $1,200** | Busts $40 to $55 x2 (prdprd, saikotikkk, Pamperloth) to RocketBrush colored-draft low end |
| 3 | Portraits: Hask and Marrit | 2 busts | **$80 to $300 each** | as above |
| 3b | Hask's boss clip set (lance, frozen-iron shard) | 62 frames | **$186 to $1,240** | per-frame rates above |
| 4 | Remaining cast portraits: Wren, Teodor, Ottilie, Pell, Dunstan, Maud, Ansgar, Brannock | 8 busts | **$80 to $300 each**, so **$640 to $2,400** | as above |
| 5 | Custom battle bodies for the stars (Keziah, Rook mounted, the captain) | 62 frames per weapon set | **$186 to $1,240 a set**; or **$0** if Lotus models them in Blender from the portrait as a reference | per-frame rates above |

**Tier 2 subtotal, items 1 to 4 without animation: about $1,900 to $7,200.**

---

## Tier 3: the full commission list (for later)

| group | count | range | basis |
|---|---|---|---|
| Missing silhouettes (chaplain, skyrider, bulwark) + tell variants | about 5 | $50 to $150 | icons $10 to $15 (Jammie); likely $0 by adapting game-icons.net |
| Tiles | 13 | $0 (client draws them); $13 to $60 if ever commissioned | $1 to $3 a tile, $20 to $60 a set (Jammie) |
| Class clip sets | 47 sets, 2,914 frames | **$8,742 to $23,312** pixel ($3 to $8 a frame); **about $58,280** hand-drawn ($20) | miagameart; Kazecat |
| Boss clip sets (+ Hask) | 7 sets, 434 frames | $1,302 to $3,472 pixel; about $8,680 hand-drawn | as above |
| Effects | 11 sheets, 84 frames | $252 to $672 pixel; about $1,680 hand-drawn | as above |
| Portraits | 12 | $960 to $3,600 | Tier 2 rate |
| Level-up poses | 11 full-body, 1 frame | $140 to $500 each, so $1,540 to $5,500 | full body $70 to $120 x2 (prdprd, Pamperloth) to RocketBrush detailed concept low end |
| Drake 3 stages + Kinsbane + choice screen | 1 set | $780 to $3,000 (designs) + $560 to $4,440 if animated | Tier 2 |
| **Total, everything commissioned** | | **about $14,000 to $44,000** at pixel per-frame rates; **about $85,000** with hand-drawn frames at 256 px | sum of the rows |

The class clips are 80 to 90 percent of that total. That is why the levers below matter.

---

## Cost levers

1. **Generic class bodies, made once and recoloured per character.** The 47 class clip sets are
   Lotus's Blender work on the CC0 Quaternius bases (Tier 0 + Tier 1, $0 to $35). Each recruit
   is the class body with a palette swap and one amber accent. That takes about $9,000 to $58,000
   off Tier 3. A palette-map recolour to LOOK's values is code, not generated art (issue 816).
2. **Portraits carry identity.** Spend the money on 12 portraits ($960 to $3,600) rather than
   custom bodies. The battle body can stay generic; the face is who they are. The only exceptions
   are Kinsbane and the drake, which ART_SPEC already lets break the rules.
3. **Code-driven effects instead of drawn frames.** Sparks, the slash arc, dust and embers are
   short hard-edged shapes in LOOK values. `docs/art/make_art.py` already draws them from coded
   shapes; keep it, or tint Kenney's CC0 particles. That saves $250 to $1,700.
4. **Commission designs, not frames.** For the drake and Kinsbane, buy concept turnarounds and let
   Lotus model and animate them in Blender. Designs cost hundreds; 186 frames of drake cost
   thousands.
5. **One artist for the whole Tier 2 run** keeps the family consistent, which ART_SPEC ranks above
   fit, and makes a bundle price easier to ask for (Pamperloth's post mentions bulk negotiation).
6. **Free routes, stated without promises** (general guidance, not researched here). Art students
   sometimes take portfolio pieces for credit and a small fee. Revenue share is common in hobby
   jams but rarely accepted by working artists for commissioned assets, and it needs a written
   agreement. A playable demo with placeholders is the usual way to fund real art afterwards.
   Placeholders stay until then.

---

## Excluded after reading the listing

| item | URL | reason |
|---|---|---|
| Fantasy Characters Pack #6 (Two Orcs) | https://two-orcs.itch.io/free-fantasy-characters-pack-6 | Tagged "AI Assisted"; "You Can't use these in commercial projects" |
| Medieval Painterly Avatars (Kalponic Studio) | https://kalponic-studio.itch.io/medieval-painterly-avatars | Tagged "AI Assisted" / "AI Graphics" |
| Medieval & Fantasy Portrait Pack (jorbaa) | https://jorbaa.itch.io/medieval-face-portraits | The page says it was made with Aseprite "and Pixellab plugin". PixelLab is an AI pixel-art generator, so it fails the human-made rule. |
| KayKit Adventurers (Kay Lousberg) | https://kaylousberg.itch.io/kaykit-adventurers | CC0 and "No generative AI was used", but the proportions are chibi and cartoon, and ART_SPEC says "never chibi". At most a rig test. |
| Pixel Art Knight (abu) | https://abuysp.itch.io/pixel-art-knight | Tagged top-down, very small files; free tier is one character ($3 for all five). Battle clips are side view. |
| Medieval Army Pack (MtPixls) | https://mtpixls.itch.io/medieval-army-pack | Idle-only NPCs (2 to 6 frames); "You can not redistribute or resale, even if modified" |
| CraftPix freebies | https://craftpix.net/file-licenses/ | Licence read ("personal and commercial projects", "No attribution... required", no resale of source files), but no item fit the style; not item-checked |

---

## CREDITS note (for `LICENSES` / a `CREDITS` file)

CC0 items need no credit; credit is still offered where the author asks it kindly (LuizMelo,
Quaternius, Kenney, Hyptosis). Items that **require** attribution, if used:

```
Icons adapted from game-icons.net, licensed CC BY 3.0
(https://creativecommons.org/licenses/by/3.0/).
  <icon name> by <author: Lorc / Delapouite / ...>, https://game-icons.net
  (one line per icon actually used; note "modified" where recoloured or redrawn)

Portraits by Justin Nichol and the Flare project (Clint Bellanger), CC BY-SA 3.0
(https://creativecommons.org/licenses/by-sa/3.0/), https://opengameart.org/content/flare-portrait-pack-number-one
  Modified versions are released under CC BY-SA 3.0.

Portraits by Subotai, "A Bunch of Portraits", https://subotai-khudozhnik.itch.io/a-bunch-of-portraits
  (paid licence; author credit and identification required; files not redistributed)
```

Each pack's files also get a `LICENSES` entry with source, version and terms, as the fonts do
(ART_SPEC: "Each file set is listed in `LICENSES`..."). Optional, requested-but-not-required
credits: "Kenney" (kenney.nl), "Quaternius", "LuizMelo", "Penusbmic", "Elthen", "AnGav".

## Unverified, to check before money moves

- Elthen's full licence (Patreon post 27430241 returned 403).
- Quaternius Universal Animation Library 1, Source tier price.
- Whether UAL 1 or 2 include spear, lance or bow attacks (the page names sword combos only).
- The method behind Hyptosis's "portrait gen tool".
- Subotai's portrait pixel size; Penusbmic and LuizMelo frame sizes not stated on the pages.
- Which Kenney particles are hard-edged enough for the no-glow rule.
- All commission ranges are from public rate posts, mostly undated personal-use rates; quote
  each artist directly and get the written grant ART_SPEC requires.
