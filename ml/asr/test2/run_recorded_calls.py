"""Test 2 - the team's recorded calls through the real pipeline: phone-quality ASR -> rules -> AI analyst.

Every turn of a call is a separate recording by the person who played that side:
    recordings/T2_<call>_<turn>_<Name>.<ext>      e.g. T2_K13_05_Ola.m4a
The calls are put back together in order, each turn is converted to phone quality (8 kHz mu-law)
and transcribed, and then the call is scored turn by turn exactly like the live system would:

  Speech-to-text   keyword recall and word error rate, split by call type (normal / lookalike / scam)
  Detection        scams caught in time, FALSE ALARMS on legitimate calls, warning lead time,
                   for rules only, AI analyst only and the hybrid ECFD uses

Transcripts are cached in results/ per engine, AI answers per model, so re-running is free.

Usage (repo root, ASR virtualenv):
  ml/asr/.venv/Scripts/python ml/asr/test2/run_recorded_calls.py --check        # which files are missing?
  ml/asr/.venv/Scripts/python ml/asr/test2/run_recorded_calls.py                # full run (Cohere + AI analyst)
  ml/asr/.venv/Scripts/python ml/asr/test2/run_recorded_calls.py --no-llm       # rules only, no LLM quota
  ml/asr/.venv/Scripts/python ml/asr/test2/run_recorded_calls.py --compare-text # also score the written scripts
Options: --engine speechmatics|whisper-service|cohere-modal, --call K13, --allow-missing, --recordings DIR
"""

import argparse
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / "ml" / "asr" / "test1"))
sys.path.insert(0, str(ROOT / "ml" / "brain"))

import score_test1 as t1  # noqa: E402
import run_call_replay as replay_tool  # noqa: E402
from ecfd_brain.analyst import CallAnalyst, Turn  # noqa: E402
from ecfd_brain.floor import HardSignalFloor  # noqa: E402

t1.HERE = HERE  # transcripts cache -> ml/asr/test2/results/
FILE_PATTERN = re.compile(r"^T2_(K\d{2})_(\d{2})_([A-Za-z0-9]+)\.(m4a|mp3|wav|ogg|opus|aac|flac|webm|3gp|amr)$", re.IGNORECASE)
KINDS = ["normal", "lookalike", "scam"]


def load_calls(only: str = None) -> list:
    calls = json.loads((HERE / "calls.json").read_text(encoding="utf-8"))
    groups = calls["keyword_groups"]
    picked = [c for c in calls["calls"] if not only or c["id"] == only.upper()]
    return picked, groups


def index_recordings(rec_dir: Path) -> tuple:
    found, unknown = {}, []
    for p in sorted(rec_dir.glob("*")) if rec_dir.exists() else []:
        m = FILE_PATTERN.match(p.name)
        if m:
            found[(m.group(1).upper(), int(m.group(2)))] = p
        elif p.is_file():
            unknown.append(p.name)
    return found, unknown


def check(calls: list, found: dict, unknown: list) -> int:
    plan_path = HERE / "pack" / "assignments.json"
    plan = json.loads(plan_path.read_text(encoding="utf-8")).get("roles", {}) if plan_path.exists() else {}
    missing_by_person = {}
    total = have = 0
    for c in calls:
        for i, turn in enumerate(c["turns"], 1):
            total += 1
            if (c["id"], i) in found:
                have += 1
                continue
            who = plan.get(c["id"], {}).get(turn["speaker"], "?")
            missing_by_person.setdefault(who, []).append(f"T2_{c['id']}_{i:02d}_{who}")
    print(f"Recordings: {have}/{total} lines present.")
    for who, names in sorted(missing_by_person.items()):
        print(f"  {who}: {len(names)} missing -> {', '.join(names)}")
    if unknown:
        print(f"Files with a wrong name (ignored): {', '.join(unknown)}")
    return 0 if have == total else 1


def transcribe_calls(calls: list, found: dict, engine, allow_missing: bool) -> dict:
    """call id -> list of (Turn with the ASR text, reference text, transcribed?)."""
    cache = t1.load_cache(engine)
    out = {}
    for c in calls:
        turns, complete = [], True
        for i, ref in enumerate(c["turns"], 1):
            path = found.get((c["id"], i))
            if path is None:
                if not allow_missing:
                    complete = False
                    break
                turns.append((Turn(ref["speaker"], ref["text"]), ref, False))
                continue
            try:
                text = t1.transcribe_cached(engine, path, cache)["transcript"] or ""
            except RuntimeError as e:
                print(f"  ! {path.name}: {e}")
                text = ""
            turns.append((Turn(ref["speaker"], text or "…"), ref, True))
        if complete:
            out[c["id"]] = turns
        else:
            print(f"  skipping {c['id']}: not all turns recorded (use --check, or --allow-missing)")
    return out


