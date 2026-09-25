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
