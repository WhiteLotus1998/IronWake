# The cast on the board: an audit (issue 487, for 13.18)

Date: 2026-09-29. One row per cast member, read from `content/units/cast.json`, `content/classes.json` and every shipped map and keep file. Gate 4 drops are the Sim's `--full` rows at 200 seeds, kept in `2026-09-29-cast-audit-gate4-200seeds.txt` beside this file. No signature is built here; the shapes named are candidates for #486, and the ones the Table has agreed are marked so.

Stats are level 1 as fielded: the unit's own bases plus its class modifiers, in the order `hp str mag dex spd lck def res cha`. Growths are the unit's own plus the class's. A drop is the share of seeds whose outcome changes when the recruit is benched (on Brackwater, units out, 0 to 4); a drop under about two standard errors (se about 0.03 to 0.06) is not a difference.

## Where each recruit is fielded

A map with unnamed `recruit` slots fills them in cast order after the captain, so Saltmarsh Ford fields Wren, Teodor and Ottilie, and the raid and the keep field Wren, Teodor, Ottilie, Pell and Dunstan.

| Map | Recruits (gate 4 drop) |
|---|---|
| the_tollgate | Pell 0.565, Teodor 0.245, Wren 0.215 |
| brackwater_cut | Pell 0.370, Rook 0.185, Wren 0.130, Dunstan 0.020 (units out) |
| harrow_weir | Pell 0.440, Keziah 0.350, Teodor 0.255, Dunstan 0.220, Ottilie 0.190 |
| old_mill_road | Wren 0.315 |
| sallow_grange | Ansgar 0.530, Pell 0.450, Teodor 0.380, Ottilie 0.325, Wren 0.120 |
| saltmarsh_ford | Wren 0.015, Teodor 0.015, Ottilie -0.020 (the map fails gate 4 for everyone: its stall, not the cast) |
| ironwake_raid | Pell 0.475, Wren 0.330, Dunstan 0.195, Teodor 0.130, Ottilie 0.105 |
| ironwake_keep | Dunstan 0.425, Pell 0.445, Wren 0.395, Teodor 0.340, Ottilie 0.280 |

Findings before the rows. Maud and Brannock are fielded on no map. Rook is fielded only on Brackwater, Keziah only on Harrow Weir, Ansgar only on Sallow Grange, so each of them has one board to be read on, and 13.18's kill clause cannot be read for Maud or Brannock at all until Map 7 or a sample fields them. Pell has the highest drop on five of the six maps that field her (Ansgar leads on Sallow Grange). Wren is fielded on seven of the eight maps and is the lowest drop on the Tollgate and Sallow Grange and the second lowest on Brackwater.

## The rows

| Recruit | Class, move | Weapons | Level 1 | Growths that separate | Personality line |
|---|---|---|---|---|---|
| Alder Fenn (captain) | cadet, infantry 4 | Iron Sword, dressing; wields sword, lance, axe | 22 8 0 7 8 6 5 2 9 | Cha 50, Lck 35 | keeps a list of every cadet's name and reads it before each map |
| Wren | cadet, infantry 4 | Iron Sword, dressing | 20 7 0 6 8 5 4 2 3 | Spd 50, Dex 45 | counts tiles under her breath and is usually right; talks to fill silences |
| Brannock | cadet, infantry 4 | Hatchet, dressing | 19 6 0 5 6 4 4 1 3 | Def 30 | treats every drill as a race and every race as a drill |
| Teodor | pikeman, infantry 4 | Iron Lance, dressing | 21 8 0 5 5 2 5 1 4 | Def 40 | gives orders to anyone younger; follows them himself when nobody is watching |
| Ottilie | bowman, infantry 4 | Iron Bow, dressing | 17 5 0 9 6 4 3 2 4 | Dex 60 | keeps a ledger of every arrow and bills the crown for the ones she loses |
| Pell | adept, infantry 4 | Cinder, Gust | 16 1 8 5 5 3 2 5 3 | Mag 60 | sets small fires by accident and large ones on purpose |
| Dunstan | bulwark, armored 4 | Iron Lance; wields axe | 23 7 0 3 2 2 7 1 2 | Def 50, Hp 65, Spd 10 | has never once stepped back |
| Maud | chaplain, infantry 4 | Radiance, Salve | 17 2 5 4 4 5 2 7 5 | Res 50 | mends what she is handed, and prays over each whether it wanted one or not |
| Ansgar | outrider, cavalry 6 | Iron Lance, Iron Sword | 20 7 0 5 6 3 3 1 4 | Hp 50, Str 45 | rode here on a horse he says was a gift; its former owner says otherwise |
| Rook | skyrider, flying 6 | Iron Lance | 17 5 0 6 9 4 1 4 2 | Spd 60, Def 10 | goes high when nervous, which is exactly when she should not |
| Keziah | reaver, infantry 4 | Iron Axe, Iron Gauntlets | 23 9 0 4 5 3 3 0 2 | Str 55, Spd 25 | has never lost a fight she started; laughs at the wrong moment |

