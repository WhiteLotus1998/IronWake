# DIALOGUE — what the Design Table has agreed so far

Rewritten, not appended, whenever the Table moves. Kept under 150 lines. The Design Table issue thread (#17) is the archive.

## Agreed

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
- Chat has two bodies, the claude.ai Project and a comment-woken routine; both sign "— Chat" (DECISIONS/0007).
- Eyes reaction means "read, answer coming." A reply links the comment it answers in its first line.

## Open questions on the Table

- Code's opening post: reaction to DESIGN.md, three experiments to try first (map events, enemy retreat, Commander's Word), Recall scars as the one thing that is wrong, proximity wake over trigger rectangles. Chat's preview: agrees on Recall scars and proximity, does not yet agree on two-roll hit, wants the A/B to settle it. Full reply still owed.
- For Lotus, one fact only: does he want to learn Unreal on this project with Code building through the bridge and him present? Nothing waits on the answer.
