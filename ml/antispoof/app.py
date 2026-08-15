"""
ECFD Voice Security / Anti-Spoofing Microservice
FastAPI service detecting synthetic/deepfake voices and replay artifacts on audio windows.
"""

import os
import time
from typing import Optional
from fastapi import FastAPI
from pydantic import BaseModel

app = FastAPI(
    title="ECFD Voice Anti-Spoofing Microservice",
    description="Acoustic deepfake and voice conversion detection API",
    version="1.0.0"
)

USE_MOCK = os.getenv("ML_USE_MOCK_MODE", "true").lower() == "true"
MODEL_NAME = os.getenv("ANTISPOOF_MODEL_NAME", "AASIST")


class VoiceAnalysisRequest(BaseModel):
    sessionId: str
    windowId: str
    sampleRate: int = 16000
    audioFormat: str = "pcm_s16le"
    audioBase64: Optional[str] = None


class VoiceAnalysisResponse(BaseModel):
    windowId: str
    spoofProbability: float
    qualityScore: float
    modelVersion: str
    inferenceDurationMs: float


@app.get("/health")
def health():
    return {
        "status": "HEALTHY",
        "service": "antispoof",
        "mockMode": USE_MOCK,
        "model": MODEL_NAME
    }


@app.post("/v1/voice/analyze", response_model=VoiceAnalysisResponse)
def analyze_voice(req: VoiceAnalysisRequest):
    start_time = time.time()

    # Mock response (low spoof probability for genuine voice baseline)
    return VoiceAnalysisResponse(
        windowId=req.windowId,
        spoofProbability=0.12,
        qualityScore=0.91,
        modelVersion=f"{MODEL_NAME}-mock",
        inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
    )