## Overlaps, and why a player takes each over the nearest alternative

- **The three cadets.** The captain is at or above Wren in every level 1 stat (Hp 22 against 20, Str 8 against 7, Dex 7 against 6, Spd equal at 8, Cha 9 against 3); Wren's edge is growth only (Spd 50 and Dex 45 against 40 and 40). Brannock is below Wren in every level 1 stat but Def and Cha (equal), and at or below her in every growth but Def (30 against 25), and carries the Hatchet where Wren carries the Iron Sword. Why Wren over Brannock today: nothing but the sword. Why Brannock over Wren: nothing. This is the reference's question 5 in our own numbers.
- **Teodor, Dunstan, Ansgar (the lances).** Teodor over Dunstan: 3 more Spd, so he doubles where Dunstan never does, and infantry movement. Dunstan over Teodor: 2 more Def and Hp at level 1 and Def 50 growth, the door. Ansgar over either: Mov 6 and a second weapon. The three are distinct already; a signature is not needed to separate them, only to make them someone.
- **Keziah and Brannock (the axes).** Keziah has 3 more Str and 4 more Hp at level 1 and the gauntlets; Brannock is faster by 1. On a map Keziah wins every comparison.
- **Ottilie, Pell, Maud, Rook** have no overlap in the cast: the only bow, the only Reason, the only healer, the only flier. Their question is not "why her over another" but whether the board ever asks for her.

## Candidate signatures by kind

The budget is kinds, not signatures (round 114). DESIGN 5's closed set is stats, combat, canto and art; the rows name the kind each candidate needs and whether it exists.

| Recruit | Candidate shape | Kind | New? |
|---|---|---|---|
| Keziah | an art that pays HP in place of uses, paid on declaring, hit or miss (agreed, round 114) | art, one field | a field on an existing kind |
| Rook | below half HP (HP x 2 < max, read at the command), +1 Mov and cannot initiate an attack; counters stand (agreed, round 116) | condition on the holder over stats and combat | new kind, shared |
| Dunstan | cannot be shoved; if an enemy is adjacent when he moves, ends adjacent to one of them or stays (agreed, round 115) | movement rule | new kind |
| Ottilie | the ledger: a miss spends no use of her bow | art/weapon field: the use paid on a hit only | Keziah's field kind, another value |
| Teodor | orders: an adjacent ally who acts after him this phase strikes at +5 hit; unwatched (no ally within 2 of him when he strikes), he strikes at +10 | condition on the holder over combat | Rook's kind |
| Wren | counts tiles: Canto after a Wait or an Item, not after an attack (she walks on, she does not fight on the move); talks: her combat is noise at 8 in place of 6 | canto; the wake rule's radius per unit | canto exists; the noise radius per unit is new |
| Brannock | the race: +1 Mov on a phase where he moves before any ally; after a full-Mov move, -10 hit (he arrives winded) | condition on the holder over stats and combat | Rook's kind |
| Captain | the list: Commander's Word (13.2, #85), Cha's first job, parked until after Map 8 | order | 13.2's own |
| Pell | large fires on purpose: her Cinder lights forest under `wildfire: on` (13.15) on a hit; small by accident: on a miss too | wildfire's rule, scoped | exists behind a header |
| Ansgar | the horse: a mounted enemy of the map's first group is sworn against him from turn 1 (13.4's grudge) | grudges | exists behind a header |
| Maud | prays over what she is handed: a heal she casts on an ally at full HP is refused, and a heal on a hurt ally also clears the ally's brace and watch | item condition | new, small; open |

Three agreed signatures use two kinds and a field. The eight candidates beyond them add one kind (the per-unit noise radius) and one small item condition if every one is taken; the rest reuse the condition kind, the art field, canto, and the wildfire and grudge headers. Only the agreed three are the Table's; the other eight are this audit's first drafts for #486 to argue.

## What the audit says to do first

1. The cadets are the case: Brannock is dominated by Wren, and Wren's only edge over the captain is growth. If 13.18 fixes one thing, it is that two of the three cadets are the same unit with the captain's stats lowered.
2. A signature is readable only where the recruit is fielded. Maud and Brannock need a sample before 13.18's kill clause can be read for them, and Rook, Keziah and Ansgar have one map each. Map 7's roster is the place to answer this.
3. Pell's drop leads everywhere she is fielded and her line is already half on the board under wildfire. She is the recruit least in need of a signature.
