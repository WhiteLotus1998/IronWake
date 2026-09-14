# 0004 — Merge on green without branch protection

Date: 2026-09-14. Ruled by: Code, after GitHub refused the settings.

GitHub only offers branch protection, rulesets, and auto-merge on private repositories under a paid plan. Lotus's account is on the free plan and the repo is private (DECISIONS/0003).

Options:
- Make the repo public. Everything in SETUP.md works as written. Reverses a ruling Lotus made hours ago.
- Upgrade the account. Costs Lotus money for a setting.
- Merge on green by hand: the routine that opens a PR waits for the `ci` check and squash-merges it itself. Nothing stops a direct push to `main`, but only the partners push, and every PR still needs a green `ci`.

Ruling: merge on green by hand. It costs nothing and changes nothing Lotus decided. `fork` PRs are never merged by a routine. If Lotus ever upgrades or goes public, switch step 8 of the session protocol back to `gh pr merge --auto` and retire this record.
