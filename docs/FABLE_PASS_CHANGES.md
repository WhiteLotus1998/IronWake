# The Fable story pass: what changed, and why

For Lotus, ahead of his own pass (`docs/FABLE_PASS.md`, 2026-10-05; the pass ran 2026-10-05 to 06). Draft 7 of `docs/STORY.md` is the text. This file is the diff in words, in STORY's order, so every edit can be found and judged. "Kept" means Lotus's or the partners' decision stands untouched. "Proposed" means Lotus decides, and the game is built to the unmarked text until he does.

## What the pass touched

- `docs/STORY.md`: rewritten as draft 7. Draft 6's header is kept at its end for the record.
- `docs/voices/*.md`: the **Who** paragraph of every sheet whose past changed, each addition marked "(STORY draft 7)"; a sample or a Never line where the change needed one. `brannock.md` is rewritten. `ivo.md` is new and marked proposed.
- `content/units/cast.json`: the eleven `personality` lines, which were stale against the story (below).
- `content/campaign.json`: the field's before card, which was the last main-line card marked placeholder.
- `content/weapons.json`, `content/items.json`: the seven item descriptions marked placeholder.
- `content/units/enemies.json`: one line on the Sworn Captain, who had none.
- `docs/look/CASTING.md`: Brannock's note, to match his reworked past. Nothing else; Lotus recasts after his character pass.
- Transcripts that print a changed line were regenerated with `tools/rejournal.py` (the PR lists them). No map tile, number, class or rule moved anywhere.

Everything quoted in STORY is a placeholder until it passes `docs/WRITING.md`, except Lotus's own lines, which are untouched.

## 1. The world (new)

**The Crown.** Draft 6 used "the crown" in a dozen places (the academy, the scrip, Ottilie's debt, the dismissal seal, Alder's commission) and never said what it was. Draft 7 makes it the one organ of the old kingdom that survived the split: an office, not a man, a crown left on a table in the keep's chancery. It owns the seam and the keep, issues the scrip and the roll, keeps the academy at Harrow, and has no soldiers but the garrison. *Why:* it explains in one stroke why nobody came for the seam, why the levy is late and short, why the scrip trades under face, and what Alder actually works for.

**The regions.** Lotus's geography is kept word for word. Each region now has a habit of mind and one shame its recruits carry: Kestrow sells what it says is holy (Maud, the rider); Sallow sold its own and wrote down the price (Ottilie, Ansgar, Ivo); Aldmere hangs the poor for the border's crimes (Wren, Teodor). *Why:* "similarities to bond over" needed a source; shared shame is one.

**Named places.** Sorrel Bend (the lazar house), Hollin, Thane's Hollow, the waystation on the keep road; the academy at Harrow; Vasse and Daughter on the Brackwater. Placeholders, like every name.

## 2. The rite in three parts (new; the one structural idea)

**What.** The armistice rite has three parts, one kept by each faith, and each faith has forgotten its part is a third of anything. The Word (Kestrow: the responses, Maud). The Stone (Aldmere: a border stone set into the keep's wall each year, one course a year for three hundred years, Wren). The Count (Sallow: the roll of names owed to the hill, the garrison made flesh, Alder's list, Ottilie's ledger, Bet's supper count).

