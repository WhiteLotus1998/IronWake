#!/usr/bin/env python3
"""Regenerate journaled transcripts after a change to what the console prints.

The journaled plays under docs/transcripts/ are replayed by tests that compare the console
output with the committed .txt byte for byte. A change to a printed line (a new forecast line,
a reworded event) fails every replay that prints it. This tool replaces each failing transcript
with its own replay, and nothing else.

How to use it:

    mkdir -p /tmp/iw-cap
    IRONWAKE_CAPTURE_DIR=/tmp/iw-cap dotnet test --no-build --filter "<the failing replay tests>"
    python3 tools/rejournal.py /tmp/iw-cap            # report: what would change, and why
    python3 tools/rejournal.py /tmp/iw-cap --apply    # write the transcripts
    git diff --stat docs/transcripts                  # review, then run the full suite

`ConsoleCapture` (tests) writes every capture into IRONWAKE_CAPTURE_DIR when it is set. A
capture replaces a transcript only when it is the same play: the same first line (map, seed and
scheme) and the same echoed commands ('> ...' lines) in the same order. A transcript matched by
two different captures is skipped and reported, and so is one matched by none. The tool never
touches scripts, only the .txt beside them, and it keeps each file's line endings. Every
transcript is tracked by git, so a run is fully reversible (`git checkout -- docs/transcripts`).

It does not change commands. A change to the command language itself (a new required suffix,
a renamed verb) needs the scripts edited, which this tool deliberately leaves to a person or a
reviewed PR.
"""
import glob
import os
import sys


def commands(text):
    return [line for line in text.split('\n') if line.startswith('> ')]


def first_line(text):
    return text.split('\n', 1)[0]


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    capdir, apply = argv[1], '--apply' in argv[2:]
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    caps = []
    for path in sorted(glob.glob(os.path.join(capdir, '*.txt'))):
        with open(path, encoding='utf-8', newline='') as fh:
            caps.append(fh.read().replace('\r\n', '\n'))
    if not caps:
        print(f'no captures in {capdir}; run the failing tests with IRONWAKE_CAPTURE_DIR set first')
        return 1

    changed, ambiguous = [], []
    for txt in sorted(glob.glob(os.path.join(root, 'docs', 'transcripts', '*.txt'))):
        with open(txt, encoding='utf-8', newline='') as fh:
            raw = fh.read()
        want = raw.replace('\r\n', '\n')
        cmds, head = commands(want), first_line(want)
        if not cmds:
            continue
        hits = {c for c in caps if c != want and first_line(c) == head and commands(c) == cmds}
        if any(c == want for c in caps if first_line(c) == head and commands(c) == cmds):
            continue
        if len(hits) > 1:
            ambiguous.append(txt)
            continue
        if len(hits) == 1:
            new = hits.pop()
            old_lines, new_lines = want.split('\n'), new.split('\n')
            removed = [l for l in old_lines if l not in set(new_lines)]
            added = [l for l in new_lines if l not in set(old_lines)]
            changed.append((txt, raw, new, removed, added))

    for txt, raw, new, removed, added in changed:
        rel = os.path.relpath(txt, root)
        print(f'{rel}: -{len(removed)} +{len(added)}')
        for l in removed[:3]:
            print(f'    - {l}')
        for l in added[:3]:
            print(f'    + {l}')
        if apply:
            out = new.replace('\n', '\r\n') if '\r\n' in raw else new
            with open(txt, 'w', encoding='utf-8', newline='') as fh:
                fh.write(out)
    for txt in ambiguous:
        print(f'{os.path.relpath(txt, root)}: skipped, two different captures replay this play')
    verb = 'rewrote' if apply else 'would rewrite'
    print(f'-- {verb} {len(changed)} transcripts; {len(ambiguous)} ambiguous')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
