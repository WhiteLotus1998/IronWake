# 0035 — The Tollgate: the woods group holds its forest and throws

Date: 2026-09-25. Issue 197 (issue 13's Tollgate slice, second pass). Spec from the twenty-seventh round: on four plays from both chairs (seeds 31, 37, 97, 101) the woods group woke on turn 2 or 3, walked onto plain, and died with no decision; Chat's lean is that it stays in its forest and screens the hill, so 6,4 is taken and not walked onto. Built by Code on what ships after DECISIONS/0033; Chat argues on the PR if it disagrees.

## Decisions

1. **The woods group is `behavior:hold`** (both units, from `guard`). It never wakes and never walks; the free kill on plain is gone.
2. **The Toll Brigand** (`toll_brigand`): the brigand's reaver template with the enemy-only Toll Axe (1-2, DECISIONS/0029) as its only weapon. It replaces the brigand at 6,5, in the forest adjacent to 6,4. The brigand template is unchanged on the other maps.
3. **A test states the screen:** the woods group holds, the toll brigand stands at 6,5 at distance 1 from 6,4, and its weapons reach 1 and 2, so the hill is struck every enemy phase while it lives and a strike on it from range 2 is answered.
4. **The limit stays at 10** (twenty-seventh round). Code's hand line is 9 and gate 1's p90 is 9. See Not decided.

## Why the thrown axe and not Hold alone

Hold alone was built and played first (seed 113, not journaled). The brigand's iron axe reaches 1 only, and every tile at range 2 from 6,5 other than 5,4, 4,5 and 7,6 is out of the woods archer's bow, so Pell struck the brigand from 8,5 with no counter and the captain finished it from the forest at 7,5. Nobody on the party took a hit from the woods group; the screen cost one turn and no HP, and the seize came on turn 7. That is a turn tax, not a place. With the Toll Axe every tile that can strike the brigand is answered, and a cadet's strike into its forest prints 60 to 63, so clearing the screen is an ordering question with a price, and the price is paid by the unit the door needs.

## Measured (200 seeds, `docs/measurements/2026-09-25-full-woods-screen-200seeds.txt`)

| The Tollgate, two rolls | Gate 1 | Losses | Gate 4 |
|---|---|---|---|
| As shipped (DECISIONS/0033) | 150 (75 percent), median 7, p90 9 | 50 timeout, 0 captain, tail 0.4 | ok, 0.480 |
| Hold alone | 149 (74 percent), median 8, p90 9 | 51 timeout, tail 0.2 | ok, 0.325 |
| Hold plus the Toll Brigand (shipped) | 137 (69 percent), median 8, p90 9 | 63 timeout, 0 captain, tail 0.3 | ok, 0.330: Pell 0.455, Wren 0.330, Teodor 0.250 |
| The same, one roll | 112 (56 percent), p90 10 | 88 timeout | ok, 0.260 |

All eight gates pass under two rolls. The timeouts keep a quiet tail near zero: the heuristic is still fighting when the clock runs out, so they are the limit biting, not a board that stopped. The refused-kill median over them falls from 0.9892 to 0.8674, so fewer of the stalls are the veto refusing a certain kill.

## The hand play

Seed 127, seize on turn 9 of 10, no Recall, no deaths; Pell ended at 6 of 16 and Teodor at 9 of 21. PLAYTEST.md has the entry; the transcript is `docs/transcripts/2026-09-25-the_tollgate-127.txt` and a test replays its script under `--strict`. The seed 97 test is retired with this change, since the woods group no longer walks into the line its script expects; its transcript stays as the record of the build it was played on.

## Not decided

- The limit. Code's line was 9 with no Recall, and a Recall on the woods would have cost the seize. By the seventh round's rule (hand line plus about two) the limit would be 11; the twenty-seventh round kept 10 on the old woods, and the bite is where the tension came from. Chat's cold re-rate decides.
- The woods archer did nothing on the play: it holds at 5,6 and nothing stood at range 2 of it. It is kept as the screen's west half, and the re-rate says whether it earns its tile.
- The map is not `tuned`. It needs Chat's cold re-rate on this version.
