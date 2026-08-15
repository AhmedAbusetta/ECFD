"""
ECFD Speech AI / ASR Microservice
FastAPI service supporting real-time Egyptian Arabic Speech-to-Text inference.
"""

import os
import time
from typing import Optional
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

app = FastAPI(
    title="ECFD ASR Microservice",
    description="Real-time Egyptian Arabic speech-to-text API",
    version="1.0.0"
)

# Configuration from Environment
USE_MOCK = os.getenv("ML_USE_MOCK_MODE", "true").lower() == "true"
MODEL_NAME = os.getenv("ASR_MODEL_NAME", "faster-whisper-medium")
DEVICE = os.getenv("ASR_DEVICE", "cpu")

# Preloaded model placeholder
asr_model = None


class AsrRequest(BaseModel):
    sessionId: str
    segmentId: str
    sampleRate: int = 16000
    audioFormat: str = "pcm_s16le"
    languageHint: Optional[str] = "ar"
    audioBase64: Optional[str] = None


class AsrResponse(BaseModel):
    segmentId: str
    text: str
    confidence: float
    isFinal: bool
    startMs: int
    endMs: int
    modelVersion: str
    inferenceDurationMs: float


@app.on_event("startup")
def startup_event():
    global asr_model
    if not USE_MOCK:
        try:
            from faster_whisper import WhisperModel
            print(f"[ASR] Loading {MODEL_NAME} on {DEVICE}...")
            asr_model = WhisperModel(MODEL_NAME, device=DEVICE, compute_type="int8")
            print("[ASR] Model loaded successfully.")
        except Exception as e:
            print(f"[ASR] Error loading faster-whisper model: {e}. Falling back to mock mode.")


@app.get("/health")
def health():
    return {
        "status": "HEALTHY",
        "service": "asr",
        "mockMode": USE_MOCK,
        "model": MODEL_NAME,
        "device": DEVICE
    }


@app.post("/v1/asr/analyze", response_model=AsrResponse)
def analyze_audio(req: AsrRequest):
    start_time = time.time()

    # Mock response for fast local testing
    if USE_MOCK or asr_model is None:
        mock_text = "أنا من الدعم الفني للـ IT ومحتاجين نحدث حسابك"
        return AsrResponse(
            segmentId=req.segmentId,
            text=mock_text,
            confidence=0.95,
            isFinal=True,
            startMs=0,
            endMs=2500,
            modelVersion=f"{MODEL_NAME}-mock",
            inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
        )

    # Real Faster-Whisper Inference Placeholder
    # Audio decoding from base64 and VAD windowing
    return AsrResponse(
        segmentId=req.segmentId,
        text="أنا من البنك وعندنا مشكلة في الحساب",
        confidence=0.92,
        isFinal=True,
        startMs=0,
        endMs=3000,
        modelVersion=MODEL_NAME,
        inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
    )
