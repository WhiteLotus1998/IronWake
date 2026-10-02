# 0153 — The rename pass: plain labels, Acc first

Date: 2026-10-02. Issue #701 (Lotus's progression batch, item 4; Chat's round 216, Code's round 218). The rule and the table are the Table's; the choices below are Code's implementation calls.

## Decided

- **The rule.** A distinctive coinage from another series goes; an ordinary English word stays. Labels change, ids do not: `canto`, `reason`, the `-breaker` ids, the `art` effect kind, protocol keys and every script word are as they were.
- **Abilities.** The seven -breakers become Sword, Lance, Axe, Bow, Fist, Faith and Lore Sense, Canto to Move Again, Deadeye to Steady Aim. Ability texts read Acc, Evade, Power and Crit.
- **Lore.** `WeaponType.Label()` is the one place a type becomes a word, `lore` for `reason`. Ranks, item cards, technique lines, rank-up lines, refusals and certification read it. Asset names (`ArtSpec.Kind`), the Sim's audit rows and content serialisation keep the id.
- **Acc first.** The forecast line reads `acc 85% dmg 11 x2 crit 5%` on both sides (a raise reads `acc -- dmg 14 crit --`). The weapon line reads `(acc 75 power 5 crit 0 wt 5 range 1-1)`, a broken one `broken: -10 acc -5 power`. The item card reads `Acc 70, Power 5, Crit 0, Wt 5`; a healing spell `Acc 100, heals, Crit 0`. The client's attack menu row and the Godot forecast card (`ACC`) follow.
- **Modifiers.** The pincer, brace, signature and rivalry lines read `acc +15` and `crit evade`; the board legends for pincer and brace say Acc. The camp's terrain bonuses and the client's move preview read `evade`.
- **Techniques.** The unit card's `Arts:` is `Techniques:`, the forecast's art line begins `technique` (`Technique` in sentence case), and the three refusals say technique. The script word stays `art <id>`.
- **Move Again.** The unit card's line, the "may move again" prompt and the event (`moves again 4,4 -> 4,5`, `stays at 1,3 (move again)`). The command stays `canto`, and so does the roster row's `canto 3`, which names the command that spends it.
- **Kinsbane and the forge** read Power and Acc. The sample `canto_raid.map` is now named Hit and Run Raid; its file name is an id.
- **DESIGN** names Lore, Move Again, the Senses, Steady Aim and Techniques, and section 5 carries one paragraph mapping the labels; the formulas keep the short field names (Mt, Hit, Avoid) because they are the record's fields.
- **Transcripts are re-recorded, not frozen.** Every journaled transcript under `docs/transcripts/` and every test expectation went through one label transform; the games are untouched, and each replay test passing against its rewritten file is the check that the transform is exactly the new output. Old labels survive only in the PLAYTEST prose and the Table, which are history.
- **Balance is byte-identical.** `--smoke` before and after differs only in its millisecond timings; `--full the_tollgate --seeds 40` likewise.

## Left as they are (for Chat's ruling, issue 701's Unsure)

- Terrain cards say `-20 to hit a unit here`: "to hit" is the verb, not the label.
- `hit` and `crit` as strike results (`hits Brigand for 5`, `crit 12`) are verbs and nouns of the event, not the Acc label.
- The roster row's `canto 3` and the help's `canto <unit>` and `[art <id>]` are script words.
- Wt, Crit, Res, Def, Mag and the rest of the stat line are plain abbreviations.
- Names kept by the rule: Faith, Promotion, Refine, Support, Mastery, Horsebane, Bloodrush, Grace, Fundamentals, Deep Study, Unsworn, Hold the Gate.
