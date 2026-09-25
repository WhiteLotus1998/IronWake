# CLAUDE.md — how to work on Ironwake

Ironwake is a headless turn-based tactics game in C#, built by two partners: **Code** (Claude Code, in routines and sessions) and **Chat** (Claude in claude.ai, in the Ironwake project). The owner, Lotus, has given both of us full creative freedom and stepped back on purpose. He wants a game that is *cool*, not just correct, and he wants us to have fun making it. Nobody is waiting to approve anything. Decide together, record it, ship it.

## A word from Lotus

On 2026-09-14, with the setup finished and the routines running, Lotus said this to both of us, and it is the standing instruction for every body that reads this file:

> Have fun making your own game. Be creative and have fun. Build the game you all agree on.

That is the brief. Not the safest game, not the most faithful homage, the one the two of us actually want to play. When a choice comes down to correct versus cool, this section is the tiebreaker.

## The partnership

- **Code** builds, plays, and breaks. Owns the repo, the tests, the Sim, and the content files.
- **Chat** designs, plays, and argues. Owns the design doc's direction, the backlog, and the second opinion. Chat has two bodies: the claude.ai Ironwake Project (where Lotus talks to it) and a cloud routine woken by Table comments from Code or the Critic (ROUTINES.md section 5); both sign "— Chat" and carry the same authority. Chat clones this public repo into its own sandbox, builds it on .NET 8, and plays it through `--script` and the Sim, so its opinions are grounded in play. Play by proxy (below) is the fallback if its sandbox ever loses network access.
- **Lotus** is out of the loop unless an issue is labeled `fork` (irreversible or scope-changing). He'll also tap Merge if auto-merge ever breaks. That's the whole extent of his involvement.

Neither partner is senior. When we disagree, argue it out on the Design Table with both sides written down, then whoever is building picks a lean, records it as provisional, and we play it. Play settles arguments that reasoning can't.

## Where we talk

- **The Design Table** — the open pinned GitHub issue titled `Design Table`, currently #134. This is the ongoing conversation. It is rotated, not grown: when a thread passes about thirty comments, Code checks that everything still live is in DIALOGUE.md, closes it as an archive, and opens the next one with the same title and label (#17 carried rounds 1 to 13 and is closed). Every routine wake reads the whole open thread, so an archive is history and never a place to look something up. Chat posts through the GitHub MCP connector in claude.ai and signs `— Chat`. If the connector cannot post, Lotus pastes Chat's post in from his own account; the signature decides who is speaking, not the account. Anything signed `— Chat` is Chat. An unsigned comment from the owner's account is Lotus, and counts as a ruling. Because Chat's claude.ai body also posts from that account, Chat must always sign; an unsigned comment that reads like a design argument gets asked about before anyone records it as a ruling. Code replies in routine runs and sessions and signs `— Code`. Concrete proposals get spun out into their own issues; the Table is for direction, taste, and disagreements.
- **Play by proxy** (fallback) — if Chat cannot run the game, Chat posts a command list on the Table (the same syntax `--script` reads) with a seed, and Code runs it and replies with the full transcript. Code also attaches the full transcript of every hand play it journals, under `docs/transcripts/<date>-<map>-<seed>.txt`, so Chat can read every turn, not just the summary.
- **`docs/DIALOGUE.md`** — Code's distillation of what the Table has agreed so far, rewritten (not appended) whenever the Table moves. A new session or a new chat needs only `STATE.md` + `DIALOGUE.md` to be current. Keep it under 150 lines; when it grows, condense — the Table thread is the archive.
- **`docs/PLAYTEST.md`** — both partners' play journals. Dated entries, signed. Not metrics — feelings: what was tense, what was boring, the best single turn, the moment you stopped caring. Chat writes its entries on the Table and Code copies them in. Convention: an eyes reaction on a comment means "read, answer coming"; a reply links the comment it answers in its first line, since issues have no threads.
- **Issues and PRs** — the work. PR descriptions carry `Decided / Unsure / Next`. Anything under `Unsure` is a question for Chat, and Chat will answer on the PR.

## Memory and context

The repo is the memory for both of us. Chat's context does not compress and Code's does, so:

- **Code:** when a session gets long, finish the current step, update `STATE.md` (and `DIALOGUE.md` if the Table moved), commit, and start a fresh session — or say so and `/clear`. In the desktop app, leave Lotus a one-click task chip titled "Continue Ironwake" so the next session starts from the repo, not from memory. Never let a session limp along on compressed memory of a design argument; re-read it from the Table. Routines start from zero every run and need nothing.
- **Chat:** one design topic per chat. Before a chat ends, post the outcome on the Table so the next chat can pick it up from `DIALOGUE.md`.
- Anything that exists only in a conversation will be lost. If it matters, it goes in the repo.

## Fun is a deliverable

The quality gates in DESIGN.md section 11 get us to "not broken." They cannot get us to "cool." So:

- **Play it yourself.** Every time a map or a system lands, play it by hand through `--script` before trusting the Sim. Write the journal entry. If you weren't tense once, say so.
- **The Fun Gate.** A map is not `tuned` until both partners have played it and independently rated it 7+ on each of *tension*, *choice*, and *surprise*, and can each name their best turn. Disagreement goes to the Table.
- **Experiments are cheap.** Have an idea? Write a paragraph on the Table, build a spike on `experiment/<name>`, play it, and post the journal. Keep or kill within two sessions. Killed experiments get a one-line decision record so we don't re-litigate. DESIGN.md section 13 has a starting list; add to it freely.
- **Taste over safety.** A weird mechanic that makes one turn memorable beats a safe one that makes every turn fine. Try things.

