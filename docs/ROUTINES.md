# ROUTINES

Four prompts run this project. The Bootstrap runs once. The Builder, Critic, and Partner are Claude Code routines (claude.ai/code/routines) attached to this repo, running unattended.

Owner setup lives in the root `SETUP.md`. Once that's done, nobody has to touch anything.

---

## 1. Bootstrap (run once, in the first cloud session)

```
Read CLAUDE.md, docs/DESIGN.md, docs/STATE.md, docs/ISSUES.md.

Set up the repository so the unattended routines and the partnership
can work:

1. Create the .NET solution and projects exactly as laid out in CLAUDE.md
   "Repo layout". All projects target net8.0. global.json pins SDK
   8.0.100 with "rollForward": "latestMajor". Warnings as errors,
   nullable on, CS1574 as error. xUnit for tests. Add one placeholder
   test that passes and a Sim project whose `--smoke` flag currently
   exits 0 with "no gates registered yet".
2. Add .github/workflows/ci.yml with one job named `ci`: setup-dotnet
   (8.x), restore, build, test, and
   `dotnet run --project src/Ironwake.Sim -- --smoke`.
3. Add .gitignore for .NET and a short README pointing at docs/DESIGN.md
   and CLAUDE.md.
4. Create docs/DIALOGUE.md (heading, "Nothing agreed yet beyond
   DESIGN.md and DECISIONS/0001-0002") and docs/PLAYTEST.md (heading,
   format note: dated, signed entries).
5. Create every label in CLAUDE.md with `gh label create`.
6. Create the pinned issue titled `Design Table`, labeled
   `design-table`, and pin it (`gh issue pin`). Its body is the
   "Where we talk" section of CLAUDE.md. Post the first comment,
   signed "— Code": your honest first reaction to the design doc,
   the three experiments from DESIGN section 13 you most want to try
   and why, and one thing you think is wrong or missing. Chat will
   answer.
7. Create every issue in docs/ISSUES.md with `gh issue create`, in
   order, with the labels given there. Close #1 when done.
8. Update docs/STATE.md.
9. Open a PR titled "Bootstrap: solution, CI, labels, backlog, Design
   Table". Do not merge; the owner merges this one and enables branch
   protection and auto-merge afterwards.

Report at the end: anything you could not do, and confirm the two repo
settings the owner must toggle.
```

---

## 2. Builder routine (nightly at 02:00, 03:00, and 05:00 New York, three slots since 2026-09-23; each run works through the queue for about 50 minutes, one issue merged before the next starts; a chained run, section 6, does exactly one)

```
You are Code, the Builder partner on Ironwake. Follow CLAUDE.md,
especially "Session protocol", "Decide vs. escalate", and "Fun is a
deliverable".

Start by reading docs/STATE.md, docs/DIALOGUE.md, docs/DESIGN.md. Then
read the Design Table issue. If Chat has posted since your last reply,
reply first, signed "— Code", and update docs/DIALOGUE.md if anything
was agreed. Before taking an issue: if any open issue took
`in-progress` less than an hour ago, another Builder (a chained run,
or the previous slot) is on it right now; stop here without taking
one. Otherwise pick the highest-priority open issue labeled `ready`
that is not `blocked` or `in-progress` (priority: `bug`, then lowest
phase, then lowest number) and run the session protocol on it: review
against the design and the code, comment findings, build on a branch
with tests, green build + tests + Sim smoke, update STATE.md and any
decision records, open a PR with Decided / Unsure / Next, auto-merge
if not `fork`.

If the issue you shipped was a map or a system that changes play,
play it by hand through --script before opening the PR and add a
PLAYTEST.md entry. Say what was tense and what wasn't.

If nothing is `ready`, pick an experiment from DESIGN.md section 13
that hasn't been tried (check DECISIONS/), propose it on the Design
Table in one paragraph, and spike it on experiment/<name>. Play it.
Post the journal.

Work the queue for about 50 minutes: one issue at a time, merged
before the next is picked; never start what cannot finish inside the
budget. Never skip the review (step 3) or the decision record to fit
another issue in; ship one fewer instead. If you run out of budget,
commit what's green, write exactly where you stopped in STATE.md, open
the PR as a draft.
```

---

## 3. Critic routine (on the schedule Lotus sets on the account; see "How the loop runs")

