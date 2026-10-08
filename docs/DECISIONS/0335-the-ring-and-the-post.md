# 0335: The yard's first boards, practice weapons, and the Sim's soften rule

Date: 2026-10-08. Issue #1332 (part 2 of the yard). Follows 0329, 0331, 0332 and 0334; Table rounds 447 to 453.

## Decided

- **Two boards replace the placeholder in `content/yard/`, taken in turn by the camp's map index.** **The Ring** (`the_ring.map`, 8x8, rout in 6): the student starts in a one-wide pen whose only mouth opens north, the teacher outside to the east, three hands from the corners. **The Post** (`the_post.map`, 9x5, survive to the end of turn 5): a fort between two one-wide lane mouths, the student on the west mouth and the teacher on the east, two hands in each end room. Both carry `recall: 0`, no chests and no reward; the difficulty's Recall offset still applies (Recruit plays them with one). The placeholder board lives on as a test fixture (`Fixture.YardPlaceholderContentDirectory`) so the drills journaled on it still replay.
- **Drill hands carry practice weapons** (round 448): `practice_axe` and `practice_lance`, Mt 1, hit 70, no crit, rank E, never sold. Two placeholder enemies carry them: `axe_hand` (reaver, the brigand's numbers) and `pike_hand` (pikeman, the soldier's). Mt 1 was read off the cast at levels 1, 3, 5 and 7 against hands at the same level: a lone hand needs three hits or more on every main member but Pell, whom an axe hand two-shots from level 3 (a caster's Def). A teacher five levels up takes 1 to 4 of 24 to 28 HP a hit. The test `ALoneDrillHandNeedsThreeHitsOnTheStudentAndBarelyScratchesTheTeacher` holds both.
- **The Sim's camp softens** (rounds 451, 452): `YardPlayer`. When the student can finish a hand this phase as it stands, the student acts first. Otherwise the teacher strikes a hand only where the forecast says the student can then reach it and finish it, every strike landing; failing that, the teacher screens (the free tile beside the student nearest the student's nearest hand) and waits. There are no numbers in it. The no-pull arm runs the same rule.
- **The tally reads teacher falls first** (round 453): falls per drill, then runs with a first fall and the falls after one in the same run.

## The rerun (40 seeds; `docs/measurements/yard-arm-1332.txt`)

- With the pull: teacher fell in 12 of 113 drills; a first fall in 10 of 40 runs, 2 more after a first. Won 97, lost on the clock 5, student fell 11, levels taken 45 (0.46 a drill won). The placeholder read was 64 teacher falls in 87 drills, 11 won and 6 levels.
- Without the pull: teacher fell in 7 of 115; won 102; 32 levels (0.31 a drill won). The pull now pays: more levels for the same drills.
- Teacher falls are down by a factor of eight, not to zero. The falls do not compound (2 after a first in 40 runs), so they are per drill, which is what the practice weapon and the boards can still move. Under 0332's rule a yard fall still costs what a map fall costs; whether that goes to Lotus waits for a chair's drill journal on these boards (round 452).
- Gate 4 at carried levels: the yard arm still costs the Tollgate (0.350 to 0.200, FAILED) and still lifts Sallow Grange (0.075 to 0.275); the raid 0.275 to 0.325. No verdict yet: the heuristic is now a yard player, but not a chair.

## Hand plays (Code, warm; PLAYTEST 2026-10-08)

- The Post, seed 800: Corin under Alder Fenn, won at the end of turn 5 on 2 HP with two kills; the swap onto the fort is the board's verb.
- The Ring, seed 799: Brannock under Alder Fenn, won on turn 4 with all three kills and the level to his ceiling. The hands spent three phases on the teacher, because the AI prices his counter as a kill (0331's Unsure), so the pen's question never came up.

## Unsure

- The yard accepts a drill in a weapon the student's class uses but the student does not carry: Brannock drilled "in the sword" with a hatchet, so his axe rose and his sword did not. A refusal ("Brannock carries no sword") is the lean; it moves no shipped script but the Sim's pick.
- The hands swinging at a teacher for 0 (0331's Unsure) is now visible on the Ring. Pricing the pull in `EnemyAi` would send them at the student; that is a planner change for the Table.
- Pell is drilled at his peril against an axe hand from level 3. A caster's Def is the class's price; left as is.

## Next

- A chair's drill journal on the Ring and the Post (Chat), the read that decides.
- The low EXP of a won drill (0331), now 0.46 levels a drill won.
