# 0107 — Generated art, slice 4: the token tells, one row per tell

Date: 2026-09-30. Issue 564, slice 4. Names and implementation; reversible.

## Decided

- **The question 0106 left open (per boss or per boss weapon, and whether the toll warden counts) is settled by the client's own rule.** The token that both chairs scored on the Tollgate (`Main.Look.cs`, `DrawSilhouette`) varies the silhouette by a tell, not by unit: a pikeman whose weapons reach 2 carries the hooked crossbar, and a reaver placed as a boss carries the double bit. A variant file must replace that placeholder shape for shape, so the rows follow the same rule. The toll warden counts: he is not a boss, but he carries the hook.
- **One row per tell, not per unit: `token_<class>_enemy_<tell>`.** Today that is `token_pikeman_enemy_hooked` (the toll warden, the Grange Reeve) and `token_reaver_enemy_double` (the Bandit Leader, the Weir Foreman). A token is 48 px and reads a shape, not a name; two bosses with the same tell look alike on the board, and their dashed ring and name say who is who. `ArtSpec.TokenTell` holds the rule and `ArtSpec.TokenVariants` derives the rows from every enemy the shipped maps place, so a new map that fields a hooked pike or a boss reaver adds nothing, and a new tell adds a row. A renderer without a variant's file falls back to the class's token.
- **The spec's rows sit after the captain's token.** `ArtSpec.Names` now takes the placed enemies with their boss flag instead of the boss ids alone; the boss clips still come from the bosses among them.
- **`make_art.py` draws the tells** with the client's strokes, bone on slate like any enemy token, and puts them in a third row of `docs/art/contact-tokens.png`. `ArtGeneratedTests` holds a variant to its class token: every bone pixel of the plain silhouette stays, and the tell adds at least 20.
- **The token's tell and the clip's tell can differ, and that is kept.** A clip is per boss weapon, so the Bandit Leader's steel axe clips are single-bitted; his token is double-bitted whichever axe he holds, because on the board the token says "this is the boss reaver", as the client already drew it.

## Next slices

Loading the tokens and tiles in the client, after Code's final showcase score, with the frames re-rendered; then the `for-lotus` sheet.
