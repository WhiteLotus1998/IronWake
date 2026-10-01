# 0139 — The forge and Refine as built: the smith's map, the step, the stores, and "exactly enough"

Date: 2026-10-01. Issue 647, building DESIGN 13.20's forge and section 14's Refine (rounds 192 to 194, 206, 207; DECISIONS/0137). Code's calls, recorded as implementation choices; every number is provisional, and the Table may move any of them.

## Decided

- **The forge is a `rooms` entry** with `"forge": true`, 600, no beds, at most one. Its `after` names the campaign map after whose win it may be built: **the Tollgate** (the issue's lean). The smith's story event is a card behind #656. Until then the camp prints `opens once the_tollgate is won`.
- **Refine is `refine <unit> <slot> mt|hit`** at the camp, once the forge stands.
  - Each step adds +1 Mt or +5 hit, never weight or crit, and costs one material plus 100 from the purse. The numbers live in `campaign.json`'s `forge` block.
  - The steps live on the stack (`refines`, `refineMt`, `refineHit`), so they travel with the weapon and survive a save. `Forge.Shape` applies them after Kinsbane and the heirloom at `BattleUnit`'s two shaping sites, so forecast, resolver, menu and `threat` read them unchanged.
  - The pack prints `Iron Lance +2`.
- **What refines on what:**
  - A priced shop weapon takes 2 steps on common.
  - A signature bound to the captain or to a member with a quest (the main line) takes 3 on rare. Any other bound weapon (a side character's, since side characters have no quests) takes 2 on common.
  - Kinsbane never Refines, nor does a healing spell or an unpriced weapon nobody is bound to.
  - The heirloom: the smith's rust line while rusted, refused while short of woken, and refined on top of its last stage once woken.
- **Materials are stores on the record** (`commonMaterial`, `rareMaterial`), not pack items. A pack item would sit in a pack through a battle, where `use`, the enemy AI and the Sim's heuristic all read items.
  - A quest pays them on a win (`common`, `rare`).
  - Maud's quest 1 pays 2 common, so the forge does something in today's campaign.
  - Chests holding materials ride #679, whose overflow goes to the wagon. No shipped map has a chest yet.
- **"Exactly enough" is read as what the campaign issues.** The rare in all quests equals 3 for each signature a quest `pays` that refines on rare. `family_lance` is in content, but nothing issues it until #656, and it refines on whatever its owner's line says when it is issued.
  - The loader refuses one short and one over, naming `campaign.json > quests > rare`.
  - Shipped content issues none and pays no rare.
- **The leave warning:** on `march`, one line per unit going to the map whose equipped physical weapon has under 5 uses: `low: <name>'s <weapon> has <n> uses`. It never refuses. The smarter number (the map's expected strikes) waits until the Sim can supply it cheaply.
- **Fixture copies of the content drop `forge` and the quests' material payouts**, since every journaled transcript before this one predates them.

## What this leaves open

- The numbers. +1 Mt or +5 hit at 100 a step is a lean. The forge at 600 plus a step or two is a wall and a half; whether that trade is felt is the 13.20 kill clause's question.
- Which map the smith is met on, and what the smith says, go to the #656 story pass.
- The item card in battle names the base weapon. The forecast numbers are the refined ones. Showing `+n` there is a follow-up if a play finds it confusing.
