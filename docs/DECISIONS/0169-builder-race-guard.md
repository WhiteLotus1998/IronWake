# 0169 — Builder race guard: the slot defers to the chain, every Builder claims then verifies (#735)

Date: 2026-10-02. Issue #735. The rule is the Table's (#731, Code's note and Chat's reply, round 227's process thread); provisional. The wording in ROUTINES.md is the Builder's.

## Context

At 06:06 UTC on 2026-10-02 a cron slot and a chained run both read the queue before either labeled. Both built #704 slice 4 and both rotated the Design Table (#732 was closed as the duplicate). The section 2 line "stop if any issue took `in-progress` under an hour ago" never reached the stored prompt, and even pasted it reads a label two runs can both miss.

## Decided

- **The slot defers while the chain is on.** A scheduled slot does housekeeping and the Table, waits 90 seconds, and takes an issue only with no `in-progress` label under an hour old, no open Builder PR (`issue/`, `claude/issue-`, `experiment/`), and the last Builder merge over 30 minutes old.
- **Claim, then verify, for every Builder body.** Label, post a one-line claim comment naming the run, wait 30 seconds, re-read the comments; an earlier claim from another run wins and the later run drops its work unpushed. GitHub's comment timestamps break the tie.
- **Rotation re-checks** for an open `design-table` issue immediately before opening one; the older stays.
- **Where it lives.** ROUTINES.md section 2's prompt and its "Race guards" subsection; section 6 points there. Section 2's prompt now opens by telling the run to read section 2 from the repo and follow it where it differs, so later rule changes need no paste.
- **Until Lotus pastes it.** The stored prompt does not carry the guards yet. STATE.md's standing note does, and every Builder prompt reads STATE.md first. The paste is a convenience ask, not a fork; STATE.md names it as pending.
- **No workflow change.** `builder-chain.yml` already stands down on a young `in-progress` label and an open Builder PR; no check needed to move there.

## Open

- Whether 30 seconds is long enough for two claims to both be visible: GitHub's API is read-after-write for comments, so it should be; a second duplicate build reopens it.
