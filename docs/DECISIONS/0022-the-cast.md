# 0022 — The cast: eleven units in content, roster order, and what each covers

Date: 2026-09-19. Issue 13, first of its three runs (the review comment on the issue lays out all three). Builder's calls, made from DESIGN.md section 9 and the Table's seventh and eighth rounds; argue them on the PR.

## The roster is content

`content/units/cast.json` holds the captain and ten recruits in the same shape as the enemy templates, plus `region`, `personality`, and `hooks` (two cast ids each; empty for the captain). The loader exposes it as `GameContent.Cast` in file order, the first the captain, and both the CLI's `play` and every Sim gate hand that list to `BattleState.From`. `SyntheticRoster` is deleted and its notice line is gone from every transcript and gate row; the transcripts dated 2026-09-18 are records of the synthetic roster (transcripts README). Content without a cast file loads with an empty cast; `play` refuses it out loud with exit 2.

Four validations, each falsified by a test: a cast member names a region; carries a personality; every hook names another cast member; a unit whose class casts only (an Adept or a Chaplain) carries at least two castable spells, since a spent spell does not equip (DECISIONS/0018 item 3; Table, seventh round). The last is the lean on the open question from the review: a Chaplain with one attack spell and one healing spell has two casting options and passes. Whether a healing spell should count is on the PR.

The cast is also in `GameContent.Units`, so gate 5's combat pool draws from it at levels 1 to 10 and a map's `E` line could name a cast member. Provisional: nothing refuses that, since no map does it.

## Roster order decides the early maps

A bare `recruit` slot takes the next undeployed recruit in roster order (DECISIONS/0014), so the order is authored for maps 1 to 3: wren (pinned by Old Mill Road), teodor, ottilie, pell, then dunstan, maud, ansgar, rook, keziah, brannock. Saltmarsh Ford fields wren, teodor, ottilie; the Tollgate adds pell, the first unit to fight with two books (#99's attack by slot).

## Coverage of section 9

| Id | Name | Region | Class | Weapon types | Movement | Carries |
|---|---|---|---|---|---|---|
| captain | Alder Fenn | crown | cadet | sword | infantry 4 | iron sword, dressing |
| wren | Wren | aldmere | cadet | sword | infantry 4 | iron sword, dressing |
| teodor | Teodor | aldmere | pikeman | lance | infantry 4 | iron lance, dressing |
| ottilie | Ottilie | sallow | bowman | bow | infantry 4 | iron bow, dressing |
| pell | Pell | aldmere | adept | reason | infantry 4 | cinder, gust |
| dunstan | Dunstan | kestrow | bulwark | lance | armored 4 | iron lance |
| maud | Maud | kestrow | chaplain | faith | infantry 4 | radiance, salve |
| ansgar | Ansgar | sallow | outrider | lance, sword | cavalry 6 | iron lance, iron sword |
| rook | Rook | kestrow | skyrider | lance | flying 6 | iron lance |
| keziah | Keziah | sallow | reaver | axe | infantry 4 | iron axe |
| brannock | Brannock | aldmere | cadet | axe (hatchet) | infantry 4 | hatchet, dressing |

Every weapon type (sword, lance, axe, bow, reason, faith) and every movement type (infantry, cavalry, flying, armored) appears at least once; the test named for section 9 checks it, along with three regions of three or four recruits each, the captain from none of them, the captain the highest Cha (his Cha scales Commander's Word, #85), two hooks per recruit, and every carried weapon usable by its carrier's class. Three regions: Aldmere, the river lowlands, sends four; Kestrow, the ridge, and Sallow, the coast, send three each. Hooks are not symmetric by rule; Phase 3 decides what a one-way hook means.

## Numbers

The captain and Wren keep the synthetic cadets' base stats exactly (22/8/0/7/8/6/5/2/9 and 20/7/0/6/8/5/4/2/3), so the committed winning script on Old Mill Road seed 7 still wins under `--strict` and the enemy-phase predictions in the test corpus still hold. What changed is growths: the synthetic roster had none, so every level-up in every play so far raised nothing; the cast's growths are the first time EXP and gate 5's stream describe the real game. Base stats sit at the enemy templates' level-1 scale (HP 16 to 22, Str 5 to 8) so map 1 is a fair fight at deployment; growths run 35 to 55 in HP and 35 to 50 in each class's main stat, with one starved stat per recruit as section 3 allows (Dunstan's Mag 0, Keziah's Res 10, Pell's Str 5).

Tuning numbers from here belong to issue 13's runs (2) and (3) and are recorded there.
