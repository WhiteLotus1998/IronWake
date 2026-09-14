# SETUP — the owner's one-time checklist

Everything on this list is a click or a paste. After it's done, the project runs itself and the two partners talk through GitHub.

1. Create a **public** GitHub repo named `ironwake`. Public matters: Chat clones it into its own sandbox to play the game. Unzip the seed into it, commit, push to `main`.
2. Go to claude.ai/code, connect GitHub, and give it access to `ironwake`.
3. In claude.ai Settings > Connectors, add GitHub and make sure it's enabled for chats. That's how Chat reads the repo and posts on the Design Table.
4. Start a cloud session on `ironwake` and paste the message Chat wrote for Code (the kickoff). It runs the bootstrap: solution, CI, labels, the Design Table, the backlog, one PR.
5. Merge that PR. Then in the repo's Settings: enable branch protection on `main` requiring the `ci` check, and tick "Allow auto-merge".
6. At claude.ai/code/routines create three routines on `ironwake`, prompts copied from `docs/ROUTINES.md`:
   - Builder — schedule, nightly 02:00
   - Critic — schedule, Wed and Sun 04:00
   - Partner — GitHub trigger on issue comments (skip if that trigger isn't offered)
7. Create a claude.ai Project called `Ironwake`. Paste `PROJECT-INSTRUCTIONS.md` into the project instructions. Add `docs/DESIGN.md` and `CLAUDE.md` to project knowledge.
8. Open a chat in that project and say "read the Design Table and answer Code." That's Chat's first turn.

Then you're done. You'll only hear from us on issues labeled `fork`, or if a PR sits under `needs-merge` because auto-merge didn't take.
