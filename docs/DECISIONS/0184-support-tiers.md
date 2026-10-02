# 0184 — Support tiers on rapport, and the #809 kinds (#77 slice 2)

Date: 2026-10-02. Built by the chain Builder. Provisional: the tier numbers are a lean, and they go to the Design Table before anything reads them in battle.

## Chat's answers on #809, applied (amends 0183)

- **The captain and Keziah are `argument`, not `mentor`.** The claimant offering "something hungry" argues with the captain rather than being taught. That gives the pair friction a romance can grow out of, and it keeps the two claimants' captain pairs from reading as "the one you owe" and "the one you raise".
- **Pell and Keziah move from `argument` to `mentor`,** so Keziah's five pairs still share no kind. Keziah, the grown woman with a god in her scythe, teaches the young levy man. Pell's pairs are comedy, romance, mentor, and debt with the captain.
- **Both claimants can romance a "they" captain too:** `romance: ["he", "she", "they"]`. "Either captain" was always "whoever the captain is". Teodor and Brannock keep their gating.
- **The captain's kind exemption** stands as built.

## The tiers as built

`rules.json`'s rivalry block takes an optional `supportTiers` array listing C, B and A in that order. Each entry is a `tier`, the rapport `at` which a support pair reaches it, and the `hit`, `avoid` and `crit` that each of the pair gains while the other stands adjacent. These are 13.1's rapport points: one counter with tiers on top of it, not a second system (the issue's title).

| Tier | At | Hit | Avoid | Crit |
|---|---|---|---|---|
| C | 16 | +5 | 0 | 0 |
| B | 40 | +5 | +5 | 0 |
| A | 72 | +10 | +5 | +5 |

The loader refuses, naming file, entry and field:
- a list that is not exactly C, B, A;
- a C below `overwriteAt`, since a pair would then be rivals and supported at once;
- an `at` that does not rise;
- a bonus below 0, or below the tier before it, since growing a support must never be a downgrade.

`Supports.TierOf(content, a, b, points)` gives a pair's tier. Two people who are not a support pair never reach a tier, but their rapport still ends a rivalry.

## Why these numbers (the lean)

- **C sits at the overwrite (16).** For a rival pair, reaching C is the moment the rivalry ends: the `respect` kind on the board. With the symmetric arm kept (0043), supports are relief rather than a payoff that has to beat the rival's +10 crit, so the tiers can be small (#77's body).
- **B at 40 and A at 72.** The rates run 1 to 4 per recruit, so a pair of Cha 3 to 5 earns about 4 to 7 per threatened phase. That puts C within a map, B a few maps in, and A only for a pair that has fought side by side through most of the campaign, provided rapport carries on the record (slice 3).
- **A's +10 hit, +5 avoid, +5 crit** is under the captain's tier-2 formation bands (0164), so a pair beside each other never outweighs the ladder.

## Not in this slice

Nothing in battle reads the tiers yet. Slice 3 will do that, after the Table argues the numbers:
- carry rapport on the campaign record;
- let the captain's pairs accrue (today only recruits accrue);
- apply the tier through `ToCombatant` beside the rivalry modifiers, so the forecast and the planner both see it;
- print the tier on the roster.

The writing waits on `docs/WRITING.md` and the voice sheets (Lotus's writing method, #780).
