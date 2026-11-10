"""
ECFD instant request check (the backend's fast, deterministic rules).

Answers one narrow question per CALLER sentence within milliseconds: did the caller just ask for
something no legitimate caller asks for by phone? It uses the same hard-signal rules as the AI call
analyst's floor (ml/brain/ecfd_brain/floor.py): a request verb + a secret ("اديني الكود"), a payment verb
+ money, a remote-control app, or a verification-bypass phrase. Single words ("كود", "لازم", "البنك")
never count on their own - judging the conversation in context is the analyst's job (ADR-0005).
"""

import sys
import time
from pathlib import Path
from typing import List, Optional

from fastapi import FastAPI
from pydantic import BaseModel

# the rules live with the analyst so the live system and the offline tests can never drift apart
for _brain in (Path(__file__).resolve().parents[1] / "brain", Path(__file__).resolve().parent / "brain"):
    if (_brain / "ecfd_brain").is_dir():
        sys.path.insert(0, str(_brain))
        break

from ecfd_brain.floor import HardSignalFloor  # noqa: E402

app = FastAPI(
    title="ECFD instant request check",
    description="Deterministic hard-signal rules on one caller sentence",
    version="2.0.0"
)

MODEL_VERSION = "hard-signal-floor-v1"
CONFIDENCE = 0.95
OTP_CONCEPTS = {"OTP"}
floor = HardSignalFloor()


class TacticMatch(BaseModel):
    type: str
    confidence: float


class NlpRequest(BaseModel):
    sessionId: str
    segmentId: str
    text: str
    language: Optional[str] = "ar"


class NlpResponse(BaseModel):
    segmentId: str
    tactics: List[TacticMatch]
    modelVersion: str
    inferenceDurationMs: float


def _label(kind: str, concept: str) -> str:
    if kind == "SECRET":
        return "OTP_REQUEST" if concept in OTP_CONCEPTS else "CREDENTIAL_REQUEST"
    return {"PAYMENT": "PAYMENT_REQUEST", "REMOTE": "REMOTE_ACCESS", "BYPASS": "VERIFICATION_BYPASS"}[kind]


def rule_match(text: str) -> List[TacticMatch]:
    """Tactics for one CALLER sentence (the backend only sends caller sentences)."""
    labels = []
    for signal in floor.check(text, "CALLER"):
        label = _label(signal.kind, signal.concept)
        if label not in labels:
            labels.append(label)
    return [TacticMatch(type=label, confidence=CONFIDENCE) for label in labels]


@app.get("/health")
def health():
    return {"status": "HEALTHY", "service": "nlp", "mockMode": False, "model": MODEL_VERSION}


@app.post("/v1/nlp/analyze", response_model=NlpResponse)
def analyze_text(req: NlpRequest):
    start_time = time.time()
    tactics = rule_match(req.text)
    return NlpResponse(
        segmentId=req.segmentId,
        tactics=tactics,
        modelVersion=MODEL_VERSION,
        inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
    )
