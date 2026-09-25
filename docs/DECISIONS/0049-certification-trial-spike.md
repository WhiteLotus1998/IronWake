# 0049 — Certification as a one-turn puzzle map (13.6): the spike

Date: 2026-09-25. Issue 73 (DESIGN 13.6), built by Code; the review is on the issue. The shape below is implementation and reversible. Keep or kill is not decided here: it waits on Chat's play, within the two sessions CLAUDE.md gives an experiment.

## Decisions

1. **One header, `certification: <class> <item> [<item> ...]`.** A map carrying it is a trial for that class, played with that loadout. It has exactly one player slot, the captain's. Whoever fills the slot plays *in the trial's class*, carrying only the loadout, each stack at full uses. Its own level, stats, ranks and mastery stay. A pikeman trying out for Bulwark has to play an armoured unit with Res 2 for a turn. That is the only reading under which a trial proves anything about the class. The header is validated (unknown class, unknown item, a weapon the class cannot wield, more than five items, a second player slot) and written back canonically, so the protocol's state round-trips it.
2. **`play <map> --candidate <id>` fields a cast unit.** The default is the captain. It is refused on a map without the header and for an id not in the cast. The console opens with the trial line and the map's events, since a lever the player cannot see is not a puzzle. It closes with `certification: <unit> earned <Class>` or `<Class> not earned` once the battle is over.
3. **Nothing is granted.** No campaign record exists until #74, so a win changes no unit. Whether a trial replaces #72's requirements, sits beside them, or is the seal's alternative is the Table's.
4. **Two samples, under `docs/samples/certification/`** so that no gated map moves:
   - `bulwark_trial.map`: survive one enemy phase against four level-3 brigands and two Hold hexers. Exactly one tile inside the candidate's reach, 5,3 in the corridor, takes one melee swing and no Cinder: worst case 8 against 25 HP. A second line is a bet. Kill the north hexer from 3,1 (13 x2 at 88, Cinder 11 back), then face three brigands on 14 HP. It won 3 of 10 seeds.
   - `outrider_trial.map`: Escape in one turn. The wall has three gates and three levers (`enter` events), and the Mov-6 outrider must Move onto a lever, Wait, and Canto through the gate it opened. Only the east lever leaves enough. The centre lever sits in forest and comes up one short. The west lever needs a forest crossing or a detour and comes up short either way.

## Played

Code, by hand (PLAYTEST.md). Bulwark seed 11: held the corridor and won. Bulwark seed 12: took the hexer bet and lost. Outrider seed 7: the centre lever was refused one tile short, and the east lever won.

## The lean, for Chat to argue

A trial is a puzzle when it has a proof and a bet. It is a quiz when it has one right sum. The Bulwark trial is the first kind. `threat` proves the corridor, and the hexer kill is a real temptation with a real price. The Outrider trial is the second kind. Once the player knows the forest costs 3, it is arithmetic, and the refusal message does the arithmetic for them. Lean: keep the mechanism, author a trial only where the class has a bet worth refusing, and say so in 13.6. Nine trials would be a toll booth. A handful at the classes whose rule is a trap for the unwary (the bulwark's Res, the flier's bows, the outrider's Canto budget with an enemy in it) might be the best teaching in the game.

## Not decided

- Keep or kill (Chat's play).
- Whether a trial earns EXP, weapon points or mastery points in the trial class. Today it would, and nothing carries them anywhere until #74.
- Whether a trial stands in for #72's requirements or comes after them.
