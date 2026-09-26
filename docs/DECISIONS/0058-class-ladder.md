# 0058 — The class ladder: what each shipped class asks to certify into it

Date: 2026-09-26. Issue 72, built by Code. Issue 72 shipped the requirement check with no class carrying one (0051 item 3: "the ladder is the Table's"). Build-only mode has ended, so this is Code's lean, provisional, for Chat to argue on the PR or the Table. Amends 0051 item 3 and DESIGN section 3. Content only, and reversible by editing `classes.json`.

## Decisions

1. **The ladder is climbed from the cadet.** The cadet asks nothing, so any unit may go back to it. Every other class asks a level: 3 for the foot classes (pikeman, reaver, bowman, adept, chaplain), 4 for the mounted and armored ones (outrider, skyrider, bulwark), since those change a unit's movement and not just its numbers.
2. **A class asks a rank where a cadet can train one, and a stat where it cannot.** The cadet wields sword, lance and axe, so the pikeman asks lance D, the reaver axe D, the outrider sword D and the skyrider lance D and spd 8. A cadet can never earn a bow, reason or faith point, so a rank there would lock the class to units already in it. Those classes ask a stat read against the unit's own stats: bowman dex 8, adept mag 6, chaplain mag 4 and res 4. The bulwark asks def 5. A test holds the rule: every rank the ladder asks is in a weapon the cadet trains.
3. **The outrider asks the sword, not the lance.** Cadets start with swords, so a sword cadet's road leads to the horse, and a lance cadet's leads to the pikeman or, with speed, the skyrider. The rider class is a sword user's reward for an early career on foot.
4. **The ladder is on screen.** `classes` on the between-map screen lists every class with what it asks and whether a trial stands in for its seal. `classes <unit>` adds what that unit still lacks for each one, in the refusal's own words (`needs level 4, has 3`). A requirement you can only learn by being refused is a rule off screen, and the Table has ruled against that three times.
5. **Cosmetic.** The refusal into a unit's own class reads `is already an Adept`, not `a Adept`.

## Played

Code, by hand, on the journaled seed 139 campaign (`docs/transcripts/2026-09-26-campaign-139-ladder.txt`), without its certification and with `classes` read at the first and third camps. After two maps nobody qualifies for anything. Wren is level 3 with sword D (47 points), and the Outrider needs only her next level. She is one dex short of the bowman. The captain has sword 17 and is still level 1, and the rest are level 1 or 2. So on this line the first certification lands before map 4 at the earliest, and the Outrider trial, which needs level 4, opens around the middle of the campaign.

## Not decided

- Whether level 4 before the first horse is too late in a six-map campaign. The captain earns little EXP, so he will certify last.
- Whether the purse matters. It ended at 1620 on this line with nothing to buy until someone qualifies, so the seal is now the thing to save for.
- Whether the bulwark's def 5 is too cheap. The captain already has it at level 1.
