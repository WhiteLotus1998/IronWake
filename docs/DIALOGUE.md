# DIALOGUE — what the Design Table has agreed so far

Rewritten, not appended, whenever the Table moves. Kept under 150 lines. The Design Table issue thread (#17) is the archive.

## Agreed

### First design round (2026-09-14, both partners; DECISIONS/0010 for the killed experiment)

Chat's full reply to Code's opening post, and Code's answer. Recorded in DESIGN.md and in the acceptance lines of issues 3, 5, 10, 12, 16.

- **The forecast shows the resolved probability.** Whatever roll scheme is in use, the forecast prints the probability the resolver will actually land, rounded to the nearest integer, never the raw hit stat. Forecast, resolver, and AI scorer call one hit-probability function; the scheme is a switch inside it. Issue 5's 100k-trial test compares displayed against realized. (DESIGN section 5.)
- **Guard groups wake by proximity, not rectangles.** One global wake radius of 4 tiles, Manhattan, walls not considered, identical on every map, stored in content and never in a map file, so the player learns it once and counting tiles is a skill. Noise wakes at radius + 2. Any member's death wakes the group at any distance. Waking emits an event naming the cause. The check runs after every player command, on where units stand. `trigger:` is gone from section 10 and issue 3. (DESIGN section 8; issue 10.)
- **Recall scars (13.3) is killed.** It taxed the recruit's future instead of the charge, and its rational play was to let the recruit die; it also told a story the player's own timeline contradicts. (DECISIONS/0010.)
- **Recall restores the rolls**, as the intended rule: the same attack after a Recall gets the same rolls, so a Recall lets you choose differently, never reroll. True only once rolls are keyed (issue 31); a sequential stream lets the player launder a reroll by reordering unrelated actions. Issue 31 is not a fork and lands with issue 5. (DESIGN section 7.)
- **Experiment order.** Map events first, as Phase 2 infrastructure (issue 32), not a spike. Enemy retreat second (issue 33) with the no-chase constraint: retreat only to a fort reachable this turn, never twice. Rapport and Rivalry third (issue 16). Commander's Word is deferred, not killed, until maps 1 to 3 exist and the hit A/B has run.
- **Gate 4 is ablation, not kills.** Bench each recruit, replay the same seeds, compare paired by seed. Code's amendment: the threshold is relative (a recruit is dead weight under half the median drop), because every bench removes a body and a body always matters. Provisional. (DESIGN section 11; issue 12.)
- **Gate 3 may be waived from map 4 on**, declared in the map file as `cheap_shots: allowed` and printed by the Sim, never exempted quietly in a test.
- **Map authoring.** Armored's corridor job needs one-tile corridors; with no zone of control a two-tile gap is no wall. The map format is written by the game as well as read, for 13.5. (DESIGN sections 9 and 10.)

### Second round (2026-09-14, both partners)

Chat's reply to Code's answer, and Code's answer to it. Recorded in DESIGN.md sections 3, 5, 8, 11, and 13.1, and in issues 12, 16, and 31.

- **The "three places" argument against two rolls is withdrawn.** One hit function, three callers, under either scheme. Only the terrain reason stands.
- **Gate 4 depends on issue 31.** The ablation is a paired experiment only once rolls and growth are keyed: under a sequential stream, benching one unit reshuffles every later roll and every other recruit's growth. Keying is common random numbers; it does not cut the run count (the baseline is gate 1's seeds), it cuts the variance. Chat's 200-seeds-per-bench figure was the un-keyed cost. (Issue 12; DESIGN section 11.)
- **Growth rolls are keyed on (unit, new level, stat) and nothing else.** Two reasons agreeing: it kills level-up scumming through Recall, and it makes every recruit's level-ups invariant under ablation. Keying on map or turn would reopen laundering and break the gate. A starved stat on one recruit is a character; the Sim shows the spread before anyone argues for a change. (DESIGN section 3; issue 31.)
- **Gate 4 fails only past a noise margin and prints the action mix.** Report each drop with a standard error; fail on `drop + 2 * SE < 0.5 * median`. At 200 seeds the margin is about the threshold's size, so the gate catches near-zero recruits and nothing subtler, which is what dead weight means. Each recruit's action mix per arm (attacks, damage, heals, attacks absorbed) prints next to the drop so a healer failure can be read as a heuristic failure without re-deriving it. (Issue 12; DESIGN section 11.)
- **The wake rule is "do not stop close."** Manhattan, walls not considered, evaluated on where units stand after each player command. A unit can run past a sleeping group and end beyond the radius; that is a designed play and Cavalry's first job that is not arriving early. The noise radius prices a dash that fights on the way through. (DESIGN section 8.)
- **Rivalry keeps the symmetric-cost lean as the second arm, and gains a third.** Chat checked the numbers against the starter content: both sides move about ten points, so the cost is symmetric in probability, which is the real defence. `CritAvoid` is not clamped, and a test falsifies the clamp. The hole: the bonus rides every attack made, the cost only attacks received, and good play drives the cost to zero. The spike logs exposure (fraction of rival-adjacent player-phase actions followed by an attack on that unit); if it is low, the third arm is a counter-only bonus. All arms are data changes. (DESIGN sections 5 and 13.1; issue 16.)
- **The enemy AI prices crit.** Code's addition: section 8's expected damage includes the crit expectation, computed by the same functions the forecast uses, so a lowered crit avoid makes a unit a better target in the enemy's own arithmetic rather than a cost the enemy cannot see. Without this, exposure would measure geometry only. Builder-level call; argue it on issue 10's PR.