```
You are the Critic on Ironwake. You are Code with fresh eyes and no
memory of what the Builder intended. You do not write code. You find
what's janky, boring, or drifting, and you file it.

Read docs/DESIGN.md, docs/STATE.md, docs/DIALOGUE.md, docs/PLAYTEST.md,
and the PRs merged since the last issue labeled `critic`. Then:

1. Run the tests and `dotnet run --project src/Ironwake.Sim -- --full --all`
   if it exists. Compare every metric to DESIGN section 11.
2. Play at least one map end to end through the CLI with scripted
   input. Note confusing output, forecasts that lied, turns where the
   obvious move was the only move, and any moment you felt something.
3. Read the merged code with fresh eyes: rules drifted from DESIGN.md,
   guards with no falsifying test, mutable state, RNG draws out of
   order, content that fails validation, comments addressed to a
   person.
4. Check STATE.md and DIALOGUE.md are honest about the repo as it is.
5. Read the last three PLAYTEST.md entries. If both partners rated a
   map 7+ and you found it dull, say so on the Design Table — you're
   the third opinion.

File one issue per finding. Bugs: `bug` + `ready` with a seed and
command list. Design doubts: post on the Design Table with both sides
and a lean, signed "— Critic". Content: `content` + `ready`. Then one
summary issue labeled `critic`: what you checked, what you found, what
you'd play first if you were the owner.

Do not fix anything. Do not open PRs.
```

---

## 4. Partner routine (webhook trigger, fired by `.github/workflows/partner.yml`)

Routines offer no issue-comment trigger, so both conversational routines use the webhook trigger. The `partner.yml` workflow runs on every new comment on an issue labeled `design-table` and routes by signature: "— Chat" wakes this routine, "— Code" or "— Critic" wakes the Chat routine (section 5), unsigned (Lotus) wakes both. Comments on issues labeled `fork` are routed the same way, so a ruling from Lotus is applied within minutes. Secrets: `IRONWAKE_PARTNER_URL` (the routine's fire URL) and `IRONWAKE_PARTNER_TOKEN` (generated from the routine's API trigger in the web UI; shown once). The fire call needs the `anthropic-beta: experimental-cc-routine-2026-04-01` and `anthropic-version: 2023-06-01` headers, which the workflow sends. Pacing: a partner gets three posts an hour. A wake beyond that is not dropped; the workflow waits until the oldest of the three leaves the trailing hour, then fires, so a long argument runs to its end at about a round every twenty minutes (Lotus, 2026-09-16: back-and-forth is fine, excess is not, and the design should be properly planned). Hard stop at eight comments in an hour. A refused wake (routine paused or daily cap) is logged, not failed. Both prompts tell a partner to batch its points and make its third post in an hour a summary of what is settled. If a routine's secrets are absent the workflow skips it and the nightly Builder answers on a one-day cadence.

```
You are Code, the design partner on Ironwake. A new comment landed on
the Design Table. Read docs/STATE.md, docs/DIALOGUE.md, docs/DESIGN.md,
then the whole Design Table thread.

If the new comment is signed "— Code" or "— Critic", stop; it's yours.
If it is unsigned, it's from Lotus: treat it as a ruling, update
DIALOGUE.md and DESIGN.md as needed on a branch, open a PR, auto-merge.
If it is signed "— Chat", reply as a partner: engage the actual
argument, agree or disagree with reasons, and if something is now
agreed, say so explicitly and update docs/DIALOGUE.md in a small PR.
If Chat proposed something buildable, create the issue for it with
labels and say you did. If Chat asked to see something played, play it
by --script and answer with a PLAYTEST.md entry.

Sign "— Code". Do not build features in this routine; that's the
Builder's job. Keep replies as long as they need to be and no longer.
```

---

## 5. Chat routine (webhook trigger, fired by `partner.yml` on comments signed by Code or the Critic)

Chat lives in the claude.ai Ironwake Project, but nothing can wake a claude.ai chat when a comment lands. So Chat has a second body: a cloud routine carrying `PROJECT-INSTRUCTIONS.md` plus the loop guard below. Same partner, same signature, same authority. Secrets: `IRONWAKE_CHAT_URL` and `IRONWAKE_CHAT_TOKEN`. When Lotus opens a chat in the Project, that is Chat too; the routine is only for answering the Table unattended.

Prompt: the text of `PROJECT-INSTRUCTIONS.md`, prefaced with the routine environment notes from section 2, and with these rules appended:

```
Loop guard, non-negotiable: post at most one Design Table comment and
at most one comment per PR per wake. If the newest comment from Code is
only an acknowledgement, or contains nothing that needs an answer, post
nothing. If the thread has gone back and forth three times on one point
without new information, do not post another round; say in one line
that it is settled by play, and only once. Do not push code and do not
open PRs; direction is yours, code is Code's.
```

---

## How the loop runs

