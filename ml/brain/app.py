"""ECFD analyst service - the call analyst + deterministic floor + risk curve behind one HTTP call.

The backend posts every finished turn (either side of the call); the service keeps the call's
turns, the analyst's notes and the risk curve in memory and answers with the call's current
assessment. In-memory state is fine for local testing; a production deployment would keep it
in the backend and call a stateless endpoint.

  POST /v1/analyst/turn  {sessionId, speaker: CALLER|EMPLOYEE, text}
  POST /v1/analyst/end   {sessionId}
  GET  /health

If turns arrive faster than the LLM answers, only the newest turn is sent to the LLM (it sees
the whole transcript anyway); older pending turns still get the instant floor check.

Run (repo root):
  ml/asr/.venv/Scripts/python -m uvicorn app:app --app-dir ml/brain --port 8003
"""

import os
import sys
import threading
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from ecfd_brain.analyst import PROMPT_VERSION, CallAnalyst, Turn  # noqa: E402
from ecfd_brain.floor import HardSignalFloor  # noqa: E402
from ecfd_brain.trajectory import RiskTrajectory  # noqa: E402
from run_test0 import load_env  # noqa: E402

load_env(HERE.parents[1] / ".env")
# Live use: one quick retry at most - a stale answer is worth less than the floor's instant one.
os.environ.setdefault("ANALYST_MAX_RETRIES", "1")

app = FastAPI(title="ECFD Analyst Service", version="1.0.0")

analyst: Optional[CallAnalyst] = None
analyst_error: Optional[str] = None
try:
    analyst = CallAnalyst.from_env()
    analyst.provider.max_retries = int(os.environ["ANALYST_MAX_RETRIES"])
except Exception as e:  # keep serving the floor so /health can report the problem
    analyst_error = str(e)
floor = HardSignalFloor()


@dataclass
class CallState:
    turns: list = field(default_factory=list)
    trajectory: RiskTrajectory = field(default_factory=RiskTrajectory)
    notes: str = ""
    previous_risk: Optional[int] = None
    last: dict = field(default_factory=dict)  # latest assessment fields, re-sent with floor-only answers
    lock: threading.Lock = field(default_factory=threading.Lock)       # protects turns
    analysis: threading.Lock = field(default_factory=threading.Lock)   # one LLM call at a time per call


calls: dict = {}
calls_lock = threading.Lock()


class TurnRequest(BaseModel):
    sessionId: str
    speaker: str
    text: str


class EndRequest(BaseModel):
    sessionId: str


class EvidenceOut(BaseModel):
    turn: int
    speaker: str
    label: str
    quote: str


class TurnResponse(BaseModel):
    sessionId: str
    turn: int
    analyzed: bool                 # false = LLM skipped/failed for this turn; floor only
    risk: int
    level: str
    levelChanged: bool
    llmRisk: Optional[int] = None
    floor: int
    stage: str = "Normal"
    trend: str = "STEADY"
    employeeState: str = "NORMAL"
    callerGoal: str = ""
    strategy: str = ""
    nextLikelyMove: str = ""
    alertAr: str = ""
    policyViolations: List[str] = []
    evidence: List[EvidenceOut] = []
    hardSignals: List[str] = []
    reasons: List[str] = []
    model: str = ""
    latencyMs: int = 0
    error: Optional[str] = None


@app.get("/health")
def health():
    return {"status": "HEALTHY" if analyst else "DEGRADED", "service": "analyst",
            "model": analyst.model if analyst else None, "prompt": PROMPT_VERSION,
            "error": analyst_error, "activeCalls": len(calls)}


@app.post("/v1/analyst/turn", response_model=TurnResponse)
def analyze_turn(req: TurnRequest):
    speaker = req.speaker.upper()
    if speaker not in ("CALLER", "EMPLOYEE"):
        raise HTTPException(status_code=400, detail="speaker must be CALLER or EMPLOYEE")
    if not req.text.strip():
        raise HTTPException(status_code=400, detail="text is required")
    with calls_lock:
        state = calls.setdefault(req.sessionId, CallState())
    with state.lock:
        state.turns.append(Turn(speaker, req.text))
        turn_no = len(state.turns)
    signals = floor.check(req.text, speaker)

    with state.analysis:
        with state.lock:
            snapshot = list(state.turns)
        assessment, error = None, None
        if analyst is None:
            error = analyst_error
        elif len(snapshot) > turn_no:
            error = "newer turn pending - the LLM will judge the call at that turn"
        else:
            try:
                assessment = analyst.assess(snapshot, state.notes, state.previous_risk)
                state.notes, state.previous_risk = assessment.notes, assessment.risk
            except Exception as e:
                error = f"LLM failed: {e}"[:300]
        out = state.trajectory.update(turn_no, assessment, signals)

        if assessment is not None:
            state.last = {
                "stage": assessment.stage, "trend": assessment.trend, "employeeState": assessment.employee_state,
                "callerGoal": assessment.caller_goal, "strategy": assessment.strategy,
                "nextLikelyMove": assessment.next_likely_move, "alertAr": assessment.alert_ar,
                "policyViolations": assessment.policy_violations,
                "evidence": [EvidenceOut(**vars(ev)) for ev in assessment.evidence],
            }
        return TurnResponse(
            sessionId=req.sessionId, turn=turn_no, analyzed=assessment is not None,
            risk=out["risk"], level=out["level"], levelChanged=out["level_changed"],
            llmRisk=out["llm_risk"], floor=out["floor"],
            hardSignals=[f"{s.kind}/{s.concept}: {' + '.join(s.words)}" for s in signals],
            reasons=out["reasons"], model=analyst.model if analyst else "",
            latencyMs=assessment.latency_ms if assessment else 0, error=error,
            **state.last,
        )


@app.post("/v1/analyst/end")
def end_call(req: EndRequest):
    with calls_lock:
        state = calls.pop(req.sessionId, None)
    return {"sessionId": req.sessionId, "turns": len(state.turns) if state else 0,
            "riskHistory": state.trajectory.history if state else []}
