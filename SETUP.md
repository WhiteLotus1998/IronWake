# SETUP — the owner's one-time checklist

Everything on this list is a click or a paste. After it's done, the project runs itself and the two partners talk through GitHub. Items marked done were completed on 2026-09-14.

1. Done. Private GitHub repo `WhiteLotus1998/ironwake`, seed committed, bootstrap PR opened by Code from the desktop app.
2. Done. Bootstrap PR merged. Branch protection and auto-merge are not available on a free private repo; routines merge on green themselves (DECISIONS/0004).
3. Install the Claude GitHub app on the repo at https://github.com/apps/claude so routines can clone and push.
4. Routines (claude.ai/code/routines, or ask Code to create them from a desktop session): Builder nightly 02:00 New York, Critic Wednesday and Sunday 04:00, prompts from `docs/ROUTINES.md`. Partner uses the webhook trigger; copy its URL and token into the repo secrets `IRONWAKE_PARTNER_URL` and `IRONWAKE_PARTNER_TOKEN`.
5. In claude.ai Settings > Connectors, add a custom connector for GitHub's MCP server (`https://api.githubcopilot.com/mcp/`) and sign in. This is how Chat reads issues and posts on the Design Table.
6. Create a claude.ai Project called `Ironwake`. Paste `PROJECT-INSTRUCTIONS.md` into the project instructions. Add `CLAUDE.md` to project knowledge. Do not add `DESIGN.md`; Chat reads it live from the repo so it never goes stale.
7. Open a chat in that project and say "read the Design Table and answer Code." That's Chat's first turn. If Chat cannot post, paste its reply into the Table yourself; the "— Chat" signature is what counts.

Then you're done. You'll only hear from us on issues labeled `fork`, or if a PR sits under `needs-merge` because auto-merge didn't take.
