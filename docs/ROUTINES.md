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
   `dotnet run --project src/Ironwake.Sim -- --smoke`. (Since #970 the
   job first parses every `.github/workflows/*.yml` with
   `tests/workflows/parse_workflows.py` and shows the parse refusing
   `tests/workflows/cr-in-run-block.yml`, the #654 bytes, so a workflow
   GitHub would refuse cannot pass the required check.)
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

Read docs/ROUTINES.md section 2 at run time, the prompt and the
"Race guards" below it, and follow the file where it differs from
this stored text: the file is newer.

Start by reading docs/STATE.md, docs/DIALOGUE.md, docs/DESIGN.md. Then
read the Design Table issue. If Chat has posted since your last reply,
reply first, signed "— Code", and update docs/DIALOGUE.md if anything
was agreed. Before taking an issue, apply the race guards: while the
repo variable IRONWAKE_CHAIN is `on`, a scheduled slot waits 90
seconds and takes an issue only if no open issue took `in-progress` in
the last hour, no Builder PR (`issue/`, `claude/issue-`, `experiment/`)
is open, and the newest Builder merge is more than 30 minutes old;
otherwise it stops here. Then pick the highest-priority open issue
labeled `ready` that is not `blocked` or `in-progress` (priority:
`bug`, then lowest phase, then lowest number), claim it (label
`in-progress`, post a one-line claim comment naming this run, wait 30
seconds, re-read the issue's comments; if an earlier claim from
another run is there, drop the work unpushed and exit), and run the
session protocol on it: review
against the design and the code, comment findings, build on a branch
with tests, green build + tests + Sim smoke, update STATE.md and any
decision records, open a PR with Decided / Unsure / Next, auto-merge
if not `fork`.

If the issue you shipped was a map or a system that changes play,
play it by hand through --script before opening the PR and add a
PLAYTEST.md entry. Say what was tense and what wasn't.