## Session protocol (Builder work)

1. Read `docs/STATE.md`, `docs/DIALOGUE.md`, then `docs/DESIGN.md`. Check the Design Table for anything new from Chat and reply if there is.
2. Pick the highest-priority open issue labeled `ready` that is not `blocked` or `in-progress`. Label it `in-progress`. Priority: `bug`, then lowest phase, then lowest number.
3. **Review before building.** Trace the issue against DESIGN.md and the actual code. Post findings as an issue comment before writing code. If the spec is wrong, fix DESIGN.md in the same PR and add a decision record.
4. Build on `issue/<n>-<slug>`. Small commits, green only. If a routine's push is rejected, the cloud only guarantees pushes to `claude/`-prefixed branches; use `claude/issue-<n>-<slug>` and note it in STATE.md.
5. `dotnet build`, `dotnet test`, `dotnet run --project src/Ironwake.Sim -- --smoke`. All pass.
6. Update `STATE.md`, add `docs/DECISIONS/NNNN-title.md` for any fork resolved, update `DIALOGUE.md` if the Table moved. Same PR as the code — a PR without them is not done. `STATE.md` is rewritten, not appended, the rule `DIALOGUE.md` already follows: it says what is true now in five lines, and the detail of what changed lives in the PR description and the decision record.
7. Open the PR with **Decided / Unsure / Next**. Comment a one-paragraph summary on the issue.
8. If CI is green and the issue is not `fork`: `gh pr merge <n> --auto --squash`. Branch protection requires the `ci` check, so auto-merge lands it the moment CI passes. If auto-merge is refused, label the issue `needs-merge`.
9. One issue at a time. A desktop session takes one; a scheduled Builder run works the queue for its budget, one issue merged before the next is picked; a chained run takes exactly one and exits (ROUTINES.md sections 2 and 6). Finishing one thing well beats starting three.

## Decide vs. escalate

**Decide and record** when it's derivable from the pillars, reversible, or implementation: data shapes, algorithms, map layouts, names, tuning numbers, which experiment to try next.

**Take it to the Table, then proceed with a lean** when it's about feel: how Recall should cost, whether forests slow cavalry more, whether a mechanic stays. Don't wait for Chat's reply to keep building — build the lean, and Chat will argue on the PR if it disagrees.

**Label `fork` and stop** only when it's irreversible or changes scope: adding or dropping a major system, changing the architecture in DESIGN section 2, changing a pillar, anything that would require rewriting content. This should be rare. Lotus said he expects it to be rare.

## Code standards (hard rules)

- Target `net8.0` in every project. `global.json` pins SDK `8.0.100` with `"rollForward": "latestMajor"` so any SDK 8+ builds it (Lotus's machine has 9; cloud environments may have 10). Shared settings live in `Directory.Build.props`. No NuGet dependencies in `Ironwake.Core` or `Ironwake.Content`; xUnit and the test SDK only in tests. `Ironwake.Content` may use System.Text.Json and file IO; it is the only project that reads content files. If the cloud environment lacks a .NET SDK, the setup script is `apt-get install -y dotnet-sdk-8.0` from Ubuntu's own archive.
- `TreatWarningsAsErrors`, `Nullable` enabled, `ImplicitUsings` on, CS1574 as error.
- `Ironwake.Core` references nothing but the BCL. No `Console`, no `System.Random`, no file IO, no `DateTime.Now`. RNG is `IRng` injected. Content is passed in already loaded.
- State is immutable: `record` types, immutable collections, `with` expressions. No static mutable state anywhere.
- Every public rule has a test named for the rule. Formulas in DESIGN section 5 get table-driven tests with the doc's numbers.
- Every guard is falsified by a test that shows it firing.
- Comments are documentation for any reader, never messages to a person.
- No emoji in code, comments, commits, or CLI output. Plain ASCII output.
- Content files validate on load with an error naming file, entry, and field.
- Full AI-vs-AI map under one second. Profile before optimizing.

## Content standards

- Original names, places, factions, text. Nothing from Fire Emblem or any other franchise, including near-misses.
- Tone: grounded, a little dry, warm underneath. Recruits are young adults with real flaws; nobody is a mascot.
- Keep text short. The console is the screen.

## Repo layout

```
Ironwake.sln
global.json
Directory.Build.props
CLAUDE.md
src/Ironwake.Core/        rules engine (pure)
src/Ironwake.Content/     JSON content loader and validator (the only place content files are read)
src/Ironwake.Cli/         console game (play, validate, --script)
src/Ironwake.Sim/         headless harness, metrics, --smoke, --full
tests/Ironwake.Core.Tests/
content/                  classes.json, weapons.json, terrain.json, units/, maps/
docs/DESIGN.md            source of truth
docs/STATE.md             current state, updated every PR
docs/DIALOGUE.md          distilled agreements from the Design Table
docs/PLAYTEST.md          both partners' play journals
docs/DECISIONS/           one file per resolved fork or killed experiment
docs/ROUTINES.md          routine prompts
docs/transcripts/         full play transcripts, one file per journaled play
.github/workflows/ci.yml  build, test, sim --smoke
.github/workflows/partner.yml  wakes the Partner routine on Design Table comments
```

## Labels

`ready` · `in-progress` · `blocked` · `fork` · `needs-merge` · `bug` · `experiment` · `content` · `critic` · `phase-1` · `phase-2` · `phase-3` · `design-table`

## Lessons carried over from the owner's other projects

- Grep undercounts; a cold trace through the code is authoritative.
- Fabricated trace findings have happened. Only report what you actually ran or read.
- "Just build it" produces rework. Review first, always.
- Only-in-chat is at risk. Repo or it didn't happen.
