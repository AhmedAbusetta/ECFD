"""Test 0 - the brain on typed Egyptian sentences.

Pass line: at least 15 of 20 exactly correct, and no invalid quote survives.
"Exactly correct" = the set of accepted labels equals the expected set (confidence ignored).

Each sentence is classified once and stored in test0/results_<model>.json; re-running
re-uses stored answers unless --fresh is given (saves free-tier quota, keeps results stable).
The dashboard's Tests page uses the same stored answers.

Usage (from the repo root):
    python ml/brain/run_test0.py
    python ml/brain/run_test0.py --fresh
    python ml/brain/run_test0.py --stored-only
"""

import argparse
import os
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from ecfd_brain import Brain  # noqa: E402
from ecfd_brain import test0  # noqa: E402


def load_env(path: Path) -> None:
    """Minimal .env reader (KEY=value lines). Values are never printed."""
    if not path.exists():
        return
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            key, value = line.split("=", 1)
            os.environ.setdefault(key.strip(), value.strip().strip('"').strip("'"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fresh", action="store_true", help="ignore stored answers and call the LLM again")
    parser.add_argument("--stored-only", action="store_true",
                        help="score only answers already saved (no API calls), e.g. when the daily quota is used up")
    parser.add_argument("--delay", type=float, default=2.5, help="seconds between calls (free-tier rate limit)")
    args = parser.parse_args()

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    load_env(HERE.parents[1] / ".env")

    brain = Brain.from_env()
    model = brain.provider.model
    data = test0.load_items()
    store = test0.load_store(model, fresh=args.fresh)

    scored, called = [], False
    for item in data["items"]:
        if item["id"] not in store["answers"] and not args.stored_only:
            if called:
                time.sleep(args.delay)
            try:
                result = brain.classify(item["text"], item.get("context", []))
            except RuntimeError as e:
                if "429" not in str(e):
                    raise
                print(f"Daily free-tier quota used up at {item['id']}; scoring the saved answers. Re-run tomorrow.")
                args.stored_only = True
                continue
            called = True
            store["answers"][item["id"]] = {"raw": result.raw, "latency_ms": result.latency_ms}
            test0.save_store(model, store)
        if item["id"] in store["answers"]:
            scored.append((item, test0.score_item(item, store["answers"][item["id"]])))

    print(f"\nTest 0  model={model}  prompt={test0.prompt_version()}\n")
    for item, s in scored:
        print(f"{'PASS' if s['ok'] else 'MISS'} {item['id']}  {item['text']}")
        if s["missing"]:
            print(f"      missing: {', '.join(s['missing'])}")
        if s["extra"]:
            print(f"      extra:   {', '.join(s['extra'])}")
        for t in s["accepted"]:
            print(f"      + {t['label']:<26} {t['confidence']:<6} «{t['quote']}»")
        for t in s["dropped"]:
            print(f"      x {t['label']:<26} dropped ({t['reason']}): «{t['quote']}»")
        if s["needs_more_context"]:
            print("      ? needs_more_context")

    summary = test0.summarize([s for _, s in scored], len(data["items"]))
    if summary["answered"] < summary["total"]:
        print(f"\nPARTIAL RUN: {summary['answered']} of {summary['total']} sentences answered so far")
    print(f"\nScore: {summary['correct']}/{summary['answered']} exactly correct (pass line {summary['pass_line']}/{summary['total']})")
    print(f"Invalid quotes that survived: {summary['invalid_quotes_kept']} (pass line 0)")
    if summary["median_ms"] is not None:
        print(f"LLM latency: median {summary['median_ms']} ms, max {summary['max_ms']} ms")
    print("TEST 0:", "PASS" if summary["passed"] else "FAIL")
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
