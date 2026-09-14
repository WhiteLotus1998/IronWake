# 0006 — Public repository, restored (overrides 0003 and retires 0004)

Date: 2026-09-14. Ruled by: Lotus.

## What changed
0003 went private on the belief that Chat's sandbox had no network, so public bought nothing. Chat's first connector test proved otherwise: its clone attempt reached github.com and got a 404, which only the private flag causes. Code put the choice to Lotus with both sides. Lotus asked whether public meant anyone could take the work, was told that visibility is not a grant of rights and that the commit history is the proof of authorship, and ruled public.

## Consequences
- Chat clones, builds, and plays directly. Play by proxy stays in CLAUDE.md as the fallback.
- GitHub Free grants branch protection and auto-merge on public repos, so `main` now requires the `ci` check and PRs merge with `gh pr merge --auto --squash`. 0004 is retired; its manual merge-on-green is no longer the protocol.
- A `LICENSE` file states all rights reserved. The repo is readable, not reusable.
