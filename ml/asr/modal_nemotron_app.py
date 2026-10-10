"""
ECFD ASR on Modal (cloud GPU) — NVIDIA Nemotron 3.5 ASR (nvidia/nemotron-3.5-asr-streaming-0.6b,
OpenMDW-1.1). Multilingual streaming RNN-T; Arabic is the generic "ar-AR" locale (no Egyptian variant).

Same contract as modal_app.py (Cohere), so it is a drop-in MlServices:AsrUrl / NEMOTRON_ASR_URL.
Deploy:
  modal deploy ml/asr/modal_nemotron_app.py
"""

import os

import modal

MODEL_ID = os.getenv("NEMOTRON_ASR_MODEL", "nvidia/nemotron-3.5-asr-streaming-0.6b")
GPU = os.getenv("ASR_GPU", "A10G")
MAX_CONTAINERS = int(os.getenv("ASR_MAX_CONTAINERS", "1"))

image = (
    modal.Image.debian_slim(python_version="3.12")
    .pip_install("transformers>=5.13.0", "torch", "accelerate", "huggingface_hub", "sentencepiece", "librosa",
                 "av>=11", "numpy", "fastapi>=0.109.0", "pydantic>=2.6.0")
    .env({"HF_HOME": "/cache/hf"})
    .add_local_python_source("modal_asr_common")
)

hf_cache = modal.Volume.from_name("ecfd-hf-cache", create_if_missing=True)

app = modal.App("ecfd-asr-nemotron")


@app.cls(
    image=image,
    gpu=GPU,
    volumes={"/cache": hf_cache},
    secrets=[modal.Secret.from_name("huggingface")],
    scaledown_window=300,
    max_containers=MAX_CONTAINERS,  # one GPU for one live call (see modal_app.py)
    timeout=600,
)
@modal.concurrent(max_inputs=8)
class NemotronAsr:
    @modal.enter()
    def load(self):
        from transformers import AutoModelForRNNT, AutoProcessor

        print(f"[ASR] Loading {MODEL_ID} on {GPU}...")
        self.processor = AutoProcessor.from_pretrained(MODEL_ID)
        self.model = AutoModelForRNNT.from_pretrained(MODEL_ID, device_map="auto")
        self.rate = self.processor.feature_extractor.sampling_rate
        hf_cache.commit()
        print(f"[ASR] Model loaded (expects {self.rate} Hz).")

    def transcribe(self, audio, language: str):
        import torch

        if self.rate != 16000:
            import numpy as np
            n = int(len(audio) * self.rate / 16000)
            audio = np.interp(np.linspace(0, len(audio), n, endpoint=False), np.arange(len(audio)), audio)
        lang = "ar-AR" if (language or "ar").lower().startswith("ar") else "auto"
        inputs = self.processor(audio, sampling_rate=self.rate, language=lang)
        inputs.to(self.model.device, dtype=self.model.dtype)
        with torch.inference_mode():
            out = self.model.generate(**inputs, return_dict_in_generate=True)
        text = self.processor.decode(out.sequences, skip_special_tokens=True)
        if isinstance(text, list):
            text = text[0]
        return text.strip(), 0.0

    @modal.asgi_app()
    def web(self):
        from modal_asr_common import build_api

        return build_api("ECFD ASR (Modal / Nemotron 3.5 ASR)", MODEL_ID, GPU, self.transcribe)
