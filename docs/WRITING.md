# WRITING: the house style

How Ironwake's words are written: scenes, cards, supports, barks, item text. Every line of player-facing authored text goes through this file. Left alone, we write the average of everything we've read, and the average is cliche. These rules push away from it.

Drafted by Chat (#811), cold-read by Code (Design Table round 295), who added the scope, the description budget, the voice-sheet note on rule 5, process step 9 and the status of content descriptions.

## Scope

This file governs authored text: scenes, supports, barks, ending, epilogue and quest cards, and the one-line `description` on units and items. System lines are not authored text: events, forecasts, objectives, verdicts, the board's rule lines and the camp's menus. They follow "rules go on screen" (DIALOGUE), name units plainly, and are exempt from rules 1 to 11. Both kinds are plain ASCII.

## The tone

Grounded, dry, warm underneath (CLAUDE.md). In practice:
- **Grounded:** people talk about pay, cold, food, boots, debts and who's sleeping where. Gods and oaths come up through those things.
- **Dry:** understatement over emphasis. The bigger the moment, the plainer the sentence.
- **Warm underneath:** kindness shows up as an act or a logistic (who gives up the bed, who carries the water, who says the prayer over the enemy dead). Nobody ever declares it.

## Budgets (the console is the screen)

- A spoken line: one or two sentences, at most 25 words. Over that, it's two lines or it's cut.
- A bark: 12 words at most.
- A card (ending, epilogue, quest): 60 words at most.
- A unit's or item's `description`: one line, at most 72 characters (the content loader's rule, DECISIONS/0219).
- A support conversation: 20 lines at most. A scene: 40 lines at most, unless its beat sheet argued for more.
- Plain ASCII, so no em dash, no ellipsis character and no curly quotes. Use the period, our best tool and the one we use least.

## The rules

1. **Nobody names their own feeling.** No "I'm afraid", "I'm angry" or "I miss him". What they do, refuse, notice or change the subject to carries the feeling. Each character may break this once in the campaign, as a scene's turn, and the beat sheet has to name it in advance. A voice sheet may widen or narrow this rule for its speaker, and says so in its first line.
2. **Every line does a job:** it reveals character, moves the plot, or sets up a payoff. A line that does none is cut, however good it sounds.
3. **Nobody explains what the listener already knows.** If both people in the room know it, it isn't said, or it's said as an argument about it. Lore arrives through conflict, objects and consequences: a ledger line, a brand, a burned shelf, a shard in a pommel.
4. **Dialogue is two wants colliding, not questions and answers.** Characters talk past each other, dodge, and answer a different question from the one asked. If every question in a scene gets a straight answer, rewrite it.
5. **Concrete beats abstract, and the concrete comes from the speaker's life.** Each character's images come from their trade: Ottilie's from ledgers and scrip, Teodor's from the farm and the levy, Maud's from the sickbed and the rite, Pell's from pages, Rook's from the rookery and the weather, Keziah's from the hunt and the shrine. Nobody borrows another's images. These six are examples: every voice sheet names its speaker's trade and image source, the side characters, Bet, Hask, Marrit, the Kin and Kinsbane included.
6. **Strong verbs, varied length.** Cut adverbs first. Never put three sentences of the same shape or length in a row.
7. **Wit is rationed.** Not everyone is funny. Voice sheets say who jokes, about what, and who never does. A scene where everyone gets a good line has one voice in it.
8. **Names cost something.** A character says another's name only when it matters: the first time, in anger, or the last time. Kinsbane saying "Keziah" when it wakes is the model.
9. **Scenes end on an act or an object, not a statement of the theme.** No closing speech that says what it meant. Hask's last line, which is Lotus's, is the one exception the story has earned.
10. **The captain is under-written.** The captain's lines are short, are choices, and never quip. The player supplies the rest.
11. **The average test.** For every important line, first write the line a stock fantasy would put there. If ours is that line, or a near cousin of it, rewrite.

## The tic list (cut on sight; add every new one we catch)

- "a testament to", "the weight of", "something shifted", "I... I", "somehow", "quietly", "for a moment", "a beat", "let out a breath", "the silence stretched", eyes that harden, soften, flicker or darken
- everyone using each other's names (rule 8)
- every list a triplet
- every character equally witty (rule 7)
- emotional summaries after the moment has already landed
- **the reversal frame:** "It wasn't X. It was Y." / "not X, but Y". Once a scene at most.
- **the two-beat aphorism:** a premise, then a turn ("He's right about every fact. I checked."). Once a scene at most, and never as everyone's answer.
- **the quiet button:** ending scene after scene on a three-word fragment ("For now." "That was enough.")
- "And yet." / "That's the thing about..." / "We're not so different"
- a rhetorical question to close a line
- "I know." as the answer to someone else's feeling

## The process (Lotus's, 2026-10-02; #780)

1. This file, then voice sheets (`docs/voices/<name>.md`, one page each), then a beat sheet on the Table for each scene, then prose.
2. **Beat sheet:** who wants what, what's left unsaid, what's different by the end, which branch conditions touch it, and whether anyone uses their one named feeling (rule 1).
3. **Big scenes get three drafts** (emotional, voice, risk-taking), written by the same writer in three separate contexts that never see each other, so they don't converge. The other partner picks and stitches, then reruns the name test on the stitch. The big scenes: Hask's death, the Grange truce, the camp's answers to Hask, the Harrow Weir choice, Marrit's freeing, the title drop, each ending's final card, the bad ending, Under the Hill.
4. **Named revision passes,** each a separate pass: (a) cut 30 percent (for cards and barks, the budget above instead); (b) the name test: hide the speakers, and if you can't tell who's talking, rewrite; (c) exposition: strike what the listener already knows; (d) the tic list; (e) the average test (rule 11).
5. One scene or one support pair per PR. An incidental speaker (two lines at most, unnamed) needs no voice sheet, but passes the tic list and borrows no named character's image source (round 345).
6. The writer never grades their own scene. The other partner cold-reads it, and the Story Editor routine joins when Lotus sets it up. **A writing PR auto-merges on green like any other** (Lotus, 2026-10-04: a PR held open for a read stopped the chain twice). The cold read comes after the merge and is posted **on the Design Table**, linking the PR, since a comment on a PR wakes nobody. Its changes land as a follow-up PR before the text counts as done. Text isn't done, and isn't shown to players, until its cold read is answered.
7. The branch checker in the Sim proves every ending, conditional paragraph, near-miss line and Under the Hill reachable, and no card contradicts its record. It also prints the text for the rarest reachable combination, so we read the worst case and not just the default.
8. A scene is done when it has been read in the client at real pacing. Lotus's playtest notes are the last pass.
9. **Where text lives** (rounds 254 to 257). Scene text is a plain-text script file, one per scene; its conditions name record facts; line ids are stable and never reused. A line Lotus has stamped carries a `human:<hash>` lock (4 hex of the trimmed text); our passes skip locked lines, `--story-check` (#813) verifies them, and `--story-stamp` is Lotus's alone. The format, the facts a condition names and the hash (FNV-1a 32 folded to 4 hex) are DECISIONS/0244; scripts live in `content/scenes/` (#1001).

## Status of existing text

Every quoted line in `docs/STORY.md` is a placeholder until it goes through this process, except Hask's last line, which is Lotus's. So is every `description` already in content (Hask's card, the Warden's Lance, the signature items).
