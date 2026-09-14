# 0001 — Founding decisions

Date: 2026-09-14. Ruled by: Chat Claude, under Lotus's standing grant to decide without him.

## C# / .NET, headless, no engine
For: Claude Code verifies itself with tests; no screen needed; C# ports into Unity (owner knows it) or Godot later.
Against: the owner's long-term engine is C++, so a C++20 core would be reusable there directly.
Ruling: C#. The rules are the portable part; porting formulas is cheap, porting a flaky unattended C++ toolchain is not.

## Immutable state + command/event pipeline
For: rewind and replay become trivial; every bug is a seed plus a command list; the Critic can reproduce anything.
Against: more allocation, a little more ceremony.
Ruling: immutable. Performance gate 7 guards the cost.

## No weapon triangle; Breaker abilities instead
For: matches the 3H feel the owner named; makes ability choice matter.
Against: less readable rock-paper-scissors for a new player.
Ruling: no triangle in v1. Provisional — revisit if gate 2 shows decisions not mattering enough.

## Two-roll hit, one-roll crit
For: displayed hit feels honest to players; crit stays scary.
Against: mathematically the displayed hit is not the true probability.
Ruling: two-roll hit. Pillar 2 is about the forecast matching the resolver, which it does; the forecast shows the displayed number and the resolver uses the same rule.

## Builder + Critic as separate routines
For: a fresh-context critic catches what the builder rationalized.
Against: costs daily routine runs.
Ruling: separate. Critic twice weekly; reduce to weekly if the Builder starves.

## Auto-merge on green
For: without it, nothing progresses unattended.
Against: a bad merge lands without a human.
Ruling: allowed, gated by CI + Sim smoke + branch protection. `fork` issues never auto-merge.
