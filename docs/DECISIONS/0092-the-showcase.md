# 0092 — The showcase: one level polished early

Date: 2026-09-30. Design Table #503, rounds 126 to 128. Epic #509, slices #510 to #516.

## Context

On 2026-09-29 Lotus asked, through the desktop session and in his words as an ask and not a ruling, for one level polished to a level he can show people: graphics, animation, an in-game how-to-play, assets, a map that is fun to play and to look at. Not the characters' stories or abilities, not the campaign story; those come later on our schedule. After that one level we go back to building on our own order. He said to say no if it would disrupt the workflow.

DESIGN.md section 12 puts the polish pass in Phase 4, not started until Phase 3's gates pass (0046). The thin renderer (0070) and its readability pass (#349) are Phase 3's, and the beta they make is a readable debug client. Lotus's only read of that client was "cool but really simple" (#374).

## Decision

Both partners said yes. One level, the Tollgate on main's rules with its tuned roster and no experiment headers, gets its polish pass now, as a bounded `phase-3` epic in the beta's queue slot: behind bugs and #131, ahead of #486, #501 and the rest of Phase 3. It does not disrupt the workflow, because everything the showcase needs the beta needed anyway, and Chat would have argued for most of it within a month.

- The look is a drawn, flat vector style of our own, built from Godot shapes, gradients, shadows and a palette. Not a borrowed tileset, so it is reviewable as text and iterated in the same run that writes it. Lotus has said the look is ours to choose and he reviews it after; both partners review it from rendered frames, which both can read as images (Chat's correction in round 127, confirmed by Code in round 128), and Lotus has the final eyes.
- Slice 0 is `docs/LOOK.md` with one mocked frame, judged by Lotus through a `for-lotus` issue before anything is animated, so his feedback on the look happens once, cheaply.
- The look's rules: class silhouettes over letters; the forecast drawn as the screen's centrepiece; Recall animated as a rewind through the protocol's history; animation never hides state, with speed, fast-forward and skip; the parity gate and `PaletteTests` extend unchanged; the how-to-play is one screen and three turn-1 callouts.
- Sound is CC0 with a `LICENSES` file naming each source, or the clip is cut; fonts are OFL. Neither partner can hear, so sound is Lotus's alone to judge.
- Each merged slice posts its frames on the Design Table; both partners score it 1 to 10 on reads at a glance and would you show it to someone; the next slice goes `ready` at 7 or more from both chairs.
- Palette lean, provisional until the frame is judged: a cold, muted world with warmth reserved for the player's side, the enemy inside the world's cold.
- Out: portraits, stories, signatures, the campaign, every other map. Nothing else gets art until this is judged.

## What it changes

DESIGN.md section 12's Phase 4 line is amended: one level's polish comes forward on Lotus's ask; the rest of Phase 4 still waits on Phase 3's gates. Architecture (section 2) is untouched: the core stays engine-free, the client carries no rules, and the parity gate stays the proof.

## Not a fork

This is content and presentation inside the game. CC0 and OFL carry no obligation beyond the licence file; if a slice ever needs money, an account or an external service, that slice files a `fork`.
