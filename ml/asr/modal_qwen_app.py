"""
ECFD ASR on Modal (cloud GPU) — QwenCleo-ASR: Qwen3-ASR-1.7B fine-tuned for Egyptian Arabic
and Arabic/English code-switching (mohammedaly22/QwenCleo-ASR, Apache-2.0).

Same contract as modal_app.py (Cohere), so it is a drop-in MlServices:AsrUrl / QWEN_ASR_URL.
Deploy:
  modal deploy ml/asr/modal_qwen_app.py
"""

import os

import modal

MODEL_ID = os.getenv("QWEN_ASR_MODEL", "mohammedaly22/QwenCleo-ASR")
GPU = os.getenv("ASR_GPU", "A10G")
MAX_CONTAINERS = int(os.getenv("ASR_MAX_CONTAINERS", "1"))

image = (
    modal.Image.debian_slim(python_version="3.12")
    .pip_install("qwen-asr>=0.0.6", "torch", "accelerate", "av>=11", "numpy",
                 "fastapi>=0.109.0", "pydantic>=2.6.0")
    .env({"HF_HOME": "/cache/hf"})
    .add_local_python_source("modal_asr_common")
)

hf_cache = modal.Volume.from_name("ecfd-hf-cache", create_if_missing=True)

app = modal.App("ecfd-asr-qwen")


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
class QwenAsr:
    @modal.enter()
    def load(self):
        import torch
        from qwen_asr import Qwen3ASRModel

        print(f"[ASR] Loading {MODEL_ID} on {GPU}...")
        self.model = Qwen3ASRModel.from_pretrained(
            MODEL_ID, dtype=torch.bfloat16, device_map="cuda:0",
            max_inference_batch_size=8, max_new_tokens=256,
        )
        hf_cache.commit()
        print("[ASR] Model loaded.")

    def transcribe(self, audio, language: str):
        # the backend sends "ar"; Qwen3-ASR takes language names
        lang = "Arabic" if (language or "ar").lower().startswith("ar") else None
        result = self.model.transcribe(audio=(audio, 16000), language=lang)
        return (result[0].text or "").strip(), 0.0  # no per-token confidence exposed

    @modal.asgi_app()
    def web(self):
        from modal_asr_common import build_api

        return build_api("ECFD ASR (Modal / QwenCleo-ASR)", MODEL_ID, GPU, self.transcribe)
