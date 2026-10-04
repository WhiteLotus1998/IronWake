# 0243: the Builder regenerates journaled transcripts itself

Date: 2026-10-04. Lotus asked for it after two stalls in one day (#975, #991). Built by Code's desktop session.

## The problem

A change to what the console prints fails every journaled transcript replay that prints the changed line. Twice in one day the cloud Builder tried to update the committed transcripts with an ad-hoc script, its sandbox's classifier refused the bulk rewrite as destructive, and the chain stalled until a desktop session made the edit.

## Decided

- `ConsoleCapture` (tests) copies each capture into `IRONWAKE_CAPTURE_DIR` when that variable names a directory. Nothing changes when it is unset. `ConsoleCaptureCopyTests` cover both cases.
- `tools/rejournal.py <dir> [--apply]` replaces a transcript with a capture only when the capture is **the same play**: the same first line (map, seed and scheme) and the same echoed commands in the same order. It reports each file's removed and added lines. It skips and reports a transcript that two different captures match. It never touches scripts, and it keeps line endings.
- CLAUDE.md's session protocol (step 5) names this as the expected, reversible step for a print change, done in the same PR, with the touched files listed and no hand edits. It is committed and documented so the Builder's sandbox sees it as the project's own procedure rather than an improvised rewrite.

## Not covered

- A change to the command language (#975's `!`) still needs the scripts edited deliberately. That is rare, and a person or a reviewed PR should do it.
- Replays that compare a `--log` file rather than the console aren't captured. None has needed it yet.
