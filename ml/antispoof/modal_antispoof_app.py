"""
ECFD voice anti-spoofing on Modal (cloud GPU) — NII AntiDeepfake XLS-R-2B.

Same contract as ml/antispoof/app.py (POST /v1/voice/analyze, GET /health), so the backend points
MlServices:AntiSpoofUrl at the URL printed by `modal deploy`. Chosen from the phone-quality evaluation
(ml/antispoof/modal_eval.py): 0.0% EER on 105 real Test 2 turns vs 525 fakes (TTS and voice cloning),
with the widest real/fake margin on cloned voices.

Weights: nii-yamagishilab/xls-r-2b-anti-deepfake, CC BY-NC-SA 4.0 - research / education only.
Deploy:
  modal deploy ml/antispoof/modal_antispoof_app.py
"""

import os

import modal

MODEL_REPO = os.getenv("ANTISPOOF_MODEL", "nii-yamagishilab/xls-r-2b-anti-deepfake")
MODEL_KEY = os.getenv("ANTISPOOF_MODEL_KEY", "xlsr_2b")  # architecture config in the NII repo
GPU = os.getenv("ANTISPOOF_GPU", "A10G")
MAX_CONTAINERS = int(os.getenv("ANTISPOOF_MAX_CONTAINERS", "1"))

# NII's code builds the model with the fairseq version their checkpoints were trained with.
image = (
    modal.Image.debian_slim(python_version="3.10")
    .apt_install("git")
    .pip_install("pip<24.1")
    .pip_install("torch==1.13.1", "numpy==1.23.5", "soundfile", "scipy", "huggingface_hub", "safetensors",
                 "fastapi>=0.109.0", "pydantic>=2.6.0")
    .run_commands(
        "pip install git+https://github.com/facebookresearch/fairseq.git@a54021305d6b3c4c5959ac9395135f63202db8f1",
        "git clone --depth 1 https://github.com/nii-yamagishilab/AntiDeepfake /nii",
    )
    .env({"HF_HOME": "/cache/hf"})
)

hf_cache = modal.Volume.from_name("ecfd-hf-cache", create_if_missing=True)

app = modal.App("ecfd-antispoof")


@app.cls(
    image=image,
    gpu=GPU,
    volumes={"/cache": hf_cache},
    scaledown_window=300,
    max_containers=MAX_CONTAINERS,  # one GPU for one live call
    timeout=600,
)
@modal.concurrent(max_inputs=4)
class AntiSpoof:
    @modal.enter()
    def load(self):
        import sys

        import torch
        from fairseq.models.wav2vec import Wav2Vec2Config, Wav2Vec2Model
        from huggingface_hub import hf_hub_download
        from safetensors.torch import load_file

        sys.path.insert(0, "/nii")
        from models.W2V_configs import global_configs, global_input_dims

        cfg = {k: v for k, v in global_configs[MODEL_KEY].items() if k != "_name"}

        class SSL(torch.nn.Module):
            def __init__(self):
                super().__init__()
                self.model = Wav2Vec2Model(Wav2Vec2Config(**cfg))

        class Detector(torch.nn.Module):
            def __init__(self):
                super().__init__()
                self.m_ssl = SSL()
                self.adap_pool1d = torch.nn.AdaptiveAvgPool1d(1)
                self.proj_fc = torch.nn.Linear(global_input_dims[MODEL_KEY], 2)

            def forward(self, wav):
                emb = self.m_ssl.model(wav, mask=False, features_only=True)["x"]  # [B, T, D]
                return self.proj_fc(self.adap_pool1d(emb.transpose(1, 2)).squeeze(-1))

        print(f"[antispoof] Loading {MODEL_REPO} on {GPU}...")
        model = Detector()
        model.load_state_dict(load_file(hf_hub_download(MODEL_REPO, "model.safetensors")))
        self.model = model.cuda().eval()
        hf_cache.commit()
        print("[antispoof] Model loaded.")

    def spoof_probability(self, audio) -> float:
        """float32 mono 16 kHz -> probability that the voice is synthetic (softmax of the fake class)."""
        import torch
        import torch.nn.functional as F

        x = torch.from_numpy(audio)
        x = F.layer_norm(x, x.shape).unsqueeze(0).cuda()  # as on the model card
        with torch.no_grad():
            probs = torch.softmax(self.model(x)[0], dim=-1)
        return float(probs[0])  # index 0 = fake, 1 = real

    @modal.asgi_app()
    def web(self):
        import base64
        import io
        import time
        from typing import Optional

        import numpy as np
        from fastapi import FastAPI, HTTPException
        from pydantic import BaseModel

        api = FastAPI(title="ECFD Voice Anti-Spoofing (Modal / NII AntiDeepfake)", version="2.0.0")
        model_version = MODEL_REPO.split("/")[-1]

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

        @api.get("/health")
        def health():
            return {"status": "HEALTHY", "service": "antispoof", "mockMode": False,
                    "model": MODEL_REPO, "device": GPU, "modelLoaded": True}

        @api.post("/v1/voice/analyze", response_model=VoiceAnalysisResponse)
        def analyze(req: VoiceAnalysisRequest):
            start = time.time()
            try:
                raw = base64.b64decode(req.audioBase64 or "", validate=True)
            except Exception:
                raise HTTPException(status_code=400, detail="audioBase64 is not valid base64")
            if not raw:
                raise HTTPException(status_code=400, detail="audioBase64 is required")
            if req.audioFormat == "pcm_s16le":
                audio = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
                rate = req.sampleRate
            else:
                import soundfile as sf

                audio, rate = sf.read(io.BytesIO(raw), dtype="float32")
                if audio.ndim > 1:
                    audio = audio.mean(axis=1)
            if rate != 16000:
                from scipy.signal import resample_poly

                audio = resample_poly(audio, 16000, rate).astype(np.float32)
            if len(audio) < 1600:
                raise HTTPException(status_code=400, detail="audio shorter than 0.1 s")

            spoof = self.spoof_probability(audio)
            # quality = how much the score can be trusted: short clips carry less evidence (1.0 from 3 s)
            quality = min(1.0, len(audio) / 16000 / 3.0)
            return VoiceAnalysisResponse(
                windowId=req.windowId,
                spoofProbability=round(spoof, 4),
                qualityScore=round(quality, 3),
                modelVersion=model_version,
                inferenceDurationMs=round((time.time() - start) * 1000, 2),
            )

        return api
