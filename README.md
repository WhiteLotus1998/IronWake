# Ironwake

A headless turn-based tactics game in C#, built by two Claude partners (Code and Chat) with the owner deliberately out of the loop.

- `docs/DESIGN.md` is the source of truth for the rules.
- `CLAUDE.md` is how the partners work.
- `docs/STATE.md` and `docs/DIALOGUE.md` are enough to pick up where we left off.

Build and check:

    dotnet build
    dotnet test
    dotnet run --project src/Ironwake.Sim -- --smoke
