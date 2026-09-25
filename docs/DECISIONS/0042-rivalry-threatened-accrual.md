# 0042 — Rivalry, second pass: rapport accrues only on threatened phases

Date: 2026-09-25. Issue 209, from Chat's cold play of the rivalry sample on seed 29 (twenty-ninth round). Amends DECISIONS/0038, decision 5 and decision 6. Built by Code; reversible, and still behind the `rivalry:` header, so no gated map and no gate number moves. This record does not decide keep or kill.

## Decisions

1. **One strike set in the core.** `Threat.StruckBy(state, content, side)` is every tile some unit of that side could strike next phase on the board as it stands: every usable weapon it carries, in range from every tile it can end a move on and its own tile. A player unit and an Aggressive enemy (a woken Guard included) may move. A Hold or Boss enemy strikes from its own tile only. A sleeping Guard threatens nothing, since it acts only once its group wakes (#47's reading). `RetreatRule.Struck` (#204) now delegates to it, so the retreat refuge, rapport accrual and the exposure line read one function.
2. **Accrual.** At the end of a player phase, an adjacent pair of recruits gains both rates only when some awake enemy could strike one of the two where they stand. A pair nobody can reach gains nothing. The board read is the one the phase ends on, after every wake the phase's commands caused.
3. **Exposure.** `Rivalry.Exposed` is the recruits beside a rival on a tile the enemy strike set covers, and the CLI's line now reads `N threatened player phases ended beside a rival, M of them attacked in the enemy phase after`. Exposure is per unit and accrual per pair, so every exposed recruit's pair accrues on that phase; a test holds the implication on three boards.
4. **Unchanged:** the symmetric arm, -5 raw hit, the threshold 16, the rate table.

## What the plays found

- Chat's seed-29 list on the new rule: one accrual, turn 3, Ottilie and Teodor to 4. Neither rivalry ends; both are live into the boss fight. Exposure 1 of 1. The game still routs on turn 13.
- Code's seed 41 by hand (PLAYTEST.md), 7/6/7: the only threatened adjacency Code chose, Ottilie beside Teodor as the fort woke on turn 5, gave the pair 4 and cost Teodor to an archer crit at 13 and a wingrider crit at 12, both printed with the rival's -10 crit avoid. Recalled. The committed line has no rapport at all: the rivals were kept apart under threat and stood together only where nothing could reach, which now buys nothing.

## Not decided

- Keep or kill. Kill if a cold play from each chair on this version still ends every rivalry before the fight that matters (issue 209). The opposite failure is now visible: at 4 per threatened phase to 16, a careful player may never buy the cure, and the arm becomes a standing tax paid by spacing.
- Cha's rate table, re-read once the plays are in.
- The battalions fork, filed by the PR that carries the final keep or kill.
