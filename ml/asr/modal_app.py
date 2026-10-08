"""
ECFD ASR on Modal (cloud GPU) — Cohere Transcribe Arabic.

Serves the same contract as app.py (POST /v1/asr/analyze, GET /health), so the backend
switches to it by setting MlServices:AsrUrl to the URL printed by `modal deploy`.

One-time setup:
  1. Accept the model terms: https://huggingface.co/CohereLabs/cohere-transcribe-arabic-07-2026
  2. Create a Hugging Face *read* token and store it in Modal:
       modal secret create huggingface HF_TOKEN=<your token>
Deploy:
  modal deploy ml/asr/modal_app.py
Demo day (keep one GPU warm to avoid a cold start on the first call; costs credits while up):
  ASR_MIN_CONTAINERS=1 modal deploy ml/asr/modal_app.py
  ...and redeploy without it afterwards.
"""

import os

import modal

MODEL_ID = "CohereLabs/cohere-transcribe-arabic-07-2026"
GPU = os.getenv("ASR_GPU", "A10G")
MIN_CONTAINERS = int(os.getenv("ASR_MIN_CONTAINERS", "0"))
MAX_CONTAINERS = int(os.getenv("ASR_MAX_CONTAINERS", "1"))

image = (
    modal.Image.debian_slim(python_version="3.11")
    .pip_install(
        "transformers>=5.4.0",
        "torch",
        "huggingface_hub",
        "accelerate",
        "sentencepiece",
        "protobuf",
        "soundfile",
        "librosa",
        "av>=11",
        "numpy",
        "fastapi>=0.109.0",
        "pydantic>=2.6.0",
    )
    .env({"HF_HOME": "/cache/hf"})
)

# Weights are downloaded once into this volume instead of on every cold start.
hf_cache = modal.Volume.from_name("ecfd-hf-cache", create_if_missing=True)

app = modal.App("ecfd-asr")


def _decode_to_16k_mono(raw: bytes):
    """Decode any container PyAV understands (wav/webm/ogg/...) to float32 mono 16 kHz."""
    import io

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


@app.cls(
    image=image,
    gpu=GPU,
    volumes={"/cache": hf_cache},
    secrets=[modal.Secret.from_name("huggingface")],
    scaledown_window=300,  # stay warm 5 min after the last request
    min_containers=MIN_CONTAINERS,
    # One GPU handles a whole call: live partials from both legs arrive together, and without
    # a cap Modal starts a new GPU for each one (it hit the 10-GPU account limit).
    max_containers=MAX_CONTAINERS,
    timeout=600,
)
@modal.concurrent(max_inputs=8)
class CohereAsr:
    @modal.enter()
    def load(self):
        import torch
        from transformers import AutoProcessor, CohereAsrForConditionalGeneration

        print(f"[ASR] Loading {MODEL_ID} on {GPU}...")
        self.processor = AutoProcessor.from_pretrained(MODEL_ID)
        self.model = CohereAsrForConditionalGeneration.from_pretrained(
            MODEL_ID, device_map="auto", dtype=torch.bfloat16
        )
        hf_cache.commit()
        print("[ASR] Model loaded.")

    def transcribe(self, audio, language: str):
        import numpy as np
        import torch

        inputs = self.processor(audio, sampling_rate=16000, return_tensors="pt", language=language)
        inputs.to(self.model.device, dtype=self.model.dtype)
        with torch.inference_mode():
            out = self.model.generate(
                **inputs, max_new_tokens=256, return_dict_in_generate=True, output_scores=True
            )
        text = self.processor.decode(out.sequences, skip_special_tokens=True)
        if isinstance(text, list):
            text = text[0]

        # Mean token probability as a rough 0..1 confidence (display only in the backend).
        try:
            scores = self.model.compute_transition_scores(out.sequences, out.scores, normalize_logits=True)
            confidence = float(np.exp(scores[0].float().cpu().numpy()).mean())
        except Exception:
            confidence = 0.0
        return text.strip(), confidence

    @modal.asgi_app()
    def web(self):
        import base64
        import time
        from typing import Optional

        import numpy as np
        from fastapi import FastAPI, HTTPException
        from pydantic import BaseModel

        api = FastAPI(title="ECFD ASR (Modal / Cohere Transcribe Arabic)", version="2.0.0")
        model_version = MODEL_ID.split("/")[-1]

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
                    "model": MODEL_ID, "device": GPU, "modelLoaded": True}

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
                    audio = _decode_to_16k_mono(raw)
                except Exception as e:
                    raise HTTPException(status_code=400, detail=f"Could not decode {req.audioFormat} audio: {e}")

            text, confidence = self.transcribe(audio, req.languageHint or "ar")
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