**Why.** Lotus asked for everything to tie together as one world. Draft 6 had three recruit threads (Pell's burned books, Wren's moved stones, Ottilie's stolen pay) that each pointed at Hask and never at each other. As three tumblers of one lock: Plan A becomes one plan in three parts; the Held ending's "one living member of each region" gets an in-world reason instead of a design one; Alder's list is the rite without Alder knowing; and "Bring them home" is the Count. It also makes the 300-course wall, the rime on it, and the frost line receding literal parts of one image.

**What it does not change.** No mechanic. Maud's one changed word, Pell's charter, the Held and hand-locked textures, the shard in last: all stand. If Lotus does not want it, every other change in this pass stands without it; no character's past depends on it.

## 3. The Kin, Kinsbane and the Bane

- The Kin: kept as draft 6 wrote it.
- Kinsbane's canon as built: kept.
- **Proposed: the Bane.** Lotus's three lines (2026-10-05) developed: one outland god of the lean season with two faces, the Kin (hearth, the herd penned) and the Bane (the cull, the hunt); the kings fed one face and starved the other until they came apart; the keepers carried what was left of the Bane in a reaping blade and named the blade for its purpose. Under this reading the secret ending's Fight win draws the Kin's half into the scythe, and the god is whole in a blade for the first time in three hundred years. **After both halves are sealed, three shapes** for Lotus to pick from, or his own: it goes home south with Keziah (recommended: her choice, unexplained, and it leaves the scythe in the world); it is laid in the empty prison; Keziah keeps it. Nothing built changes under any of them. One line changes: the woken god's goodbye becomes a staying.

## 4. Hask

- Kept: everything Lotus kept. **His last line is verbatim and marked as his.** "I gave you the cleanest death", "Then I'll take it the long way", "You held it", the three class lines, the title drop.
- **New: the wound.** A Sallow pike at Saltmarsh Ford twenty years ago; Teodor's grandfather carried him off the ford to the lazar house at Sorrel Bend; the lance was the parting gift. *Why:* draft 6 said "an old wound" and nothing else. This one ties map 3's ground, Maud's quest 1 and Teodor's lance to the man.
- **New: he feeds the lazar house.** The meal bins at Sorrel Bend, bought monthly and never signed for, are his, out of the stolen Count. *Why:* "the villain you question" shown as an act, not a speech; the player finds his name on it at the Long Count.
- **New: Plan A in three parts.** Burn the Word, move the Stones, starve the Count. *Why:* section 2.
- **Proposed: giving him the list** (pending Lotus since round 366). Written as story: if the captain hands it over, Hask reads the ten names aloud and reads the captain's first, where the crossed-out one was; then the card goes to "we". The partners' lean (yes, the cheap form) is kept; the pass adds only how it should read.

## 5. The captain

- **New: the name.** Alder for the one tree in the keep yard, the alder against the bakehouse wall the foundling was found under; Fenn for the duty clerk whose turn it was when the roll needed two names. *Why:* a foundling's name should come from somewhere, and the tree stands where Bet found them, so the Cold Kitchen pays twice. Alder's rule-1 narrowing and under-writing stand.
- **New: what Alder shares** (with Marrit and Bet the garrison; with the rider, the line below a holding; with Teodor, the grandfather's voice in Hask's mouth).

## 6. The company, one by one

Each entry now carries a **what they share** line, which is where the supports come from. The web: Maud and Dunstan share Osric, the dead rite-keeper; Pell and Maud the old order of the responses; Pell and Keziah a burned building and one thing carried out; Teodor and Brannock a life put down for another; Wren and Teodor his letters; Ottilie and Ansgar Sallow's paper; Ottilie and Keziah the bought seat; the rider and Keziah the one thing that ever wanted them; the rider and Dunstan an owner walked away from; Brannock and Ansgar the lee side of the fire; Brannock and Maud a church that did not come. The existing support pairs in `campaign.json` all fit it; none was changed.

- **Maud.** *New:* a lazar-house child, raised at Sorrel Bend among the garrison's broken men, who learned the basin before the rite and the rite's old order from the house's dying keeper; the order took her because a ward is property. *Why:* draft 6 gave her a job and a flaw and no childhood; this one puts her quest 1 on ground she grew up on and gives her flaw (control) a source (the one place nobody could overrule her). The senior killed on the road is now Osric, killed on Dunstan's bridge, which ties two recruits who already had a support.
- **Pell.** *New:* fourteen, in the cellar copying lines as a punishment, which is why she lived; came up with one page; her school was the first of Hask's burnings. *Why:* "trusts a page over a person" needed the night it was decided. Her thread (the brand, the post, the undercroft door, the Grange's spines) stands as draft 6 wrote it.
- **Teodor.** *New:* he cannot read, and never says so; his grandfather carried Hask off the ford; Wren teaches him the letters on the socket, unmentioned, before the Warden's Gate names them. *Why:* draft 6's Teodor was "big, kind and slow", which is the stock farm boy. A secret he keeps for the farm's pride, and a quiet thing Wren does for him, make him a person. His quests stand.
- **Ottilie.** *New:* Vasse and Daughter, salt factors on the Brackwater, thirty years the garrison's salt; the firm folded the week the Crown's payment was skimmed under the Warden's seal; her father's last line in the ledger is unpaid wages (Ivo's, if Ivo is taken). *Why:* her money was abstract; salt is a thing the garrison ate, so the man who ruined her house is the man her house fed, and the Count has a smell.
- **Keziah.** *New:* her mother died in the third burning, Kestrow's; at twelve she went back into the nave for the blade under the altar; Joab carried her out and she did not see him again until the Oath Stone, sworn. *Why:* draft 6 said the shrine was burned and never said who was in it. `cast.json` still gives her `region: sallow` with a harbour-brawler line; the line is fixed here, the region is a Builder's change (section 14).
- **The drake rider** (id `rook`, name pending from Lotus). *New:* sold to the chapter by her mother for a winter's grain at six; told that day that the drake would be killed if she ever spoke of it; nine years believing it; at fifteen understood the lie was told to keep a child quiet, and has assumed everyone is lying since. *Why:* her flaw had no cause. The rookery names its wards from the birds (which is where "Rook" came from), so her new name should be a bird's (section 13). Corrie, the drake's secret name, is explained: the high hollows where the snow never melts. The drake's three stages, the Drake Warden, and the Sky Captain's frost on landing (#1127) are recorded as built.
- **Wren.** *New:* the Grange holds her father's originals because the armistice lodges each border's survey with the other side's archive; her father's stones were the Stone; his hanging was Hask's doing, and the stones set into the wall at the reseal are the first honest course in eight years. *Why:* her thread was true but unexplained; now it is a third of the lock.
- **Brannock.** *Reworked, per the brief's calibration example.* Before the parish he was a river-road bruiser for the Aldmere boat-guilds, broken on the Harrow quay at twenty-two and left on the parish's alms step; eight years a cook, the violence put down like a tool and the choice made again every day; when the parish burned and nobody came, he picked the tool back up. His flaw (a name over the truth) now has its reason: a name is permission to be what he was. Hask sees it on sight at the Grange, kindly, which is the worst thing anyone has said to him. *Why:* the portrait says a fighter; the brief asked whether to recast or rethink; making the gap the character keeps the face Lotus liked and gives Brannock the one thing he lacked, a past of his own. His voice sheet is rewritten: the old trade has no images on purpose, and a cold reader finds the gap.
- **Dunstan** (takes the name Rook when Lotus names the rider). *New:* a quarryman's son; the shield is his own quarry-board; the bridge was over a Kestrow gorge and the party was Osric, the senior rite-keeper, whom Hask's sworn came for; the lords ran, Dunstan held two days, Osric died anyway, and Kestrow sent Maud. *Why:* "held a bridge while his lords fled" is the stock stoic veteran; naming what was on the bridge ties him to Maud and to Plan A's first part, and it costs no line, since he never speaks of it and she never will.
- **Ansgar.** *New:* indentured by the courier guild at twelve; six years carrying Hask's post, never a seal broken, so he knows every road and most of what was in the letters; third from the end of the line when the warmth reached his elbows. *Proposed:* a plant at the Tollgate, an unnamed courier boy who rides through with Hask's post and flinches at the brazier, so that map 9's meeting is a recognition. *Why:* Code's own campfire worry that Ansgar is the forgettable one, met too late to care about. The plant is text only and touches no tuned board.
- **Bet.** Kept exactly as 0247 and her sheet have her; one detail added (the alder over the oven wall). Her never-hinted rule stands.
- **The other hires.** Kept.

## 7. Ivo Caddell, the lightning mage (new, proposed; Lotus's ruling of 2026-10-06)

Lotus ruled in a new recruit, a man, who brings the lightning school. The pass proposes who, as a direction and not a build.

- **The recommendation: a storm-warden.** Sallow, forty-four. Sallow's salt houses keep a man on the roof with a copper rod and the guild's Lore to catch lightning before it finds the warehouse; Ivo served his indenture to that guild and then stood nineteen years on the roof of Vasse and Daughter, Ottilie's firm, keeping the garrison's salt dry. Struck off when the firm folded. He has caught lightning all his life and never thrown it. He is cheerful, the only cheerful voice in the cast, and his flaw is that he grounds everything: nothing lands on him, grief included, until the first time he throws. *Why this shape:* Lotus wants huge damage and an end-game stun; a man who has held lightning in his hands for nineteen years and finally lets it go is that, with a reason. He ties into the Count (the garrison's salt) and into Ottilie (her father's last line is his wages), and he fills a gap the cast had: nobody in it was happy.
- **Join:** the camp after Harrow Weir, as the sixth main member before the pick. *Why:* no tuned map's party changes (the Tollgate and the Weir are fought without him; from the raid on there is already a bench; the raid is untuned by design), and every run sees lightning, which was Lotus's reason. The alternative, a side character met at the Grange (the archive's roof is a storm-warden's post), costs even less and risks some runs never meeting him.
- **Quests, if he is main cast:** the Salt House (the firm's warehouse on the Cut, eight years of the garrison's salt that Hask's men have come for, because Hollin's full tables are salted with it) and the Rod (the keep roof in a storm before the finale, where he throws for the first time, paying the end-game spell).
- **The alternative character, if a storm-warden is wrong:** the Crown's last clerk at Ironwake. When the garrison was dismissed and the chancery emptied, one clerk stayed and kept the roll for eight years for nobody. He copied the list Alder reads every morning and crossed out Marrit's name because the roll said dead. He has the chancery's Bolt in his desk and has never cast it at a person. His flaw: he believes a thing written down is a thing done, and he wrote down everything Hask did for eight years. Throwing lightning is the first thing he ever did that was not writing. He joins at the raid, out of the chancery door with a book. *Why he is second:* he is the Crown made flesh and ties to the Count even harder, but he shares pages with Pell and the roll with Alder, and a clerk is a cooler idea than a cooler image.
- Chat's direction (round 383: he spends where Pell keeps; Kestrow; maps 5 to 7) is kept where it fits. Sallow is chosen over Kestrow because a Kestrow caster would be a man who left a church that teaches no Lore, which is a second church story beside Maud's and the rider's. Pillar 5's count is Lotus's ruling to record (section 14).

## 8. Brannock and the earth school (proposed; Lotus, 2026-10-06)

Lotus is weighing making Brannock an earth mage or adding one. **Recommendation: Brannock.** Earth is Lore's protective arm (wards, no strikes, no heals). The reworked Brannock is already its shape: a man who looks as if he will hit you, whose one spell is to stand something in front of someone else; his flaw (he wants to hit) plays against his kit (the book will not). The parish's old lay-brother taught him the wall-wards with the bread. The shape: keep the hatchet, add a little Magic, reach earth tomes, and his door becomes the Almoner (a Levy who learned the parish's wards; a real word for the parish officer who gives out alms, and not the genre's). If Lotus adds a character instead, the earth caster should be the one person whose magic nobody fears, and Brannock already is. Nothing is built either way.

## 9. Magic (proposed): the story side

Lotus's school identities (fire burns, ice slows, earth buffs and supports, lightning hits huge with an end-game stun, Light heals, Dark is the grimoire) and the partners' mapping (Table round 383) stand. STORY adds only where Lore comes from in this world (shrine-schools, the academy, the guilds, a parish; Kestrow's church teaches none), and the story of **the Tithe**, the dark grimoire: a book Hask did not burn, carried by a lector, the Kin's warmth taken by hand; Faith's mending refuses whoever carries it; a dark Pell drifts into the sworn's grammar and drops names and is never sworn; a dark Maud loses the goddess's word and her doors close. A dozen lines each, written only when Lotus rules.

**Tomes with a Magic check** (Lotus, 2026-10-06, during the pass): yes to tomes, and yes to the check in place of a rank cap, for three reasons given in STORY: it reads as a number (pillar 2), it makes a tome an object with a promise in it, and it keeps the dedicated casters special without a rule that names them, since the end-game spells ask for a Magic only a caster reaches. Who can learn: anyone with Magic above zero. Every tome is named for where it came from.

## 10. Maps

The map section now says, for every main map and every side map: what the fight is, where, why it matters, what the player protects or chases, what winning and losing cost, and what it should feel like. The numbers stay the Sim's and the Fun Gate's.

- **Side maps are each a piece of the lock or a piece of Plan A**, and the entry says which: the Lazar House and the Long Count carry Hask's unsigned hand on the meal bins; the Warden's Gate is a key in the hands of the grandson of the man who carried its warden; the Cold Kitchen is the oven wall.
- **Harrow Weir** is placed below the Crown's academy at Harrow, so Alder knows the ground (text only; the weir is `tuned` and untouched).
- **Brackwater Cut** is where Ottilie's firm had its wharf (text only).
- **Saltmarsh Ford** is where Hask took his wound (text only).
- **Proposed:** the Ansgar plant at the Tollgate (section 6).
- **Not taken:** Code's campfire idea of a sworn soldier glimpsed at the end of map 1. Map 1's scenes have been through the passes, and the brigands' new silver tipped into Hask's purse is already the plant. Noted here so it is not re-argued.

## 11. Endings

- **The reseal** now says what "by hand" means: the Word said (Maud, or Pell reading, or the captain badly), the Stone set (Wren's stones, or one from the breach), the Count read (the captain reads the list on the hill, the crossed-out name uncrossed if Marrit lives; Bet climbs first). *Why:* section 2. The Held and hand-locked textures, the shard in last, "Same name. Different iron.", one nation governed from the keep, the table still arguing: all kept.
- **The bad ending:** kept ("perfect as written"); one proposed variant sentence under the Grange choice.
- **Under the Hill:** the four conditions, the stumble line, Reseal or Fight, the win and the loss: kept. The Kin's one speaking scene is noted. The Bane variant of the win is marked proposed.

## 12. Game text changed (before and after)

`content/units/cast.json`, `personality` (prints on the unit card in the client; one line each):
- Captain: "Crown-appointed, twenty-four, and the only one here who did not choose it; keeps a list of every cadet's name in a coat pocket and reads it before each map." > "A seam foundling the garrison raised and the Crown commissioned; reads the list of ten names every morning, and stops at the one that is crossed out." *(It contradicted the foundling story.)*
- Wren: "Counts tiles under her breath and is usually right; talks to fill silences and fills a lot of them." > "A surveyor's daughter who counts tiles under her breath and is usually right; talks to fill silences, and fills a lot of them."
- Teodor: "Eldest of nine, gives orders to anyone younger, officers included; follows them himself when nobody is watching." > "Eldest of nine on an Aldmere farm; the pay feeds the other eight. Carries his grandfather's rusted lance and keeps looking at the letters under the rust." *(The old line was the opposite of his flaw.)*
- Ottilie: "Keeps a ledger of every arrow and bills the crown for the ones she loses; has not been paid." > "A salt factor's daughter whose firm fed the garrison for thirty years and folded on Crown debt; keeps a ledger of what the Crown owes, to the ounce."
- Pell: "Reads in the saddle, at table, and once during a drill; sets small fires by accident and large ones on purpose." > "A shrine-school novice who came up through the smoke with one page; reads in the saddle, at table, and once during a drill. Sets small fires by accident and large ones on purpose."
- Dunstan: "Slow to speak and slower to walk; has never once stepped back, and carries the scars of the times he should have." > "A Kestrow shield sergeant who held a bridge two days after his lords left it; carries his own quarry-board for a shield, split to the strap, and does not talk about the bridge."
- Maud: "Mends what she is handed, people included, and says a short prayer over each one whether it wanted one or not." > "The youngest rite-keeper Kestrow ever sent, raised in a lazar house; mends what she is handed, people included, and says the prayer over the enemy dead because nobody else will."
- Ansgar: "Rode here on a horse he says was a gift; the horse's former owner says otherwise." > "A courier Sallow sold to the Warden's companies at twelve, who ran the night before his swearing on the horse they gave him for it; flinches at fires."
- The rider: "Grew up on a cliff and distrusts flat ground; goes high when nervous, which is exactly when she should not." > "The church's escort for an animal the other faiths say no longer exists, one ledger line below it; assumes everyone is lying until proven otherwise, and is kinder to the drake than to people."
- Keziah: "A harbour brawler who has never lost a fight she started, and laughs at the wrong moment in every one of them." > "An outland shrine-keeper's daughter who carries the one thing she took out of the fire; angry, generous, superstitious, and right more often than anyone credits." *(The old line was from before she was an outlander.)*
- Brannock: "Was the best farmhand in his parish and says so; treats every drill as a race and every race as a drill." > "A parish lay-brother and cook who rang the bell for an hour while his parish burned and nobody came; looks like a fighter, and was one, once."

`content/campaign.json`, the field's before card (the last main-line placeholder; third person, present, per rounds 344 to 346; the rules line kept, its placeholder prefix dropped):
- "Hollin is a sworn village, a day short of the keep. Nobody in it is hungry or fighting, every hearth is lit, and nobody meets the captain's eye."
- "A girl at the well, asked her name, says, "We're well, thank you," and goes back to the bucket."
- "Past Hollin, the keep. The rime on its walls has started to run."

`content/weapons.json` and `content/items.json` (72 characters at most each; "Placeholder." dropped):
- Kinsbane: "Placeholder. Drains 5 HP unfed. Kills heal 10." > "Outland reaping blade, never used on grain. Something in it is hungry."
- The Family Lance, held: "The rust holds. What is under it is waiting for a hand it knows." Its three stages keep their lines with the prefix dropped.
- Ottilie's Tally: "Iron limbs notched like a ledger's margin. She knows every mark."
- Pell's Commonplace: "Copied out by hand, margins full. None of it will burn." (unchanged but for the prefix)
- Maud's Psalter: "Placeholder. Her order's prayers in her own hand, and some of hers." > "Her order's prayers in her own hand, with one word changed."

`content/units/enemies.json`: the Sworn Captain gains "A boy of the oath's first winter. Twenty-four now, and never once cold." (Chat's lean from round 361, which STORY now carries.)

**Not changed:** the branch pitch lines (both voice sheets keep them); the scene scripts and quest cards already through the passes; Bet's text (0146's rule); the hires' menu lines; every line of Lotus's.

## 13. Names for Lotus to pick

- **The drake rider.** The rookery names its wards from the birds, so a bird's name keeps the custom and explains "Rook" in passing. Offers, for a silver-haired woman with ice-blue eyes: **Merle** (a blackbird; the dark name on the pale girl is the right kind of wrong), **Linnet**, **Siskin**, **Teal**. Not Corbie or Daw: both are crows, too near the name she is giving up. Recommended: Merle.
- **The drake.** Keep **Corrie**: a corrie is the high fell hollow where snow lies all summer, which is where it sleeps best, and it is a word a Kestrow child would know. It was a placeholder; it has earned its place.
- **Ivo Caddell** is a placeholder for the lightning mage. **Osric** (the dead rite-keeper), **Vasse and Daughter** (the firm), **Sorrel Bend**, **Thane's Hollow**, **the Almoner**, **the Tithe**, **the Salt House** and **the Rod** are placeholders too.
- **Alder Fenn** stands; the pass only gives the name a source.

## 14. Follow-ups for Code (not done in this pass)

- `cast.json` still gives Keziah `region: sallow`. The story says the outlands, and `origins` already has `outlands`, but `Rivalry.cs` reads a recruit's region to pair rivals, so the change can move numbers on the field and wants a Sim re-read; `StarterContentTests` also counts three regions of three or four. A Builder issue, not a text edit.
- Pillar 5 and DESIGN 14's arrival table change when Lotus takes Ivo (and Brannock's class, if he takes the earth proposal). Lotus's ruling on the lightning mage is recorded on the Table (2026-10-06).
- If the rite in three parts is kept, the reseal card's Held line should say which hand did which part; no rule changes, and #634 owns the card.
- The proposed Ansgar plant is one incidental line in the Tollgate's after scene when that scene is written (#1001's format).

## 15. Why the pass worked this way

The brief asked for people who are different and have things to bond over, maps that tie into the main story, and one fluent world. The pass did not add plot; it gave every existing thread a place in one structure (the rite), gave every character the one night their flaw was decided, and wrote down what each of them shares with someone else. The villain's spine and every line Lotus wrote are exactly where they were.
