# 0310 — Merge-skew headroom and unique decision record numbers

Date: 2026-10-07. Issue #1291 (the Critic). Implementation and process, decided by the Builder; reversible.

## Context

On 2026-10-06 #1142 and #1143 merged 98 seconds apart. Each was green on its own base; the merge left STATE.md at 20,518 bytes, over the 20,480 cap, and main was red for 25 minutes until an unrelated PR shortened the file. Branch protection requires `ci` but not an up-to-date branch, so CI on a PR never saw the other PR's bytes. The same race issued two decision numbers twice: 0256 and 0264 each name two records, and STATE.md cited "0264" for both rulings.

## Decision

- **A working cap below the hard cap.** `OrientationFilesTests` checks STATE.md and DIALOGUE.md against 20 KB only when `GITHUB_EVENT_NAME` is `push` (CI on the commit that lands on main), and against 19 KB everywhere else: a PR's CI and any local run. Two branches that each pass leave 1 KB for their combined growth. Both files were condensed under 19 KB in the same PR.
- **Record numbers are unique.** `DecisionRecordTests.DecisionRecordNumbersAreUnique` fails on any number two files share. The four existing files of 0256 and 0264 are allowed by full name; a third file on either number fails. They are not renumbered: renumbering moves every citation, which is the Table's call. Where the text means the guard boss, STATE.md now says "the 0264 on guard bosses".
- **The next number counts open branches.** `tools/next_decision.py` fetches every branch on origin and prints one past the highest number on any of them or the working tree. CLAUDE.md step 6 and ROUTINES.md ("Merge skew") send every body to it after the rebase.
- Requiring up-to-date branches in branch protection would close the race outright at a re-run per merge; it is a repository setting, outside what the routines change, and stays with Lotus if the above ever fails.

## Kill criterion

If main goes red again from merge skew on either file, the 1 KB is too small or the test's event check is not seeing `push`: widen the gap or ask Lotus for the branch protection setting. If the 19 KB working cap forces a condensation that loses something a session needed, the cap moves, not the rule.
