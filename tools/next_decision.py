#!/usr/bin/env python3
"""Print the next free decision record number, counting records still in review.

Two PRs open at once used to take the same number (0256 and 0264 were each issued twice,
issue 1291), because each counted only the records on its own base. This tool counts every
record in the working tree, on origin/main, and on every other branch on origin, and prints
one past the highest as four digits.

How to use it, after rebasing on main and before naming the file:

    python3 tools/next_decision.py            # fetches origin first
    python3 tools/next_decision.py --no-fetch # counts what is already fetched

It only reads: `git fetch origin` (every branch, whatever the clone's refspec) and
`git ls-tree`. Two runs that name a record in the same minute can still collide; DecisionRecordTests then fails on the later PR, which renumbers.
"""
import os
import re
import subprocess
import sys

NUMBERED = re.compile(r"^(\d{4})-.*\.md$")
FOLDER = "docs/DECISIONS"


def git(*args):
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def numbers(names):
    found = set()
    for name in names:
        match = NUMBERED.match(os.path.basename(name))
        if match:
            found.add(int(match.group(1)))
    return found


def main(argv):
    if "--no-fetch" not in argv:
        git("fetch", "origin", "--quiet", "+refs/heads/*:refs/remotes/origin/*")
    root = git("rev-parse", "--show-toplevel").strip()
    taken = numbers(os.listdir(os.path.join(root, FOLDER)))
    refs = git("for-each-ref", "--format=%(refname)", "refs/remotes/origin").split()
    for ref in refs:
        if ref.endswith("/HEAD"):
            continue
        taken |= numbers(git("ls-tree", "--name-only", ref, FOLDER + "/").split("\n"))
    print("%04d" % (max(taken, default=0) + 1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
