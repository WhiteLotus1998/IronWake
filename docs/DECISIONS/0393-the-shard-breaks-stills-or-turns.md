# 0393: the shard breaks, stills or turns

Date: 2026-10-09. Issue #1386, slice 3e. Answers 0390's open question, whether breaking the shard under the hill touches Frozen Iron, as the Table settled it: rounds 550 to 555, with DIALOGUE in #1476 and #1477. Both rules are on samples until Chat's cold chair has played them.

## Decided

- **A broken shard must change the hill's clock** (551). Leaving the frost alone is rejected, because the 1549, 1550 and 1552 plays each broke the shard and lost to the same clock.
- **`shard_breaks: stills | turns`** is a map header, refused unless the map also carries `kin_shard:` and `swallowed:` (`MapDefinition.ShardBreaks`, `ShardBreak`). There is no campaign default: `content/hill/under_the_hill.map`, its plays and the keep are unchanged.
- **`stills`** (Code's round-550 lean, withdrawn in 552 and built for the chair to judge). From the break, Frozen Iron holds the dose it reached and climbs no more (`Swallow.Stilled`, `frozenIronStilled`).
  - **Code's guard, not yet agreed:** the held dose is never below the stage's step (3). The 1553 play broke the shard on turn 2 and held the frost at 0, and on a `race` stage nothing else ends the map, so a turtle would never lose. With the guard, `stills` cannot collapse to "off". It can still collapse to "slow". If the Table wants the bare collapse shown, the guard comes off.
- **`turns`** (Chat's lean, 551 and 555; both partners' lean from 552). From the break, Frozen Iron keeps climbing and lands on the Kin too (`Swallow.Turned`, `frozenIronTurned`, `frozenIronFell.turned`).
  - It never takes him below his **floor**: half his current stage's max HP, rounded up (`Swallow.Floor`, the risen's rule). Below the floor the frost skips him, and it never raises him. The Kin's heal is unchanged.
  - The floor follows his current max HP, so it resets with the stage. The swallow row prints `him included but never below N`.
  - The frost alone can never kill him, so 552's tie rule is unreachable and is dropped (555).
  - **The turtle** (553) is pinned in `ShardBreaksTests`: the shard broken early, the captain healed to full every player phase, the Kin never struck. That board loses, and the Kin never drops below his floor.
  - On the hill the turn limit ends nothing while he stands swallowed (`race`). The turtle loses to the climbing dose, not to the limit.
  - `turns` ends Lotus's exemption (514) on the hill. It goes on his sign-off with the Kin's numbers.
- **The samples:** `docs/samples/under_the_hill_stills.map` and `docs/samples/under_the_hill_turns.map`, each 0391's campaign board plus the header.

## Read

- **Sim**, `--finale ... --seeds 200`, full / depleted / floor wins out of 200 (`docs/measurements/under_the_hill_{stills,turns}-1386.txt`):

  | Board | full | depleted | floor | Floor's median length |
  |---|---|---|---|---|
  | 0391's board, no header | 164 | 122 | 17 | 7 turns |
  | `stills` | 188 | 158 | 41 | 10 turns |
  | `turns` | 185 | 146 | 44 | 7 turns |

  Every loss is the frost's.
- **Code 1553, warm, full company, `stills`, 8/6/6** (`docs/transcripts/2026-10-09-under_the_hill_stills-1553.*`). Lost on turn 5 with Hask at 2. The frost held at 3, a clock you can count. Once the sworn were down, the only question was Def 12.
- **Code 1553, warm, full company, `turns`, 9/7/7** (`docs/transcripts/2026-10-09-under_the_hill_turns-1553.*`). The same opening to the turn-2 break. Won on turn 6 with the captain at 4. The frost took Hask from 20 to his floor while I cleared the sworn. Then five swings took him to 3, below the floor where the frost skips him, and the Kin healed him to 5. On turn 6 Maud's 3, Brannock's 1 and the captain's 3 at 81 % finished him.

## Not decided here

- The `stills` guard (Table).
- Which rule ships to the campaign hill: Chat's cold chair on both, then Lotus's sign-off for `turns`.
- The Kin's stage HP, a feel number for the chair and Lotus.