def asr_report(calls: list, transcribed: dict, groups: dict) -> None:
    print("\nSpeech-to-text (recorded turns only)")
    print(f"{'type':<10} {'turns':>5} {'keywords caught':>16} {'median WER':>11}")
    by_kind = {c["id"]: c["kind"] for c in calls}
    for kind in KINDS + ["all"]:
        found = expected = 0
        wers = []
        for cid, turns in transcribed.items():
            if kind != "all" and by_kind[cid] != kind:
                continue
            for turn, ref, real in turns:
                if not real:
                    continue
                norm = " ".join(t1.words_of(turn.text))
                for g in ref["keywords"]:
                    expected += 1
                    found += any(t1.contains_variant(norm, v) for v in groups[g])
                wers.append(t1.wer(ref["text"], turn.text))
        if wers:
            wers.sort()
            kw = f"{found}/{expected}" if expected else "-"
            print(f"{kind:<10} {len(wers):>5} {kw:>16} {wers[len(wers) // 2]:>10.0%}")


def detection_report(title: str, scenarios: list, results: dict) -> None:
    print(f"\n{title}")
    print(f"{'call':<5} {'type':<10} {'label':<6} {'alert turn (rules · AI · hybrid)':<34} {'deadline':>8}  verdict")
    for sc in scenarios:
        rows = results[sc["id"]]
        first = {s: replay_tool.first_alert([o[s]["level"] for (_, _, _, o) in rows]) for s in replay_tool.SYSTEMS}
        fmt = " · ".join("-" if first[s] is None else str(first[s]) for s in replay_tool.SYSTEMS)
        hybrid = first["hybrid"]
        if sc["label"] == "LEGIT":
            verdict = "FALSE ALARM" if hybrid else "ok"
        else:
            verdict = "caught in time" if hybrid and hybrid <= sc["alert_by"] else ("late" if hybrid else "MISSED")
        print(f"{sc['id']:<5} {sc['kind']:<10} {sc['label']:<6} {fmt:<34} {sc['alert_by'] or '-':>8}  {verdict}")
    print(f"\n{'system':<8} {'scams caught in time':<22} {'false alarms (legit calls)':<27} avg lead (turns)")
    for system, row in replay_tool.score(scenarios, results).items():
        print(f"{system:<8} {row['detected']:<22} {row['false_alarms']:<27} {row['avg_lead_turns']}")


def run_detection(calls: list, turns_by_call: dict, analyst, floor, cache, args) -> tuple:
    scenarios, results = [], {}
    by_id = {c["id"]: c for c in calls}
    for cid, turns in turns_by_call.items():
        c = by_id[cid]
        sc = {"id": cid, "label": c["label"], "kind": c["kind"], "alert_by": c["alertByTurn"],
              "description": c["description"], "turns": turns}
        results[cid] = replay_tool.replay(sc, analyst, floor, cache, args.fresh, args.no_llm)
        scenarios.append(sc)
    return scenarios, results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="only list missing / misnamed recordings")
    parser.add_argument("--engine", choices=["cohere-modal", "speechmatics", "whisper-service"], default="cohere-modal")
    parser.add_argument("--call", help="run one call, e.g. K13")
    parser.add_argument("--recordings", default=str(HERE / "recordings"))
    parser.add_argument("--allow-missing", action="store_true", help="use the written line for turns not recorded yet")
    parser.add_argument("--no-llm", action="store_true", help="rules only (the AI analyst uses stored answers if any)")
    parser.add_argument("--compare-text", action="store_true", help="also score the written scripts (no ASR)")
    parser.add_argument("--fresh", action="store_true", help="ask the AI analyst again instead of stored answers")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    t1.load_env(ROOT / ".env")

    calls, groups = load_calls(args.call)
    found, unknown = index_recordings(Path(args.recordings))
    if args.check:
        return check(calls, found, unknown)

    engine = t1.make_engine(args.engine)
    print(f"ASR engine: {engine.describe()}  ·  recordings: {len(found)} files")
    transcribed = transcribe_calls(calls, found, engine, args.allow_missing)
    if not transcribed:
        print("No complete call to score yet. Run with --check to see what is missing.")
        return 1
    asr_report(calls, transcribed, groups)

    analyst = CallAnalyst.from_env()
    floor = HardSignalFloor()
    store = HERE / "results" / f"analyst_{analyst.model.replace('/', '_')}.json"
    store.parent.mkdir(exist_ok=True)
    cache = json.loads(store.read_text(encoding="utf-8")) if store.exists() else {}
    try:
        asr_turns = {cid: [t for t, _, _ in turns] for cid, turns in transcribed.items()}
        scenarios, results = run_detection(calls, asr_turns, analyst, floor, cache, args)
        detection_report(f"Detection on the recorded calls (ASR transcripts, model {analyst.model})", scenarios, results)
        if args.compare_text:
            text_turns = {cid: [Turn(ref["speaker"], ref["text"]) for _, ref, _ in turns] for cid, turns in transcribed.items()}
            scenarios, results = run_detection(calls, text_turns, analyst, floor, cache, args)
            detection_report("Same calls from the written scripts (no ASR) - the difference is what ASR costs",
                             scenarios, results)
    finally:
        store.write_text(json.dumps(cache, ensure_ascii=False, indent=1), encoding="utf-8")
    if args.no_llm:
        print("\n(--no-llm: the 'AI' and 'hybrid' columns only use AI answers already stored)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
