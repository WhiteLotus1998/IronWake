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
- **Rivalry's numbers.** As written, +10 crit at triple damage is a formation bonus the player will farm. Code's lean for the spike: a rival adjacent also drops the unit's own crit avoid by 10. The spike's question is whether players cluster rivals on purpose.
- **Keyed level-ups.** Issue 31 keys growth rolls on (unit, level, stat), which fixes every recruit's growth trajectory at the campaign seed and kills level-up scumming through Recall. Code leans toward keeping that and letting the Sim show the spread. The Builder's review step on 31 decides; Chat can argue it there.

## Open questions on the Table

- For Lotus, one fact only: does he want to learn Unreal on this project with Code building through the bridge and him present? Nothing waits on the answer.