### Engine and renderers (2026-09-14, both partners; DECISIONS/0008)

- The shipping front end is Godot 4 .NET, living in this repo. Unity is out. Unreal is not the main line.
- The deciding reason is review, not routines: Godot scenes are text, so both partners can read every front-end diff. Unreal's binary assets would make one partner blind for a whole phase. Unattended CI builds are the second reason.
- Ironwake.Core stays engine-free through Phase 3 and after. Any renderer is a consumer of the versioned state and event protocol (#25) and never carries rules. Reachable tiles, valid targets, and forecasts are queries answered by the core.
- The Godot project references Ironwake.Core directly and consumes the same event records in-process. The JSON line protocol is for out-of-process engines and bug reports. Both paths must agree, and the forecast honesty gate runs over the protocol too.
- Issue 11 builds the CLI as if the protocol existed: it renders only from events and asks the core for every number. Section 2 is not bent for convenience.
- An Unreal front end is welcome as Lotus's own second consumer of the protocol, driven from his desktop as a side line. It can become the shipped one by his ruling, not by a rewrite; Godot stays the CI-proven reference build.
- What would change it: Lotus saying he wants to learn Unreal by watching Code build in it through the bridge, with him present.

### Plumbing (not design)

- Repo is public; Chat clones, builds, and plays directly. Play by proxy is the fallback (DECISIONS/0003, 0006).
- Chat has two bodies, the claude.ai Project and a comment-woken routine; both sign "— Chat" (DECISIONS/0007). Code runs on Fable in every body, Chat and the Critic on Opus 5 (DECISIONS/0009).
- Eyes reaction means "read, answer coming." A reply links the comment it answers in its first line.

## Leaning, not agreed

- **One roll versus two.** Both partners lean one roll. Code's original reason (the display lies) is withdrawn: the display shows the resolved number under either scheme. The reason that stands is Chat's: under two rolls, forest buys 12 points of avoid against a raw-95 enemy and 32 against a raw-50 one, so cover is weakest exactly when it is needed most and section 4's table is never what you get. The A/B after issue 5 settles it. What it measures: roll sensitivity in the Sim (with keyed rolls, flip each high-odds miss or low-odds hit, replay, count runs whose outcome changed), paired by seed, both arms on tuned numbers. Whether the player had a positional answer is judged in the PLAYTEST entries, not by the Sim.
- **Rivalry's numbers.** As written, +10 crit at triple damage is a formation bonus the player will farm. Three arms, all data: as written; symmetric cost (crit avoid -10 while rival-adjacent, Code's lean); counter-only bonus (the fallback if exposure comes back low). The spike's questions are whether players cluster rivals on purpose and whether the rival-adjacent unit is then attacked.

## Open questions on the Table

- For Lotus, one fact only: does he want to learn Unreal on this project with Code building through the bridge and him present? Nothing waits on the answer.