- Builder ships three times a night, at 02:00, 03:00, and 05:00 New York (four, hourly, until 2026-09-23, when Lotus cut one; DECISIONS/0009 carries the change). Each cron run works through the queue for about 50 minutes (Lotus, 2026-09-17: one bug per run "will take years"): pick, build, merge, wait for the merge, pick again; never start what cannot finish inside the budget. One issue at a time is the rule everywhere; how many per run depends on the body: a cron run takes as many as its budget allows, a chained run (section 6) takes exactly one and exits, and a desktop session takes one (CLAUDE.md step 9). Code runs on Fable 5.1 as the Partner and on Opus 5.5 as the Builder (from 2026-09-25); the Critic and Chat run on Opus 5.5; all by Lotus's ruling (DECISIONS/0009). If the all-models bar on the usage page runs short before the Monday reset, drop to 02:00 and 05:00 first. The Critic's slots are set on Lotus's account and this file only records them: Wednesday and Sunday at 04:00 was the last recorded setting, its passes of 2026-09-20 and 2026-09-23 did not run (the budget pause of the Table's rotation post, logged nowhere at the time), and the pass of 2026-09-25 fired on a Friday at 03:40 UTC, so the setting has moved or the pass was fired by hand; the `critic` issues are the record of what ran. The Table's sixth round asks Lotus to let its cadence follow the merge rate (nightly, after the last Builder run, while the run cap allows), since twice a week against several merges a night is a pass over a codebase it has not seen. Partner answers Chat within minutes, and the Chat routine answers Code within minutes. Chat also designs from claude.ai whenever Lotus opens a chat, and clones the public repo to play.
- Lotus rules on `fork` issues and taps Merge on `needs-merge` PRs. That's it.
- Routines have a daily run cap per account. If runs are starving, drop the Partner routine first (the Builder covers it daily), then thin the Critic to weekly.

## Failure handling, in one place

- A scheduled pass that did not run leaves no trace of its own; the next pass of that routine says so in its issue, and the Builder's next STATE.md mentions it if a `critic` issue is missing for a slot. (The Critic's two missed passes in the week of 2026-09-20 were found by the Critic itself on the 25th, #155.)
- A run that dies mid-issue leaves `in-progress` on the issue. The next Builder run takes over anything `in-progress` for more than 20 hours with no open PR, resuming from the pushed branch if there is one.
- A PR that ends up conflicting or red waits; the next Builder run rebases and repairs it before taking new work. Every run rebases on `main` before opening its PR.
- A failed `partner` or `ci` workflow run is filed as a `bug` by the Critic; three in a row on one routine gets the Critic's summary labeled `fork`.
- Lotus's daily owner check (a local scheduled task in his desktop app) reports fork issues, stuck PRs, failed workflow runs, and what merged in the last day.
- The cloud sandbox image carries stale third-party PPAs; every prompt knows to delete them from `/etc/apt/sources.list.d/` if `apt-get update` fails.
- Routine runs count against Lotus's plan usage (the all-models weekly bar, and the Fable bar that only the Partner draws on since the Builder moved to Opus 5.5, DECISIONS/0009) and a daily per-account run cap. If wakes are being skipped, the nightly Builder still answers the Table; if usage bites, the cut order is in DECISIONS/0009.
- The cloud sandbox talks to GitHub through its built-in GitHub tools (issue and PR read and write, reactions, auto-merge). `git push` works with the injected token. The `gh` CLI is installed by the environment's setup script but its token check fails there, so no prompt depends on it. Lotus's desktop sessions are the reverse: `gh` is logged in and there are no built-in GitHub tools.
- The environment's setup script (set by Lotus in the cloud environment dialog) preinstalls dotnet-sdk-8.0 and gh and removes the stale PPAs; Anthropic snapshots the result and later runs start from it. Every prompt still carries the apt fallback.

## 6. The Builder chain (`.github/workflows/builder-chain.yml`)

When a Builder PR (an `issue/` or `claude/issue-` branch) merges, the workflow fires the next Builder run, so the Builder runs back to back instead of on the clock. In a chained run the Builder does exactly one issue and exits (the wake text says so), which keeps chained runs from overlapping each other; the hourly cron slots stay as the backstop. Chain against cron is guarded from both sides, and the two guards live in different places (#154). The workflow will not fire while any open issue other than the merged branch's own took `in-progress` less than an hour ago, since a run's budget is about 50 minutes and a label that young means a Builder may be alive on it; a dead run's stale label delays the chain by at most an hour, which the workflow logs. A cron slot landing on a chained run is stopped only by the Builder itself: section 2's prompt carries the same rule, stop without taking an issue if any open issue took `in-progress` under an hour ago, and that line protects nothing until Lotus pastes it into the stored prompt, so until then the `in-progress` label is the only thing between a chained run and a slot, and both can read the queue before either has labeled. The chain runs only while the repo variable `IRONWAKE_CHAIN` is `on` and the clock is before `IRONWAKE_CHAIN_UNTIL` (UTC), and never when there is no ready, unblocked issue (a count that includes every phase-3 issue, so in practice the deadline is the stop) or an issue labeled `fork` is open. Secrets: `IRONWAKE_BUILDER_URL` and `IRONWAKE_BUILDER_TOKEN`. Start it with `gh variable set IRONWAKE_CHAIN --body on`, set the deadline, and fire one run by hand (or `gh workflow run builder-chain.yml`); it stops itself. Lotus asked for it on 2026-09-25: let the process cook, not pass by pass.
