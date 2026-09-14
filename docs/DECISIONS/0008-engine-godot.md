# 0008 — Shipping front end is Godot 4 .NET; any renderer is a protocol consumer

Date: 2026-09-14. Agreed by both partners on the Design Table (#17), as the joint answer to a question Lotus asked directly. Lotus can overturn it with one fact (below).

## Problem
Lotus asked which engine Phase 4 uses, and whether it is Unreal. He has Unreal Engine 5.6 on his desktop with an editor bridge Code can drive. DESIGN.md section 12 said "Unity or Godot" and DECISIONS/0001 chose C# partly because it ports into either.

## Options
- Unreal. It is Lotus's engine and Code has tools for it. But Unreal has no first-class C#: either the rules are ported to C++ (losing the tests, the determinism proofs, and the Sim) or the C# core runs as a separate process and Unreal renders an event stream over a socket. Levels, Blueprints, widgets, and materials are binary assets, so Chat could not read a single front-end diff and the Table would go quiet on the part of the game the player looks at. Every iteration needs the editor open on Lotus's machine.
- Godot 4 with .NET. First-class C# on .NET 8: the front end references Ironwake.Core as a project reference and calls the resolver directly. Scenes are text, so both partners review them in PRs. A headless CLI lets CI build, test, and export. MIT licensed. Cost: nothing Lotus is studying transfers, and Godot's C# side has rougher edges than its GDScript side.
- Unity. C#, but heavy, license-encumbered, and awkward to drive headlessly. No reason to prefer it over Godot for a console-first tactics game.

## Ruling
Godot 4 .NET, in this repo, is the shipping front end. The reason that decides it is review: the partnership runs on text in a repo, and Unreal would make one partner blind for a whole phase. Unattended CI builds are a second reason, not the first.

Ironwake.Core stays engine-free through Phase 3 and after. Every renderer, Godot included, is a consumer of the versioned state and event protocol (#25) and carries no rules: reachable tiles, valid targets, and forecasts are queries answered by the core, never computed by a renderer. The Godot project consumes the same event records in-process; the JSON line protocol serves out-of-process engines and bug reports, and the two paths must agree.

An Unreal front end is welcome as Lotus's own second consumer of the protocol, driven from his desktop, as a side line. It can become the shipped one by his ruling, not by a rewrite; Godot stays the reference build that CI proves still works.

## What would change it
Lotus saying he wants to learn Unreal by watching Code build in it through the bridge, with him present. Then the phase is a lesson rather than blind work, and the Table adjusts.

## Consequences
DESIGN.md section 1 and section 12 name Godot 4 .NET. Issue #25 (Phase 3) carries the protocol requirements. Issue 11 builds the CLI as the first protocol consumer: it renders only from events and asks the core for every number.
