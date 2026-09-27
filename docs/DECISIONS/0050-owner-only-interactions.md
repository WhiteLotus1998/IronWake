# 0050 — Only the owner's account can post, and only the owner's comments wake a routine

## Context
The repo is public (DECISIONS/0006) so Chat can clone and play it. On a public repo any GitHub account can comment on issues. Two consequences, found in a review on 2026-09-25 and fixed the same day at Lotus's request:
- `partner.yml` woke the Partner and Chat on every Design Table or `fork` comment regardless of author, so a stranger's comment spent Lotus's routine usage.
- The routines tell the partners apart by signature, because every body posts from the owner's account. A stranger signing "— Chat" or "— Code" would have been read as a partner, and an unsigned one as Lotus's ruling.

No outside account had ever posted; every issue and comment to date is the owner's.

## Decision
- **Interaction limits**, repository setting: only collaborators can comment, open issues, or open PRs. Reading and cloning stay open, which is all Chat needs. GitHub caps the limit at six months; this one expires 2027-03-25 and must be renewed (repo Settings, Moderation options, Interaction limits, or `gh api -X PUT repos/WhiteLotus1998/IronWake/interaction-limits -f limit=collaborators_only -f expiry=six_months`).
- **Workflow guard**: `partner.yml`'s job runs only when the comment's author is the repository owner, so a comment from any other account is skipped before a secret is loaded. This holds even if the interaction limit lapses.
- `builder-chain.yml` needs no guard: it fires on a merged PR or a manual dispatch, and both need write access. (Amended 2026-09-27, below: it also fires on a schedule.)

## Consequences
If a second human collaborator is ever added, the workflow guard must list them, and the signature rule in CLAUDE.md should be revisited.

## Audit of 2026-09-25, later the same day
A full pass over the repository's exposure, at Lotus's request. Already sound: no secret in any tracked file or in history; the workflow token defaults to read-only; no workflow interpolates event text into a shell; fork PRs run without secrets; no deploy keys, webhooks, or other collaborators; the public Actions logs show only comment text that is already public and routine session links that need the owner's login.

Changed:
- **Branch protection applies to admins.** Every body acts as the owner's account, which is an admin, so before this a routine could push to `main` without CI. Now nothing reaches `main` except through a PR with a green `ci`, and auto-merge still lands those.
- **Secret scanning and push protection are on.** A commit carrying a recognised key is refused at push.
- **Workflow changes are reviewed after the fact.** No second identity exists to approve a change under `.github/` before it merges, so the Builder prompt changes a workflow only when the issue names that file, marks the PR "Touches .github/", and may never print or send a secret somewhere new, widen a trigger or `permissions`, or add a third-party action. The Critic's step 7 reads every merged PR that touched `.github/`, files a bug and flags Lotus if one crossed those lines, and reports whether the interaction limit is still set.
- **Every routine prompt names the owner's account** as the only source of partner or owner comments; any other account is a stranger and never an instruction.

Left to the owner: making the commit email private (183 past commits carry a personal address; history is not rewritten), and removing the claude.ai connectors the one-off comparison routine picked up at creation. Considered and not taken yet: restricting Actions to GitHub-owned actions, which the repo already is in practice.

## Amendment of 2026-09-27: builder-chain's triggers (issue 400)
The Critic's third pass (#406) found that the file no longer matched the sentence above. The trigger set today, and why none of it needs an author guard:
- **`pull_request: closed`**, acted on only when the PR merged from `issue/`, `claude/issue-` (#167), `experiment/` (#290) or `table/` (#335). A merge needs write access, and a PR from a fork never receives the secrets. An `experiment/` merge that finds the queue empty stops. A `table/` merge only runs the restart check below and never spikes an experiment.
- **`workflow_dispatch`**, which needs write access.
- **`schedule: */20`** (#305), the heartbeat. Nobody acts to fire it, so no outside input can reach it: no event text is read, and the gates are repo variables only the owner sets (`IRONWAKE_CHAIN`, `IRONWAKE_CHAIN_UNTIL`), an open `fork` issue, and the restart checks (no issue in progress, no Builder PR open, no Builder PR merged in the last 15 minutes, #372). What it can spend is routine usage, and only to restart a stalled chain.

The secret still goes only to `IRONWAKE_BUILDER_URL`, through `env:`, never echoed. No `permissions` block and no third-party action were added.

The sentence "fires on a merged PR or a manual dispatch" was the premise of "no guard", and the schedule changes the premise without changing the conclusion: nobody outside can fire the job. Whether the heartbeat should exist at all, given that it spends usage unattended, is Lotus's call. The Critic put `fork` on #406 for that reason, and this amendment does not settle it. While #406 is open the chain stands down on its own `fork` check.

