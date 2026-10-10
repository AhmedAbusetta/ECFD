"""
ECFD ASR on Modal (cloud GPU) — Whisper large-v3 + an Egyptian-Arabic LoRA adapter, merged at load.

One file, deployed once per adapter (the app name follows the adapter):
  WHISPER_LORA=AbdelrahmanHassan/whisper-large-v3-egyptian-arabic WHISPER_APP=ecfd-asr-whisper-ah \
      modal deploy ml/asr/modal_whisper_lora_app.py
  WHISPER_LORA=maryamas222/whisper-large-v3-egyptian-lora-v4 WHISPER_APP=ecfd-asr-whisper-mm \
      modal deploy ml/asr/modal_whisper_lora_app.py
Same contract as modal_app.py (Cohere), so the backend or the Test 2 scorer can point at either URL.
"""

import os

import modal

BASE_MODEL = "openai/whisper-large-v3"
ADAPTER = os.getenv("WHISPER_LORA", "maryamas222/whisper-large-v3-egyptian-lora-v4")
APP_NAME = os.getenv("WHISPER_APP", "ecfd-asr-whisper-lora")
GPU = os.getenv("ASR_GPU", "A10G")
MAX_CONTAINERS = int(os.getenv("ASR_MAX_CONTAINERS", "1"))

image = (
    modal.Image.debian_slim(python_version="3.12")
    .pip_install("transformers>=4.45,<5", "peft", "torch", "accelerate", "huggingface_hub",
                 "av>=11", "numpy", "fastapi>=0.109.0", "pydantic>=2.6.0")
    .env({"HF_HOME": "/cache/hf", "WHISPER_LORA": ADAPTER})
    .add_local_python_source("modal_asr_common")
)

hf_cache = modal.Volume.from_name("ecfd-hf-cache", create_if_missing=True)

app = modal.App(APP_NAME)


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
class WhisperLoraAsr:
    @modal.enter()
    def load(self):
        import torch
        from peft import PeftModel
        from transformers import AutoModelForSpeechSeq2Seq, AutoProcessor

        adapter = os.environ["WHISPER_LORA"]
        print(f"[ASR] Loading {BASE_MODEL} + {adapter} on {GPU}...")
        self.processor = AutoProcessor.from_pretrained(BASE_MODEL)
        base = AutoModelForSpeechSeq2Seq.from_pretrained(BASE_MODEL, torch_dtype=torch.float16).to("cuda")
        self.model = PeftModel.from_pretrained(base, adapter).merge_and_unload().eval()
        self.adapter = adapter
        hf_cache.commit()
        print("[ASR] Model loaded.")

    def transcribe(self, audio, language: str):
        import torch

        inputs = self.processor(audio, sampling_rate=16000, return_tensors="pt")
        features = inputs.input_features.to("cuda", dtype=torch.float16)
        with torch.inference_mode():
            ids = self.model.generate(features, language="ar", task="transcribe", max_new_tokens=225)
        text = self.processor.batch_decode(ids, skip_special_tokens=True)[0]
        return text.strip(), 0.0

    @modal.asgi_app()
    def web(self):
        from modal_asr_common import build_api

        return build_api(f"ECFD ASR (Modal / Whisper large-v3 + {ADAPTER})", ADAPTER, GPU, self.transcribe)
