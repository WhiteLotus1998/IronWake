# 0003 — Private repo, how Chat talks and plays, how the Partner wakes

Date: 2026-09-14. Ruled by: Lotus (private); the rest by Code after checking what the platform actually offers.

## Private repository (overrides 0002)
0002 chose public so Chat could clone and play. Chat's claude.ai sandbox has no network, so it cannot clone regardless, and everything else (routines through the Claude GitHub app, Chat through the GitHub MCP connector) works on a private repo. Lotus ruled private. Flip to public later if there is ever a reason.

## Chat posts through the GitHub MCP connector, or Lotus pastes
The claude.ai GitHub connector reads files only. Chat uses GitHub's own MCP server as a custom connector to read issues and post comments. If that fails, Chat writes the post and Lotus pastes it. The signature decides who is speaking: anything signed "— Chat" is Chat, whatever account posted it. Only an unsigned comment from the owner's account is a ruling.

## Play by proxy
Chat cannot build or run the game. Code attaches the full transcript of every journaled play under `docs/transcripts/`, and runs any seed plus command list Chat posts on the Table, replying with the transcript. Chat's Fun Gate scores come from those transcripts. Revisit if Chat's environment ever gains network access.

## Partner routine wakes through a webhook
Routines trigger on a schedule, on pull request and release events, or on a webhook. There is no issue-comment trigger. `.github/workflows/partner.yml` forwards Design Table comments to the routine's webhook; it needs two repo secrets and does nothing without them.

## Bootstrap ran locally, not in the cloud
Lotus's machine has .NET 9 and the GitHub CLI, so Code ran the bootstrap from the desktop app. Nothing about the repo depends on where it was run.
