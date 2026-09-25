# 0047 — The nine class masteries and their requirement

Date: 2026-09-25. Issue 69's last acceptance line, built by Code as a lean after build-only mode lifted. Provisional in the ordinary way: argue it on the Table or on the PR, and the Critic's Monday pass may reopen it.

## Decisions

1. **Every starter class names one mastery from issue 66's effect set.** No new effect kind; the issue puts the masteries' own effects beyond that set out of scope. Three Breakers, four on-combat modifiers, two passives. Each answers the class's weakness or sharpens its job, and each reads on the forecast or in `show`:

   | Class | Mastery | Effect |
   |---|---|---|
   | cadet | Fundamentals | +1 Str, +1 Spd, +1 Def (passive) |
   | pikeman | Horsebane | +20 hit, +10 crit against cavalry |
   | reaver | Bloodrush | +15 crit, -10 avoid in every fight |
   | bowman | Deadeye | +10 hit, +10 crit in every fight |
   | adept | Deep Study | +2 Mag (passive) |
   | chaplain | Grace | +10 avoid, +20 crit avoid in every fight |
   | outrider | Lancebreaker | the Breaker: the rider's fear is the pike, and the enemy roster is lance-heavy |
   | skyrider | Bowbreaker | the Breaker: the flier's nightmare, turned |
   | bulwark | Reasonbreaker | the Breaker: armor's hole is magic, the ninth round's Dunstan at Res 1 against Cinder |

   Bloodrush is a trade, not a gift, on purpose: the forecast shows both halves, and a reaver who mastered it stands differently. No two classes master the same ability, and a test holds it.
2. **The requirement is 12 combats, 8 for the chaplain.** The body's lean is "about a map and a half of steady use". Counting `X attacks Y` lines for cast members across every journaled transcript, a deployed unit fights about 4 to 7 combats per map and a front-liner about 8 to 13. 8 was built first and measured: on the seed-139 Old Mill Road replay Wren mastered Cadet on enemy phase 6 of 7, and on the Cleave play the captain mastered it on turn 8, so 8 is one map for a front-liner, not one and a half. At 12 the seed-211 hand play ends with the captain at 9 of 12. A chaplain's heals are not combats (DESIGN section 5), so at 12 a chaplain would take three maps or more; 8 keeps it on the same curve.
3. **The arts sample carries every shipped ability.** `docs/samples/arts/abilities.json` is documented as a drop-in for `abilities.json`, and a class naming a mastery the file lacks fails validation, so the sample is the shipped list plus Cleave. The journaled Cleave play predates the masteries and a transcript is never regenerated, so `CombatArtCliTests` replays it on content with no class mastery, the build it was played on.

## Measured

`--full --all`, 200 seeds, two rolls, main at 24ec573 against this branch. No gate changes verdict. Old Mill Road's gate 1 goes from 59 to 67 wins (29 to 34 percent, still FAILED; timeouts 109 to 102), Wren's gate 4 drop from 0.285 to 0.325. The masteries are the only change a game can read, so these are games where a unit reached 12 combats inside the map; which unit was not traced. Saltmarsh Ford goes from 22 to 23 wins (gate 4 still FAILED, 0.030 to 0.015). The Tollgate is unchanged to the game (159 of 200, drop 0.310). Gate 5 draws the shipped abilities, so its crits read 962 to 967 against expected 927 to 930, inside its tolerance.

## Not decided

- Masteries are felt across maps, and the campaign record that carries points from one map to the next is #74's. Until it lands, no single-map play shows one except on a very long map.
- Whether any enemy should carry a mastery or a Breaker. Enemies earn nothing (DECISIONS/0017), but content may still hand an enemy an ability; none does.
