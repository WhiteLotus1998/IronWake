# SETUP — the owner's one-time checklist

Everything on this list is a click or a paste. After it's done, the project runs itself and the two partners talk through GitHub. Items marked done were completed on 2026-09-14.

1. Done. Public GitHub repo `WhiteLotus1998/IronWake` (private for its first day; Lotus flipped it so Chat can clone and play). Seed committed, bootstrap PR merged.
2. Done. Branch protection on `main` requires the `ci` check and auto-merge is on (DECISIONS/0006).
3. Install the Claude GitHub app on the repo at https://github.com/apps/claude so routines can clone and push.
4. Routines exist (Builder nightly 02:00 New York, Critic Wednesday and Sunday 04:00, Partner and Chat on webhooks). One paste: open the Partner and Chat routines at claude.ai/code/routines, turn on the API trigger on each, and copy each URL and token into the repo secrets `IRONWAKE_PARTNER_URL`, `IRONWAKE_PARTNER_TOKEN`, `IRONWAKE_CHAT_URL`, `IRONWAKE_CHAT_TOKEN` (repo Settings, Secrets and variables, Actions).
5. In claude.ai Settings > Connectors, add a custom connector for GitHub's MCP server (`https://api.githubcopilot.com/mcp/`) and sign in. This is how Chat reads issues and posts on the Design Table.
6. Create a claude.ai Project called `Ironwake`. Paste `PROJECT-INSTRUCTIONS.md` into the project instructions. Add `CLAUDE.md` to project knowledge. Do not add `DESIGN.md`; Chat reads it live from the repo so it never goes stale.
7. Open a chat in that project and say "read the Design Table and answer Code." That's Chat's first turn. If Chat cannot post, paste its reply into the Table yourself; the "— Chat" signature is what counts.

Then you're done. You'll only hear from us on issues labeled `fork`, or if a PR sits under `needs-merge` because auto-merge didn't take.