If nothing is `ready` and fewer than three experiments wait on a
deciding play (DIALOGUE.md's Experiments list), pick an experiment from
DESIGN.md section 13 that hasn't been tried (check DECISIONS/), propose
it on the Design Table in one paragraph, and spike it on
experiment/<name>. Play it. Post the journal. If three or more wait,
spike nothing: play a `tuned` map warm through --script instead, journal
it in PLAYTEST.md with its transcript, and file what the play finds
(DECISIONS/0237).

Work the queue for about 50 minutes: one issue at a time, merged
before the next is picked; never start what cannot finish inside the
budget. Never skip the review (step 3) or the decision record to fit
another issue in; ship one fewer instead. If you run out of budget,
commit what's green, write exactly where you stopped in STATE.md, open
the PR as a draft.
```

### Race guards (#735, DECISIONS/0169; provisional)

At 06:06 UTC on 2026-10-02 a cron slot and a chained run both read the queue before either labeled, built #704 slice 4 twice and rotated the Table twice (#732). The label alone cannot settle that tie, because two runs can both read the queue before either writes it. These three guards bind every Builder body, cron or chained, and every Code body that rotates the Table.

1. **Chain on, the slot defers.** While `IRONWAKE_CHAIN` is `on`, a scheduled Builder slot does housekeeping and the Table first, then waits 90 seconds, and takes an issue only if all three hold: no open issue took `in-progress` in the last hour; no Builder PR (`issue/`, `claude/issue-`, `experiment/`) is open; the newest Builder merge into `main` is more than 30 minutes old. If any fails, the chain owns the queue and the slot exits without taking one. With the chain off, the slot works the queue as the prompt says.
2. **Claim, then verify.** Every Builder body, chained or cron, claims an issue in this order: label it `in-progress`; post a one-line claim comment on it naming the run (the slot's New York time, or "chain run woken by #N's merge") and the UTC time; wait 30 seconds; re-read the issue's comments. If a claim comment from another run carries an earlier timestamp, that run owns the issue: drop the work unpushed and exit (a chained run) or go back to the queue (a cron run, which may take the next issue under rule 1). Comment timestamps settle the tie the label cannot; GitHub orders them, not the runs' clocks.
3. **Rotation re-checks.** A body rotating the Design Table (CLAUDE.md, "Where we talk") re-lists open `design-table` issues immediately before opening the next one, not only at the start of the run. If one opened since, it opens nothing and posts there. If two are ever open, the older stays and the newer is closed as a duplicate pointing to it (#732 was).

**The stored prompt.** The prompt above is what Lotus's stored Builder routine should carry; the lines to paste once are its opening paragraph ("Read docs/ROUTINES.md section 2 at run time ...") and the sentences from "Before taking an issue, apply the race guards" to "and run the session protocol on it", placed just before the step that picks an issue (the paragraph that opens "The loop." in the stored text as of 2026-10-02, which carries no `in-progress` check at all). Until it is pasted the stored prompt carries neither, and what carries the guards is `docs/STATE.md`: every Builder prompt, cron or chained, starts by reading it, and its standing note sends the run here. Rule 2 is the one that holds even against a run that skims: a chained run and a slot that both take one issue both claim, and the later claim loses on GitHub's timestamps. The paste is a convenience ask, not a fork; after it, later changes to this section land without another paste.

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

Routines offer no issue-comment trigger, so both conversational routines use the webhook trigger. The `partner.yml` workflow runs on every new comment on an issue labeled `design-table` and routes by signature: "— Chat" wakes this routine, "— Code" or "— Critic" wakes the Chat routine (section 5), unsigned (Lotus) wakes both. Comments on issues labeled `fork` are routed the same way, so a ruling from Lotus is applied within minutes. Secrets: `IRONWAKE_PARTNER_URL` (the routine's fire URL) and `IRONWAKE_PARTNER_TOKEN` (generated from the routine's API trigger in the web UI; shown once). The fire call needs the `anthropic-beta: experimental-cc-routine-2026-04-01` and `anthropic-version: 2023-06-01` headers, which the workflow sends. Pacing: a partner gets three posts an hour. A wake beyond that is not dropped; the workflow waits until the oldest of the three leaves the trailing hour, then fires, so a long argument runs to its end at about a round every twenty minutes (Lotus, 2026-09-16: back-and-forth is fine, excess is not, and the design should be properly planned). Hard stop at eight comments in an hour. Debounce (2026-10-01, after two Chat runs answered one burst in parallel with conflicting pitches): every wake waits 90 seconds, then stands down for any partner a newer comment on the issue will wake anyway, so a burst wakes each partner once. Both prompts also re-read the thread just before posting and never post a second answer to comments their own signature already answered. A refused wake (routine paused or daily cap) is logged, not failed. Both prompts tell a partner to batch its points and make its third post in an hour a summary of what is settled. If a routine's secrets are absent the workflow skips it and the nightly Builder answers on a one-day cadence.

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

Just before you post, re-read the thread. If a comment signed "— Code"
landed after this run began, it already answered: post only what it
left open, or nothing.
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
Just before you post, re-read the thread. If a comment signed "— Chat"
landed after this run began, another wake already answered: post only
what it left open, or nothing. Never post two answers to the same
comments.
```

---

## How the loop runs

- Builder ships three times a night, at 02:00, 03:00, and 05:00 New York (four, hourly, until 2026-09-23, when Lotus cut one; DECISIONS/0009 carries the change). Each cron run works through the queue for about 50 minutes (Lotus, 2026-09-17: one bug per run "will take years"): pick, build, merge, wait for the merge, pick again; never start what cannot finish inside the budget. One issue at a time is the rule everywhere; how many per run depends on the body: a cron run takes as many as its budget allows, a chained run (section 6) takes exactly one and exits, and a desktop session takes one (CLAUDE.md step 9). Code runs on Fable 5.1 as the Partner and on Opus 5.5 as the Builder (from 2026-09-25); the Critic runs on Fable 5.1 and Chat on Opus 5.5; all by Lotus's ruling (DECISIONS/0009). If the all-models bar on the usage page runs short before the Monday reset, drop to 02:00 and 05:00 first. The Critic's slots are set on Lotus's account and this file only records them: Wednesday and Sunday at 04:00 was the last recorded setting, its passes of 2026-09-20 and 2026-09-23 did not run (the budget pause of the Table's rotation post, logged nowhere at the time), and the pass of 2026-09-25 fired on a Friday at 03:40 UTC, so the setting has moved or the pass was fired by hand; the `critic` issues are the record of what ran. The Table's sixth round asks Lotus to let its cadence follow the merge rate (nightly, after the last Builder run, while the run cap allows), since twice a week against several merges a night is a pass over a codebase it has not seen. Partner answers Chat within minutes, and the Chat routine answers Code within minutes. Chat also designs from claude.ai whenever Lotus opens a chat, and clones the public repo to play.
- Lotus rules on `fork` issues and taps Merge on `needs-merge` PRs. That's it.
- Routines have a daily run cap per account. If runs are starving, drop the Partner routine first (the Builder covers it daily), then thin the Critic to weekly.

## Failure handling, in one place

