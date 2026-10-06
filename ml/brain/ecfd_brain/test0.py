"""Test 0 helpers shared by the command line (run_test0.py) and the dashboard lab service.

Answers are stored per model in ml/brain/test0/results_<model>.json and are only valid for
the exact prompt + schema that produced them (prompt_version).
"""

import hashlib
import json
from pathlib import Path

from .brain import SYSTEM_PROMPT, TOOL_PARAMETERS, validate

TEST0_DIR = Path(__file__).resolve().parents[1] / "test0"
PASS_CORRECT = 15


def prompt_version() -> str:
    return hashlib.sha256((SYSTEM_PROMPT + json.dumps(TOOL_PARAMETERS)).encode()).hexdigest()[:10]


def load_items() -> dict:
    return json.loads((TEST0_DIR / "dev_sentences.json").read_text(encoding="utf-8"))


def store_path(model: str) -> Path:
    return TEST0_DIR / f"results_{model.replace('/', '_')}.json"


def load_store(model: str, fresh: bool = False) -> dict:
    path = store_path(model)
    store = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
    if fresh or store.get("prompt_version") != prompt_version():
        store = {"model": model, "prompt_version": prompt_version(), "answers": {}}
    return store


def save_store(model: str, store: dict) -> None:
    store_path(model).write_text(json.dumps(store, ensure_ascii=False, indent=2), encoding="utf-8")


def score_item(item: dict, answer: dict) -> dict:
    """Re-validate a stored raw answer with the current safeguards and compare to the expected labels."""
    accepted, dropped = validate(answer["raw"], item["text"])
    got = {t.label for t in accepted}
    expected = set(item["expected"])
    return {
        "id": item["id"],
        "ok": got == expected,
        "accepted": [{"label": t.label, "confidence": t.confidence, "quote": t.quote} for t in accepted],
        "dropped": [{"label": t.label, "confidence": t.confidence, "quote": t.quote, "reason": why} for t, why in dropped],
        "missing": sorted(expected - got),
        "extra": sorted(got - expected),
        "needs_more_context": bool(answer["raw"].get("needs_more_context", False)),
        "latency_ms": answer.get("latency_ms"),
    }


def summarize(scored: list, total_items: int) -> dict:
    latencies = sorted(s["latency_ms"] for s in scored if s.get("latency_ms") is not None)
    correct = sum(s["ok"] for s in scored)
    return {
        "answered": len(scored),
        "total": total_items,
        "correct": correct,
        "invalid_quotes_kept": 0,  # validate() never accepts a quote missing from the sentence
        "median_ms": latencies[len(latencies) // 2] if latencies else None,
        "max_ms": latencies[-1] if latencies else None,
        "passed": len(scored) == total_items and correct >= PASS_CORRECT,
        "pass_line": PASS_CORRECT,
    }
