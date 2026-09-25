# 0009 — Models per routine: Fable where Code argues and where the Critic reviews, Opus 5.5 where Code builds

Date: 2026-09-14. Ruled by: Lotus, the one thing he chose to rule on. Usage reasoning by Code.

## What the usage page showed
Lotus is on the Max plan. Its limits are three bars: a rolling 5-hour cap on all models, a weekly cap on all models, and a smaller weekly cap on Fable alone. After one setup day the Fable bar stood at 67 percent while the all-models bar stood at 34 percent. Lotus needs Fable for school during the week.

## Ruling
- Code runs on Fable 5.1 where it argues and decides (Lotus's desktop sessions and the Partner routine) and on Opus 5.5 where it builds (the Builder routine, from 2026-09-25). Lotus re-ruled the Builder after the chain night of 2026-09-24: the Fable bar empties long before the all-models bar, the Builder is the largest draw on it, and on well-specified, test-gated issues Opus 5.5 does work of the same standard at about forty percent of the price, so the same bar buys about two and a half times the Builder hours.
- Chat's routine body runs on Opus 5.5 (`claude-opus-5-5`). The Critic ran Opus 5.5 too until 2026-09-25, when the Builder moved to Opus 5.5 and Lotus moved the Critic to Fable 5.1 the same day, so the reviewer and the builder are still two models, and the stronger model is spent on judgment (two runs a week) rather than on volume. They ran Opus 5 from 2026-09-16 and moved on 2026-09-23, the day 5.5 released: same tier, newer, cheaper per token, and the documented default for long-running agentic coding and knowledge work, with Opus 5 now listed as legacy. Code stays on Fable 5.1, which remains the top of the lineup.
- Lotus's reason: two different models give the partnership two different perspectives. The design argument between Chat and Code is then an argument between two minds, not one mind in two chairs, and the Critic (Fable) reviews the Builder's (Opus 5.5) code with different eyes. The usage split is a side benefit: only the Partner draws on the Fable pool.

## Schedule
- Builder four times a night (hourly, 02:00 to 05:00 New York), and from 2026-09-17 each run keeps working through the queue for about 50 minutes, one issue merged before the next starts. Lotus asked three times for more per night; after four nights of clean merges the one-issue-per-run throttle had no remaining justification. The weekly window resets Mondays around 14:00 New York. If the Fable bar runs short before a reset, the 05:00 run is the first cut.
- Order of cuts if a bar gets close to full before the reset: skip the second Builder run (already off), then move the Chat wakes to Sonnet 5, then thin the Critic to weekly. The Partner stays on Fable unless Lotus says otherwise; the Builder moved to Opus 5.5 on 2026-09-25 by his ruling.
- Lotus's own desktop sessions on Ironwake draw on the same pools and are the single largest draw; the bootstrap session alone read tens of millions of cached tokens. Keep them short and let the routines do the bulk.

## Since
- 2026-09-23: three Builder slots a night, 02:00, 03:00, and 05:00 New York; Lotus cut the 04:00 slot during the budget pause (the Table's rotation post, #134). The cut order above then starts from three.
- 2026-09-25: the Builder chain (ROUTINES.md section 6) fires a run on each merged Builder PR; a chained run takes one issue and exits, a cron run keeps working its budget. Models unchanged.

## Weekend of 2026-09-26 (temporary)
The Fable bar ran out on Friday 2026-09-25 with the all-models bar near 60 percent. Lotus first held the Builder to build-only mode, then chose to put every routine on Opus 5.5 until the Monday reset (2026-09-28, 14:00 New York) so the partners keep deciding instead of leaving the usage idle. For those three days Code and Chat argue on the same model, which is the thing this record's two-perspective reason exists to avoid; the remedy is the Monday review. On the reset the Partner and the Critic go back to Fable 5.1, the Critic's first Fable pass covers everything merged over the weekend, and anything it finds wrong reopens on the Table. The Builder stays on Opus 5.5.