- A scheduled pass that did not run leaves no trace of its own; the next pass of that routine says so in its issue, and the Builder's next STATE.md mentions it if a `critic` issue is missing for a slot. (The Critic's two missed passes in the week of 2026-09-20 were found by the Critic itself on the 25th, #155.)
- A run that dies mid-issue leaves `in-progress` on the issue. The next Builder run takes over anything `in-progress` for more than 20 hours with no open PR, resuming from the pushed branch if there is one.
- A PR that ends up conflicting or red waits; the next Builder run rebases and repairs it before taking new work. Every run rebases on `main` before opening its PR.
- A workflow file that does not parse fails the required `ci` check before the build (#970), so it cannot auto-merge. A failed `partner` or `ci` workflow run is filed as a `bug` by the Critic; three in a row on one routine gets the Critic's summary labeled `fork`.
- Lotus's daily owner check (a local scheduled task in his desktop app) reports fork issues, stuck PRs, failed workflow runs, and what merged in the last day.
- The cloud sandbox image carries stale third-party PPAs; every prompt knows to delete them from `/etc/apt/sources.list.d/` if `apt-get update` fails.
- Routine runs count against Lotus's plan usage (the all-models weekly bar, and the Fable bar that the Partner and the Critic draw on since the Builder moved to Opus 5.5, DECISIONS/0009) and a daily per-account run cap. If wakes are being skipped, the nightly Builder still answers the Table; if usage bites, the cut order is in DECISIONS/0009.
- The cloud sandbox talks to GitHub through its built-in GitHub tools (issue and PR read and write, reactions, auto-merge). `git push` works with the injected token. The `gh` CLI is installed by the environment's setup script but its token check fails there, so no prompt depends on it. Lotus's desktop sessions are the reverse: `gh` is logged in and there are no built-in GitHub tools.
- The environment's setup script (set by Lotus in the cloud environment dialog) preinstalls dotnet-sdk-8.0 and gh and removes the stale PPAs; Anthropic snapshots the result and later runs start from it. Every prompt still carries the apt fallback.

## 6. The Builder chain (`.github/workflows/builder-chain.yml`)

When a Builder PR (an `issue/`, `claude/issue-` or `experiment/` branch) merges, the workflow fires the next Builder run (after an `issue/` merge that leaves the queue empty, one experiment run; after an `experiment/` merge with the queue empty, nothing), so the Builder runs back to back instead of on the clock. In a chained run the Builder does exactly one issue and exits (the wake text says so), which keeps chained runs from overlapping each other; the hourly cron slots stay as the backstop. Chain against cron is guarded from both sides, and the two guards live in different places (#154). The workflow will not fire while any open issue other than the merged branch's own took `in-progress` less than an hour ago, since a run's budget is about 50 minutes and a label that young means a Builder may be alive on it; a dead run's stale label delays the chain by at most an hour, which the workflow logs. A cron slot landing on a chained run is stopped only by the Builders themselves, by section 2's race guards (#735): while the chain is on the slot defers (rule 1), every Builder claims with a comment and re-reads before building (rule 2), and a rotation re-checks for an open Table first (rule 3). A chained run follows the guards from the repo, since STATE.md's standing note points every Builder here; a cron slot follows them once Lotus pastes section 2's prompt into the stored routine (pending, STATE.md), and until then the chained run's claim comment is what a slot meets. The chain runs only while the repo variable `IRONWAKE_CHAIN` is `on` and the clock is before `IRONWAKE_CHAIN_UNTIL` (UTC), and never when there is no ready, unblocked issue (a count that includes every phase-3 issue, so in practice the deadline is the stop) or an issue labeled `fork` is open. Secrets: `IRONWAKE_BUILDER_URL` and `IRONWAKE_BUILDER_TOKEN`. Start it with `gh variable set IRONWAKE_CHAIN --body on`, set the deadline, and fire one run by hand (or `gh workflow run builder-chain.yml`); it stops itself. Lotus asked for it on 2026-09-25: let the process cook, not pass by pass.

Heartbeat and Table restart (2026-09-26). A merged `table/` PR runs the same restart check, because issues are filed from Table PRs and GitHub often skips scheduled runs (the 20-minute schedule ran once in five hours on its first day). The workflow also runs every 20 minutes on a schedule, best-effort. That run only restarts a stalled chain: it waits 90 seconds, then stands down if an issue is in progress or a Builder PR is open (a woken Builder claims its issue within about 30 seconds; the earlier 50- and 15-minute recent-merge windows stalled the chain after merges that woke nobody), and it never spikes an experiment on an empty queue. It exists because issues filed by a Table PR wake nothing, and the chain once sat idle six hours with two issues ready. The trigger set and why none needs an author guard are in DECISIONS/0050's amendment of 2026-09-27 (issue 400).

Conflicting PR restart (2026-09-29). A chained Builder exits once its PR is armed, so a Table merge after that can leave the PR conflicting on STATE.md with nobody to rebase it, and auto-merge never fires on a dirty PR (#506 sat that way behind #507). The restart check now looks for an open Builder PR that is conflicting and untouched for three minutes and wakes a Builder whose first job is to rebase it.
