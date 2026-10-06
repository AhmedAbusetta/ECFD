"""Test 2 (offline replay) - the call analyst on whole scenario calls, turn by turn.

For every scenario in data/scenarios/ (and data/samples/) the call is replayed one turn at a
time. After each turn the deterministic floor checks the caller's words and the call analyst
(LLM) judges the whole call so far. Three systems are scored from the same LLM answers:

  floor   deterministic hard signals only (no AI)
  llm     the call analyst only
  hybrid  final risk = max(floor, smoothed LLM risk)  <- what ECFD ships

Metrics: detection (ALERT at or before alertByTurn on FRAUD calls), false alarms (any ALERT on
LEGIT calls) and lead time (turns between the first ALERT and the last turn still in time).

LLM answers are stored in ml/brain/replay/results_<model>.json and re-used, so re-running costs
no quota; --fresh asks the LLM again.

Usage (repo root):
  ml/asr/.venv/Scripts/python ml/brain/run_call_replay.py
  ml/asr/.venv/Scripts/python ml/brain/run_call_replay.py --scenario SCN-CVV-001 --verbose
  ml/asr/.venv/Scripts/python ml/brain/run_call_replay.py --fresh
"""

import argparse
import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))

from ecfd_brain.analyst import PROMPT_VERSION, CallAnalyst, Turn, build_user_message, validate  # noqa: E402
from ecfd_brain.floor import HardSignalFloor  # noqa: E402
from ecfd_brain.trajectory import RiskTrajectory  # noqa: E402
from run_test0 import load_env  # noqa: E402

SCENARIO_DIRS = [ROOT / "data" / "scenarios", ROOT / "data" / "samples"]
SYSTEMS = ["floor", "llm", "hybrid"]


def load_scenarios(only: str = None) -> list:
    scenarios = []
    for d in SCENARIO_DIRS:
        for path in sorted(d.glob("*.json")):
            doc = json.loads(path.read_text(encoding="utf-8"))
            if "turns" in doc:
                turns = [Turn(t["speaker"].upper(), t["text"]) for t in doc["turns"]]
                label, alert_by = doc["label"], doc.get("alertByTurn")
            elif "utterances" in doc:  # data/samples format (speakerId + speakers[].role)
                roles = {s["id"]: s["role"].upper() for s in doc["speakers"]}
                turns = [Turn(roles[u["speakerId"]], u["text"]) for u in doc["utterances"]]
                label, alert_by = "FRAUD", len(turns)
            else:
                continue
            if only and doc["scenarioId"] != only:
                continue
            scenarios.append({"id": doc["scenarioId"], "label": label, "alert_by": alert_by,
                              "description": doc.get("description", doc.get("attackType", "")), "turns": turns})
    return scenarios


def first_alert(levels: list):
    return next((i + 1 for i, lvl in enumerate(levels) if lvl == "ALERT"), None)


def replay(scenario: dict, analyst: CallAnalyst, floor: HardSignalFloor, cache: dict, fresh: bool, stored_only: bool):
    turns = scenario["turns"]
    tracks = {s: RiskTrajectory() for s in SYSTEMS}
    rows, notes, previous_risk = [], "", None
    for i in range(1, len(turns) + 1):
        current = turns[:i]
        signals = floor.check(current[-1].text, current[-1].speaker)
        message = build_user_message(current, analyst.policy, notes, previous_risk)
        key = hashlib.sha1(f"{PROMPT_VERSION}|{analyst.model}|{message}".encode("utf-8")).hexdigest()
        assessment = None
        if key in cache and not fresh:
            assessment = validate(cache[key]["raw"], current)
            assessment.latency_ms = cache[key]["latency_ms"]
        elif not stored_only:
            try:
                assessment = analyst.assess(current, notes, previous_risk)
                cache[key] = {"raw": assessment.raw, "latency_ms": assessment.latency_ms}
            except Exception as e:  # keep replaying: the floor still works without the LLM
                print(f"    ! turn {i}: LLM failed ({e})")
        if assessment is not None:
            notes, previous_risk = assessment.notes, assessment.risk

        out = {
            "floor": tracks["floor"].update(i, None, signals),
            "llm": tracks["llm"].update(i, assessment, ()),
            "hybrid": tracks["hybrid"].update(i, assessment, signals),
        }
        rows.append((current[-1], signals, assessment, out))
    return rows


