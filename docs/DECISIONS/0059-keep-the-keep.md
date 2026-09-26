# 0059 — Keep the keep, provisionally: two edits, attacked twice

Date: 2026-09-26. Issue 82 (experiment 13.5), forty-fourth round on the Design Table (#265). Both partners played the spike from opposite halves of the menu: Code on seed 82 with both walls (7/6/7), Chat on seed 91 with the ditch and both forts (6/6/5). Chat's leans, with Code's refinements. Closes #82; the follow-ups are #287 and #288.

## Decisions

1. **The keep is kept, provisionally.** Code's play felt the walls, because `threat` counted them. Chat's play felt a sortie punished, and the forts it bought went unfelt.
2. **The keep's acceptance is play, not gate 1.** The heuristic sorties through the breaches instead of holding them, so a narrower breach traps its sortie (base 85 percent, both walls 62). Gate 1 cannot price a defensive edit. Nobody builds a holding heuristic to fix that.
3. **No spend can close a breach, and the bare keep must be fair on its own.** Edits change where the fight happens, not which difficulty tier the player is on.
4. **The edits are priced against the purse, not against zero harm** (Chat). A menu that can be ignored for free is decoration. An edit competes with a seal or a steel weapon. Ignoring it may cost a recruit, never the map. This replaces #82's guess that the prices should be low enough that ignoring the system is never punished.
5. **The fort edit is cut; the menu is wall and ditch** (#287). 11,4 and 11,7 are the perches a player uses anyway, so the edit added little and none of it was felt. A third edit returns only if the raid shows a leak that neither answers. #82's "three edits" acceptance is superseded.
6. **The waves run to the clock** (#287). The last spawn was turn 4 on a limit of 8, which gave both plays two free turns. Waves go on enemy phases 5 and 6 with the heaviest last, and the first wave reaches the breaches by enemy phase 2.
7. **The keep is attacked twice** (#288, Chat's lean). A lesson that arrives on the last map cannot be spent, and a defensive edit that works is invisible. A mid-campaign raid, fought on the bare keep with a smaller force from the finale's own spawn tiles, shows the player where it leaks. The menu opens only after the raid (Code's refinement: a spend before it is a guess). The finale is fought on the keep the record holds.
8. **Each placement prints what it does in rules terms, derived from content** (#288, Chat's lean). This is the Brackwater line again: rules go on screen, geometry doesn't.

## Played

- Code, seed 82, both walls: `docs/transcripts/2026-09-26-ironwake_keep-walls-82.*`. The hexer's cast from 9,5, the unbought ditch tile, killed Ottilie.
- Chat, seed 91, ditch and both forts: `docs/transcripts/2026-09-26-ironwake_keep-ditchforts-91.*`, replayed by Code under `--strict` to the same game. The hexer walked 0,8 to 4,8 to 8,8 and died there. On a keep with the forts and no ditch, the same script diverges on turn 2 (a different enemy board refuses Ottilie's move), and the hexer still walks 0,8 to 4,8 to 8,8. On this seed, then, the ditch most likely changed nothing, which is decision 7's invisibility in its plainest form.

## Built (#287)

Decisions 5 and 6 are in the content. The menu in `campaign.json` sells a wall (400) and a ditch (300). The van starts a tile closer, at x 3 to 6, so its soldier and brigand stand on 10,4 and 10,7 after enemy phase 1 and strike the breaches on enemy phase 2. The waves are two on enemy phase 2, two on 3, one on 4, two on 5 and four on 6 (two brigands, a soldier and a hexer), so the last spawn arrives on the breaches on enemy phases 7 and 8. `KeepTests` holds both shapes. Gate 1 at 100 seeds, read and not gated: bare 77 percent, walls 62, ditch 82, both 64 (before: bare 85, walls 62). Code's seed 287 play on the bare keep survived with three recruits fallen and two Recalls spent; the tensest turn was player phase 8. The two journaled scripts from #82 replay against their own `.map` copies and are unchanged.
