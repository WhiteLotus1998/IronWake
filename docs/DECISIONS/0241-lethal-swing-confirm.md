# 0241: an attack into a lethal counter asks first

Date: 2026-10-04. Issue #975 (rounds 336 and 337 on the Design Table). Built by Code's desktop session from the chain Builder's branch.

## Decided

- In the console, interactive and `--script`, an `attack` whose forecast prints `Counter: lethal to <unit> (...)` (issue 539) is refused and nothing is spent. The refusal reads `counter is lethal to <Name> (<dmg> against <hp> hp); add ! to swing anyway: <the line> !`.
- `attack ... !` swings anyway. A `!` on an attack that isn't counter-lethal is accepted and ignored. Under `--strict` an unconfirmed lethal swing stops the script.
- Only the attacker's own death asks. A certain first-round kill never prints the line (issue 539), so it never asks.
- The Core, the AI, the Sim's planner and the protocol are unchanged. The Sim's `--trace` and campaign scripts write `!` on such attacks. The client's script reader (`Ironwake.Client.Script`) drops a trailing `!`, since the command is the same attack.

## Committed scripts

Every committed script that swings into a lethal counter gained a `!`, along with the echoed line in its transcript. That came to 60 transcripts (58 with scripts, 2 without), found by reading each transcript's echoed `attack` lines against the lethal line under them. Another 19 scripts whose transcripts predate issue 539's line were found by replaying them under `--strict` and marking each refused line. `tests/parity/campaign/full-campaign-631.script` was rewritten by the Sim, and `the_tollgate-113.script` was marked by replay. Every replay matches its transcript apart from the echoed line. The chain Builder's sandbox refused the bulk edit as destructive, so the desktop session made it. Git keeps every original.

## Tests

`LethalSwingConfirmTests`: a lethal swing is refused and spends nothing; the same line with `!` swings; a safe swing runs with or without `!`; `help` prints the rule. 4379 tests pass.