def print_call(scenario: dict, rows: list, verbose: bool):
    print(f"\n=== {scenario['id']}  [{scenario['label']}]  {scenario['description']}")
    print(f"{'#':>2} {'who':<8} {'floor':>5} {'llm':>4} {'final':>5} {'level':<8} {'stage':<20} {'employee':<17} text")
    for i, (turn, signals, a, out) in enumerate(rows, 1):
        h = out["hybrid"]
        llm = "-" if a is None else str(a.risk)
        stage = a.stage if a else ""
        emp = a.employee_state if a else ""
        flag = " <- hard signal" if signals else ""
        print(f"{i:>2} {turn.speaker:<8} {h['floor']:>5} {llm:>4} {h['risk']:>5} {h['level']:<8} {stage:<20} {emp:<17} {turn.text[:60]}{flag}")
        if verbose and a is not None:
            print(f"      goal: {a.caller_goal} | strategy: {a.strategy}")
            for ev in a.evidence:
                print(f"      evidence [{ev.turn}] {ev.speaker} {ev.label}: {ev.quote}")
            for item, reason in a.dropped:
                print(f"      dropped ({reason}): {item}")
            if a.alert_ar:
                print(f"      alert_ar: {a.alert_ar}")
    last = rows[-1][2]
    if last and last.alert_ar:
        print(f"   employee alert: {last.alert_ar}")


def score(scenarios: list, results: dict) -> dict:
    table = {}
    for system in SYSTEMS:
        detected = missed = false_alarms = legit = 0
        leads = []
        for sc in scenarios:
            levels = [out[system]["level"] for (_, _, _, out) in results[sc["id"]]]
            fa = first_alert(levels)
            if sc["label"] == "LEGIT":
                legit += 1
                false_alarms += fa is not None
            elif fa is not None and fa <= sc["alert_by"]:
                detected += 1
                leads.append(sc["alert_by"] - fa)
            else:
                missed += 1
        frauds = detected + missed
        table[system] = {"detected": f"{detected}/{frauds}", "false_alarms": f"{false_alarms}/{legit}",
                         "avg_lead_turns": round(sum(leads) / len(leads), 1) if leads else None}
    return table


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--scenario")
    parser.add_argument("--fresh", action="store_true", help="ask the LLM again instead of re-using stored answers")
    parser.add_argument("--stored-only", action="store_true", help="never call the LLM")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    load_env(ROOT / ".env")

    analyst = CallAnalyst.from_env()
    floor = HardSignalFloor()
    store_path = HERE / "replay" / f"results_{analyst.model.replace('/', '_')}.json"
    store_path.parent.mkdir(exist_ok=True)
    cache = json.loads(store_path.read_text(encoding="utf-8")) if store_path.exists() else {}

    scenarios = load_scenarios(args.scenario)
    if not scenarios:
        print("No scenarios found.")
        return 1
    results = {}
    try:
        for sc in scenarios:
            results[sc["id"]] = replay(sc, analyst, floor, cache, args.fresh, args.stored_only)
            print_call(sc, results[sc["id"]], args.verbose)
    finally:
        store_path.write_text(json.dumps(cache, ensure_ascii=False, indent=1), encoding="utf-8")

    latencies = [a.latency_ms for rows in results.values() for (_, _, a, _) in rows if a is not None]
    print(f"\nmodel={analyst.model}  prompt={PROMPT_VERSION}  calls={len(scenarios)}  "
          f"median LLM latency={sorted(latencies)[len(latencies) // 2] if latencies else '-'} ms")
    print(f"{'system':<8} {'detected (in time)':<20} {'false alarms':<14} avg lead (turns)")
    for system, row in score(scenarios, results).items():
        print(f"{system:<8} {row['detected']:<20} {row['false_alarms']:<14} {row['avg_lead_turns']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
