"""Parses each YAML file named on the command line and exits 1 naming every
file that does not parse. Files are read as bytes so that no newline
translation hides a stray carriage return (#654 shipped one inside a run
block, and GitHub refused the whole workflow; #970)."""

import sys

import yaml


def main(paths):
    if not paths:
        print("parse_workflows: no files given")
        return 1
    failed = 0
    for path in paths:
        with open(path, "rb") as f:
            data = f.read()
        try:
            doc = yaml.safe_load(data)
        except yaml.YAMLError as e:
            print(f"{path}: does not parse as YAML: {e}")
            failed += 1
            continue
        if not isinstance(doc, dict) or "jobs" not in doc:
            print(f"{path}: parses, but has no top-level 'jobs' mapping")
            failed += 1
            continue
        print(f"{path}: parses")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
