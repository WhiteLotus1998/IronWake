# 0332: Gate 4's yard arm and the pull's ablation

Date: 2026-10-08. Issue #1331, slice 3. Follows 0329 and 0331; Table rounds 447 to 449.

## Decided

- **The yard arm is a Sim measurement, `--yard [--seeds N] [--map <id>]`, wired to no exit code.** `LevelRun.Measure` takes a yard arm. Under one, each camp after the levy drill fights one drill (`YardRun.Camp`). The student is the lowest-level unit standing. The teacher is the highest-level unit the record's own `YardRefusal` lets teach it. The weapon is the student's main weapon where the teacher uses it, otherwise the first weapon both use. Roster order breaks ties. The drill is the shipped `BeginYard` on the pool's board in turn, played by `HeuristicPlayer`, with its result applied through `AfterYard`. Permadeath stays off, as `Measure` runs it.
- **Gate 4 reads it at `carried` levels.** Gate 4 is per board, so the arm uses `--curve`'s `carried` method: the file's party at the levels each arm's campaign brings to the camp, slot by slot. It prints gate 1 and gate 4's headline on every campaign map under three arms over the same seeds: no yard, the yard, and the yard with the pull off. It also prints each arm's drill tally.
- **The pull's ablation is a flag only the Sim sets.** `YardHand.Pulls` defaults to on, and `Combatant.Pulls` reads `Teaches && Pulls`. No command, file or record sets it off (round 448: never a player option).
- **The camp-screen row and its click parity are a later slice.** The row needs the Godot client's camp screen, and parity needs a Godot run, which this routine's sandbox cannot do.

## The first read (40 seeds, placeholder board; `docs/measurements/yard-arm-1331.txt`)

- Drills with the pull: 87 fought, 11 won, 36 lost on the clock, 40 student falls and 64 teacher falls. Students took 6 levels across all of them.
- With the pull off: 103 fought, 60 won, 7 lost on the clock, and 3 levels taken.
- The heuristic never softens on purpose. Under the pull its teacher strikes, leaves hands at 1 HP, and the hands keep swinging until the clock runs out. A wounded teacher (permadeath off) then misses the next map's yard and carries the wound.
- Carried levels: the yard arm costs the captain a level at some camps (maps 4, 6, 7). Gate 4's median drop moves: the Tollgate 0.350 to 0.200 (FAILED), the raid 0.275 to 0.175, Sallow Grange 0.075 to 0.300. The no-pull arm matches no yard on the Tollgate and the raid, and matches the yard on Sallow.
- Read: under the heuristic the yard is a net cost. It is the floor of what the yard pays, not what a chair who softens and hands kills would see. The heuristic is not a yard player, so these numbers cannot tune the pull, the practice weapon or the boards.

## Unsure

- Whether the heuristic should learn to soften (the teacher strikes only where the student can finish this phase) before the arm is read again. The lean is no: a chair's drill journal on #1332's boards first, since a planner rule tuned on the placeholder would be tuned on the wrong board.

## Next

- #1332: the practice weapon, the ring and the post. Then rerun `--yard`.
- #1331: the camp-screen row with the client's click parity.
