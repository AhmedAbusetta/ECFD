"""Shared pieces of the Modal ASR apps: the ASR service contract (POST /v1/asr/analyze, GET /health,
the same as ml/asr/app.py) and audio decoding, so every engine is a drop-in MlServices:AsrUrl."""

import base64
import io
import time
from typing import Callable, Optional


def decode_to_16k_mono(raw: bytes):
    """Decode any container PyAV understands (wav/webm/ogg/...) to float32 mono 16 kHz."""
    import av
    import numpy as np

    resampler = av.AudioResampler(format="s16", layout="mono", rate=16000)
    chunks = []
    with av.open(io.BytesIO(raw)) as container:
        for frame in container.decode(audio=0):
            for out in resampler.resample(frame):
                chunks.append(out.to_ndarray().reshape(-1))
        for out in resampler.resample(None):  # flush
            chunks.append(out.to_ndarray().reshape(-1))
    if not chunks:
        return np.zeros(0, dtype=np.float32)
    return np.concatenate(chunks).astype(np.float32) / 32768.0


def build_api(title: str, model_id: str, gpu: str, transcribe: Callable):
    """FastAPI app for one engine. `transcribe(audio_float32_16k, language_hint) -> (text, confidence)`."""
    import numpy as np
    from fastapi import FastAPI, HTTPException
    from pydantic import BaseModel

    api = FastAPI(title=title, version="2.0.0")
    model_version = model_id.split("/")[-1]

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

    @api.get("/health")
    def health():
        return {"status": "HEALTHY", "service": "asr", "mockMode": False,
                "model": model_id, "device": gpu, "modelLoaded": True}

    @api.post("/v1/asr/analyze", response_model=AsrResponse)
    def analyze(req: AsrRequest):
        start = time.time()
        try:
            raw = base64.b64decode(req.audioBase64 or "", validate=True)
        except Exception:
            raise HTTPException(status_code=400, detail="audioBase64 is not valid base64")
        if not raw:
            raise HTTPException(status_code=400, detail="audioBase64 is required")

        if req.audioFormat == "pcm_s16le":
            if req.sampleRate != 16000:
                raise HTTPException(status_code=400, detail="pcm_s16le audio must be 16000 Hz mono")
            audio = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
        else:
            try:
                audio = decode_to_16k_mono(raw)
            except Exception as e:
                raise HTTPException(status_code=400, detail=f"Could not decode {req.audioFormat} audio: {e}")

        text, confidence = transcribe(audio, req.languageHint or "ar")
        return AsrResponse(
            segmentId=req.segmentId,
            text=text,
            confidence=round(min(max(confidence, 0.0), 1.0), 3),
            isFinal=True,
            startMs=0,
            endMs=int(len(audio) / 16),
            modelVersion=model_version,
            inferenceDurationMs=round((time.time() - start) * 1000, 2),
        )

    return api
