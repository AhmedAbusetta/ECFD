"""Test 2 - file the recordings teammates sent under the right call and turn.

Each person named every recording with their name and the line number from their list:
    "Ola 1.m4a", "Ola 2.m4a", "علا 3.m4a", "Jana 4.ogg", "ahmed badr_05.mp3" ...
Put everything you received in one folder (sub-folders are fine; a sub-folder named after the
person also works if the file names only have the number), then run (repo root):

    python ml/asr/test2/import_recordings.py incoming          # dry run: shows the mapping and problems
    python ml/asr/test2/import_recordings.py incoming --copy   # copies into recordings/ as T2_<call>_<turn>_<Name>

Names are matched to team.json: the file-name form (Ola), the display name (علا), or a first name
when only one person has it (Jana - but not Ahmed, there are four). The line number is the first
number in the file name, so "Ola 1 (2).m4a" is still line 1. If one line was sent twice, the newer file wins.
"""

import argparse
import json
import re
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
AUDIO = {".m4a", ".mp3", ".wav", ".ogg", ".opus", ".aac", ".flac", ".webm", ".3gp", ".amr", ".mp4"}


def squash(text: str) -> str:
    """Lower-case letters only (Arabic or Latin): 'Ahmed Badr_' -> 'ahmedbadr'."""
    return "".join(ch for ch in text.lower() if ch.isalpha())


def aliases(team: list) -> dict:
    """Every way someone might write a person's name -> their file name. Ambiguous aliases are dropped."""
    seen = {}
    for p in team:
        forms = {p["name"], p["display"]}
        forms.update(re.split(r"[()]", p["display"]))                 # "علا (Ola)" -> "علا", "Ola"
        forms.update(re.findall(r"[A-Z][a-z]*", p["name"]))           # "JanaSameh" -> "Jana", "Sameh"
        forms.update(w for part in re.split(r"[()]", p["display"]) for w in part.split())
        for form in forms:
            key = squash(form)
            if len(key) >= 2:
                seen.setdefault(key, set()).add(p["name"])
    return {k: names.pop() for k, names in seen.items() if len(names) == 1}


def identify(path: Path, incoming: Path, known: dict):
    """(person, line number) from the file name, falling back to the folder name for the person."""
    numbers = re.findall(r"\d+", path.stem)
    number = int(numbers[0]) if numbers else None
    person = known.get(squash(re.sub(r"\d+", " ", path.stem)))
    for folder in path.relative_to(incoming).parents:
        if person:
            break
        if folder.name:
            person = known.get(squash(folder.name))
    return person, number


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("incoming", help="folder with all the recordings that were sent")
    parser.add_argument("--copy", action="store_true", help="actually copy the files (default: only show the mapping)")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    order = json.loads((HERE / "pack" / "assignments.json").read_text(encoding="utf-8"))["order"]
    team = json.loads((HERE / "team.json").read_text(encoding="utf-8"))
    known = aliases(team)
    incoming = Path(args.incoming)

    chosen, unreadable, duplicates = {}, [], []
    for path in sorted(p for p in incoming.rglob("*") if p.is_file() and p.suffix.lower() in AUDIO):
        person, number = identify(path, incoming, known)
        if not person or number is None or not 1 <= number <= len(order[person]):
            unreadable.append(path.relative_to(incoming))
            continue
        key = (person, number)
        if key in chosen:
            older, newer = sorted([chosen[key], path], key=lambda p: p.stat().st_mtime)
            duplicates.append(f"{person} {number}: kept {newer.name}, ignored {older.name}")
            chosen[key] = newer
        else:
            chosen[key] = path

    out_dir = HERE / "recordings"
    missing_total = 0
    for p in team:
        name, lines = p["name"], order[p["name"]]
        have = [n for n in range(1, len(lines) + 1) if (name, n) in chosen]
        missing = [n for n in range(1, len(lines) + 1) if (name, n) not in chosen]
        missing_total += len(missing)
        print(f"{p['display']:<24} {len(have):>2}/{len(lines)}" + (f"   missing lines: {missing}" if missing else "   complete"))
        for n in have:
            src = chosen[(name, n)]
            dest = out_dir / f"T2_{lines[n - 1]}_{name}{src.suffix.lower()}"
            if args.copy:
                out_dir.mkdir(exist_ok=True)
                shutil.copy2(src, dest)

    if duplicates:
        print("\nSent twice (newest kept):\n  " + "\n  ".join(duplicates))
    if unreadable:
        print("\nCouldn't tell whose / which line (rename to 'Name number', e.g. 'Ola 3'):\n  "
              + "\n  ".join(str(u) for u in unreadable))
    if not args.copy:
        print("\nDry run - nothing copied. Run again with --copy.")
    else:
        print(f"\nCopied {len(chosen)} files to {out_dir}. Next: run_recorded_calls.py --check")
    return 1 if (unreadable or missing_total) else 0


if __name__ == "__main__":
    sys.exit(main())
