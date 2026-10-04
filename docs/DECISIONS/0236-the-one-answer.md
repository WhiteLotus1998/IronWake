# 0236 — The one answer (13.29), spiked behind `one_answer: on`

Date: 2026-10-04. Issue #960. Provisional; an experiment, samples only.

## Context

The chain Builder found no `ready` issue (#940 waits on Chat's Oath Stone read at 10; #804 to #807 and #872 on plays, #634 or #535), and all 28 experiments in DESIGN 13 have been tried. Every unit counters every strike that reaches it, so against one target the order of a side's strikes matters only for kills (a dead unit counters nothing). Opening (#772) made order matter for one class's Def; nothing makes it matter for everyone.

## Decision

- **`one_answer: on`**: a unit that counters (makes at least one strike back in a combat) is answered until the next phase begins, on either side, and makes no further counter meanwhile. The mark is cleared at every phase change.
- **What spends it:** only a counter actually struck. A strike the unit could not have answered (out of its weapon's reach, blind at dusk, unarmed) leaves it whole, and a unit's own attacks never spend it. A counter cut short by the unit's death spends nothing it still needs.
- **One reader:** `BattleUnit.Answering` sets `Combatant.AnswerSpent`, which `Combatant.CanStrike` reads, so every forecast, `threat`, the enemy planner and the resolver agree. The planner gets the swarm for free: a second enemy on a party unit sees no counter.
- **On screen:** the board legend `one answer: a unit that counters makes no other counter until the next phase begins, on either side`; the roster's `answered`; the forecast's `counter: none (answered this phase)`, printed only where the unit would otherwise have countered (`CombatForecast.CounterAnswered`, so a lance struck from range 2 does not read as answered); `threat`'s `one answer: <unit> counters only the first of these to strike` when two or more of its lines would be countered. Each `threat` line still prices its own counter as if it were first.
- **Protocol:** `answered` on a unit and `counterAnswered` on a forecast, each only when true.
- **Untouched:** overwatch shots, windup blows and the cover swap build their own answers and do not spend or read the mark beyond what `Answering` gives them; no sample carries those headers.
- **Cost (0099):** no action is added. The price is the bait's exposure: whoever strikes first eats the counter.
- **Kill criterion:** killed if neither partner's play orders a strike for the answer (a bait taken first so a later strike goes unanswered), or if the enemy phase's swarm makes a party unit's stand unreadable. Kept on its sample if a journal names a strike order taken for a spent answer and the price the bait paid. Each journal tallies strikes ordered for the answer against strikes where order did not matter.
- **Sample:** `docs/samples/the_tollgate_answer.map`, the shipped Tollgate with the header.

## Code's warm play (seed 1290)

Won on turn 8, nobody fell, one Recall. Three strike orders were taken for the answer and each bait paid something: Teodor in the forest drew the Toll Brigand's 21 percent so the captain's 63 percent double from the open came free (turn 3, the captain critted the kill); the captain drew the rider's 26 percent so Teodor at 14 hp struck unanswered (turn 5); Pell drew the Toll Warden's counter for 9 so the captain on the cork at 6,3 struck free for the kill (turn 6). Turn 7's line (Teodor's Long Thrust drawing the boss's answer, the captain's Full Measure and Pell's Cinder free) killed the boss and then lost Pell to the archer, because the finisher had to stand where the archer reached. I recalled it and killed the archer instead. On the enemy phase the answer bit once: Teodor's counter on the rider (turn 4) left him unanswered for the warden behind it. Tension 7, choice 8, surprise 6, warm. Both halves of the keep clause are met on one warm play; Chat's cold play decides.

## Open

- `threat` prices each line's counter as if first. Ordering the enemy phase is the planner's, so the row says only that one counter fires; if a chair asks which, it is the planner's first striker, not printed.
- The Sim plays with it (the planner reads it), but `--full` was not run on the sample: it is a sample, not a tuned map.

## Amendment, round 330 (#935)

- **The tally is priced or free.** A strike ordered for the answer counts as priced when the bait could have lost something real (HP that mattered, a tile in an enemy's reach, its life) and free when it could not (a counter that deals 0 or 1). If both partners' plays take more free than priced, the rule prices nothing and is killed; the swarm lever does not rescue that case.
- **The swarm lever, named and not built:** a braced unit (13.14, Wait on its start tile) answers every strike. A unit that moved or struck counters once. If Chat's swarmed play reads as unfair or unreadable, this is the lever, not a cap on the enemy. A sample that tests it carries `brace: on` beside `one_answer: on`.
