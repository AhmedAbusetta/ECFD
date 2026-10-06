"""
ECFD Speech AI / ASR Microservice
FastAPI service supporting real-time Egyptian Arabic Speech-to-Text inference.

Accepts either raw 16 kHz mono PCM (audioFormat="pcm_s16le", the Media Gateway format)
or any container ffmpeg/PyAV can decode (audioFormat="wav", "webm", "ogg", ...; the
browser microphone and recorded test clips).
"""

import base64
import io
import os
import time
from typing import Optional

import numpy as np
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

app = FastAPI(
    title="ECFD ASR Microservice",
    description="Real-time Egyptian Arabic speech-to-text API",
    version="1.1.0"
)

# Configuration from Environment
USE_MOCK = os.getenv("ML_USE_MOCK_MODE", "true").lower() == "true"
MODEL_NAME = os.getenv("ASR_MODEL_NAME", "small")
DEVICE = os.getenv("ASR_DEVICE", "cpu")
COMPUTE_TYPE = os.getenv("ASR_COMPUTE_TYPE", "int8")
# 1 = greedy decoding (fastest, real-time default); 5 = beam search (slower, slightly more accurate)
BEAM_SIZE = int(os.getenv("ASR_BEAM_SIZE", "1"))
# Biases decoding toward Egyptian dialect and the code-switched terms ECFD cares about.
INITIAL_PROMPT = os.getenv(
    "ASR_INITIAL_PROMPT",
    "مكالمة باللهجة المصرية. الدعم الفني IT، كود OTP، رسالة SMS، الحساب، البنك، انستاباي، فودافون كاش."
)

asr_model = None
model_error: Optional[str] = None


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
    global asr_model, model_error
    if USE_MOCK:
        return
    try:
        from faster_whisper import WhisperModel
        print(f"[ASR] Loading faster-whisper '{MODEL_NAME}' on {DEVICE} ({COMPUTE_TYPE})...")
        asr_model = WhisperModel(MODEL_NAME, device=DEVICE, compute_type=COMPUTE_TYPE)
        print("[ASR] Model loaded.")
    except Exception as e:  # keep the service up so /health can report the problem
        model_error = str(e)
        print(f"[ASR] Failed to load model: {e}")


@app.get("/health")
def health():
    return {
        "status": "HEALTHY" if (USE_MOCK or asr_model is not None) else "DEGRADED",
        "service": "asr",
        "mockMode": USE_MOCK,
        "model": MODEL_NAME,
        "device": DEVICE,
        "modelLoaded": asr_model is not None,
        "modelError": model_error,
    }


def _decode_audio(req: AsrRequest) -> np.ndarray:
    """Return float32 mono audio at 16 kHz."""
    try:
        raw = base64.b64decode(req.audioBase64 or "", validate=True)
    except Exception:
        raise HTTPException(status_code=400, detail="audioBase64 is not valid base64")
    if not raw:
        raise HTTPException(status_code=400, detail="audioBase64 is required")

    if req.audioFormat == "pcm_s16le":
        if req.sampleRate != 16000:
            raise HTTPException(status_code=400, detail="pcm_s16le audio must be 16000 Hz mono")
        return np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0

    from faster_whisper.audio import decode_audio
    try:
        return decode_audio(io.BytesIO(raw), sampling_rate=16000)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Could not decode {req.audioFormat} audio: {e}")


@app.post("/v1/asr/analyze", response_model=AsrResponse)
def analyze_audio(req: AsrRequest):
    start_time = time.time()

    if USE_MOCK:
        return AsrResponse(
            segmentId=req.segmentId,
            text="أنا من الدعم الفني للـ IT ومحتاجين نحدث حسابك",
            confidence=0.95,
            isFinal=True,
            startMs=0,
            endMs=2500,
            modelVersion=f"{MODEL_NAME}-mock",
            inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
        )

    if asr_model is None:
        raise HTTPException(status_code=503, detail=f"ASR model not loaded: {model_error}")

    audio = _decode_audio(req)
    segments, _info = asr_model.transcribe(
        audio,
        language=req.languageHint or "ar",
        beam_size=BEAM_SIZE,
        vad_filter=True,
        initial_prompt=INITIAL_PROMPT,
        condition_on_previous_text=False,
    )
    segments = list(segments)

    text = " ".join(s.text.strip() for s in segments).strip()
    # avg_logprob is a log-likelihood per token; exp() gives a rough 0..1 confidence.
    confidence = float(np.exp(np.mean([s.avg_logprob for s in segments]))) if segments else 0.0
    duration_ms = int(len(audio) / 16)

    return AsrResponse(
        segmentId=req.segmentId,
        text=text,
        confidence=round(min(max(confidence, 0.0), 1.0), 3),
        isFinal=True,
        startMs=int(segments[0].start * 1000) if segments else 0,
        endMs=int(segments[-1].end * 1000) if segments else duration_ms,
        modelVersion=f"faster-whisper-{MODEL_NAME}",
        inferenceDurationMs=round((time.time() - start_time) * 1000, 2)
    )
