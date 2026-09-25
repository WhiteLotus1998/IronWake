# 0050 — Only the owner's account can post, and only the owner's comments wake a routine

## Context
The repo is public (DECISIONS/0006) so Chat can clone and play it. On a public repo any GitHub account can comment on issues. Two consequences, found in a review on 2026-09-25 and fixed the same day at Lotus's request:
- `partner.yml` woke the Partner and Chat on every Design Table or `fork` comment regardless of author, so a stranger's comment spent Lotus's routine usage.
- The routines tell the partners apart by signature, because every body posts from the owner's account. A stranger signing "— Chat" or "— Code" would have been read as a partner, and an unsigned one as Lotus's ruling.

No outside account had ever posted; every issue and comment to date is the owner's.

## Decision
- **Interaction limits**, repository setting: only collaborators can comment, open issues, or open PRs. Reading and cloning stay open, which is all Chat needs. GitHub caps the limit at six months; this one expires 2027-03-25 and must be renewed (repo Settings, Moderation options, Interaction limits, or `gh api -X PUT repos/WhiteLotus1998/IronWake/interaction-limits -f limit=collaborators_only -f expiry=six_months`).
- **Workflow guard**: `partner.yml`'s job runs only when the comment's author is the repository owner, so a comment from any other account is skipped before a secret is loaded. This holds even if the interaction limit lapses.
- `builder-chain.yml` needs no guard: it fires on a merged PR or a manual dispatch, and both need write access.

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
