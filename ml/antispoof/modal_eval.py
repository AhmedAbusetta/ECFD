"""Anti-spoofing step 2: score the phone-quality test set (testset/make_testset.py) with candidate detectors
on a Modal GPU, then report EER and false alarms per detector and per fake-voice generator.

Detectors (all MIT-licensed, pretrained on ASVspoof; none fine-tuned on our data):
  aasist    AASIST (clovaai/aasist, weights in the repo) - the baseline named in the project paper
  xlsr-sls  XLS-R 300M + SLS classifier, v1 checkpoint (sukhdeveyash/XLS-R-SLS-Deepfake-Detection)
Both expect 16 kHz audio cut / repeat-padded to 64,600 samples (~4 s); output[:, 1] is the
"real voice" (bona fide) log-probability, so higher = more likely real.

Run from the repo root (ASR virtualenv for numpy):
  modal run ml/antispoof/modal_eval.py
Scores go to ml/antispoof/testset/results/ (git-ignored).
"""

import json
from pathlib import Path

import modal

HERE = Path(__file__).resolve().parent
TESTSET = HERE / "testset"
CUT = 64600

aasist_image = (
    modal.Image.debian_slim(python_version="3.11")
    .apt_install("git")
    .pip_install("torch", "numpy", "soundfile", "scipy")
    .run_commands("git clone --depth 1 https://github.com/clovaai/aasist /aasist")
)

# XLS-R + SLS needs the old fairseq commit the authors used, so it gets its own older stack.
sls_image = (
    modal.Image.debian_slim(python_version="3.10")
    .apt_install("git", "wget")
    .pip_install("pip<24.1")
    .pip_install("torch==1.13.1", "numpy==1.23.5", "soundfile", "scipy", "huggingface_hub")
    .run_commands(
        "pip install git+https://github.com/facebookresearch/fairseq.git@a54021305d6b3c4c5959ac9395135f63202db8f1",
        "git clone --depth 1 https://github.com/Yash-Sukhdeve/XLS-R-SLS-Deepfake-Detection /sls",
        "wget -q -O /xlsr2_300m.pt https://dl.fbaipublicfiles.com/fairseq/wav2vec/xlsr2_300m.pt",
    )
    .env({"PYTHONPATH": "/sls/src"})
)

# NII AntiDeepfake (post-trained on 56k h real + 18k h fake speech, 100+ languages). Weights: CC BY-NC-SA 4.0,
# research / education only. Same fairseq stack; their repo provides each model's architecture config.
nii_image = sls_image.pip_install("safetensors").run_commands(
    "git clone --depth 1 https://github.com/nii-yamagishilab/AntiDeepfake /nii"
)
NII_MODELS = {  # our name -> (Hugging Face repo, NII config key)
    "nii-mms-300m": ("nii-yamagishilab/mms-300m-anti-deepfake", "mms_300m"),
    "nii-w2v-large": ("nii-yamagishilab/wav2vec-large-anti-deepfake", "w2v_large"),
    "nii-xlsr-1b": ("nii-yamagishilab/xls-r-1b-anti-deepfake", "xlsr_1b"),
    "nii-xlsr-2b": ("nii-yamagishilab/xls-r-2b-anti-deepfake", "xlsr_2b"),
}

app = modal.App("ecfd-antispoof-eval")


def _prepare(wav_bytes: bytes):
    """8 kHz phone WAV -> 16 kHz float, first 64,600 samples (repeat-padded like the authors' eval)."""
    import io

    import numpy as np
    import soundfile as sf
    from scipy.signal import resample_poly

    x, sr = sf.read(io.BytesIO(wav_bytes), dtype="float32")
    if x.ndim > 1:
        x = x.mean(axis=1)
    if sr != 16000:
        x = resample_poly(x, 16000, sr).astype("float32")
    if len(x) >= CUT:
        return x[:CUT]
    return np.tile(x, CUT // len(x) + 1)[:CUT]


def _score_all(model, clips: dict, forward) -> dict:
    import numpy as np
    import torch

    ids = list(clips)
    scores = {}
    for i in range(0, len(ids), 16):
        batch = ids[i:i + 16]
        x = torch.from_numpy(np.stack([_prepare(clips[k]) for k in batch])).cuda()
        with torch.no_grad():
            out = forward(model, x)
        for k, s in zip(batch, out[:, 1].float().cpu().numpy().tolist()):
            scores[k] = s
    return scores


@app.function(image=aasist_image, gpu="A10G", timeout=1800)
def score_aasist(clips: dict) -> dict:
    import sys

    import torch

    sys.path.insert(0, "/aasist")
    from models.AASIST import Model

    config = json.load(open("/aasist/config/AASIST.conf"))["model_config"]
    model = Model(config).cuda()
    model.load_state_dict(torch.load("/aasist/models/weights/AASIST.pth", map_location="cuda"))
    model.eval()
    return _score_all(model, clips, lambda m, x: m(x)[1])


@app.function(image=sls_image, gpu="A10G", timeout=1800)
def score_xlsr_sls(clips: dict) -> dict:
    from types import SimpleNamespace

    import torch
    from huggingface_hub import hf_hub_download
    from sls_asvspoof.model import Model

    import sls_asvspoof.model as sls

    # The authors ran a modified fairseq that always returns every transformer layer's output. Stock
    # fairseq only collects them when given a target layer, so ask for the last one (index 23 of 24).
    def extract_feat(self, input_data):
        self.model.to(input_data.device, dtype=input_data.dtype)
        x = input_data[:, :, 0] if input_data.ndim == 3 else input_data
        out = self.model(x, mask=False, features_only=True, layer=len(self.model.encoder.layers) - 1)
        return out["x"], out["layer_results"]

    sls.SSLModel.extract_feat = extract_feat
    model = Model(SimpleNamespace(xlsr_model="/xlsr2_300m.pt"), "cuda").cuda()
    ckpt = hf_hub_download(repo_id="sukhdeveyash/XLS-R-SLS-Deepfake-Detection", filename="v1/epoch_2.pth")
    state = torch.load(ckpt, map_location="cuda")
    for key in ("model_state_dict", "state_dict", "model"):  # some checkpoints wrap the weights
        if isinstance(state, dict) and key in state and isinstance(state[key], dict):
            state = state[key]
    state = {k[len("module."):] if k.startswith("module.") else k: v for k, v in state.items()}  # DataParallel
    missing, unexpected = model.load_state_dict(state, strict=False)
    print(f"[xlsr-sls] checkpoint keys: {len(state)}, missing {len(missing)}, unexpected {len(unexpected)}")
    if missing or unexpected:
        print("  sample checkpoint keys:", list(state)[:5], "| missing:", missing[:5], "| unexpected:", unexpected[:5])
        raise RuntimeError("checkpoint does not match the model")
    model.eval()  # deterministic: no dropout / layer-drop (our extract_feat no longer forces train mode)
    return _score_all(model, clips, lambda m, x: m(x))


@app.function(image=nii_image, gpu="A10G", timeout=3600)
def score_nii(name: str, clips: dict) -> dict:
    """Whole clip at 16 kHz, layer-normalised (as on the model cards); score = real logit - fake logit."""
    import io
    import sys

    import soundfile as sf
    import torch
    import torch.nn.functional as F
    from fairseq.models.wav2vec import Wav2Vec2Config, Wav2Vec2Model
    from huggingface_hub import hf_hub_download
    from safetensors.torch import load_file
    from scipy.signal import resample_poly

    sys.path.insert(0, "/nii")
    from models.W2V_configs import global_configs, global_input_dims

    repo, key = NII_MODELS[name]
    cfg = {k: v for k, v in global_configs[key].items() if k != "_name"}

    class SSL(torch.nn.Module):
        def __init__(self):
            super().__init__()
            self.model = Wav2Vec2Model(Wav2Vec2Config(**cfg))

    class Detector(torch.nn.Module):
        def __init__(self):
            super().__init__()
            self.m_ssl = SSL()
            self.adap_pool1d = torch.nn.AdaptiveAvgPool1d(1)
            self.proj_fc = torch.nn.Linear(global_input_dims[key], 2)

        def forward(self, wav):
            emb = self.m_ssl.model(wav, mask=False, features_only=True)["x"]  # [B, T, D]
            return self.proj_fc(self.adap_pool1d(emb.transpose(1, 2)).squeeze(-1))

    model = Detector()
    state = load_file(hf_hub_download(repo, "model.safetensors"))
    missing, unexpected = model.load_state_dict(state, strict=False)
    print(f"[{name}] keys {len(state)}, missing {len(missing)}, unexpected {len(unexpected)}")
    if missing:
        print("  missing:", missing[:5], "| sample keys:", list(state)[:5])
        raise RuntimeError("checkpoint does not match the model")
    model = model.cuda().eval()

    scores = {}
    for k, wav_bytes in clips.items():
        x, sr = sf.read(io.BytesIO(wav_bytes), dtype="float32")
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = torch.from_numpy(resample_poly(x, 16000, sr).astype("float32"))
        x = F.layer_norm(x, x.shape).unsqueeze(0).cuda()
        with torch.no_grad():
            logits = model(x)[0]
        scores[k] = float(logits[1] - logits[0])
    return scores


def eer(real: list, fake: list):
    """Equal error rate and the threshold where real voices wrongly flagged = fakes wrongly accepted."""
    import numpy as np

    real, fake = np.array(real), np.array(fake)
    best = (2.0, 0.0, 1.0)  # (gap, threshold, eer)
    for t in np.unique(np.concatenate([real, fake])):
        frr = (real < t).mean()     # real voices called fake
        far = (fake >= t).mean()    # fakes called real
        gap = abs(frr - far)
        if gap < best[0]:
            best = (gap, t, (frr + far) / 2)
    return best[2], best[1]


@app.local_entrypoint()
def main(detectors: str = "aasist,xlsr-sls"):
    manifest = json.loads((TESTSET / "manifest.json").read_text(encoding="utf-8"))
    clips = {m["file"]: (TESTSET / "audio" / m["file"]).read_bytes() for m in manifest}
    label = {m["file"]: m["label"] for m in manifest}
    source = {m["file"]: m["source"] for m in manifest}
    out_dir = TESTSET / "results"
    out_dir.mkdir(exist_ok=True)

    runners = {"aasist": score_aasist.spawn, "xlsr-sls": score_xlsr_sls.spawn}
    runners.update({n: (lambda c, n=n: score_nii.spawn(n, c)) for n in NII_MODELS})
    chosen = [d for d in detectors.split(",") if d in runners]
    calls = {d: runners[d](clips) for d in chosen}   # the detectors run at the same time
    for name, call in calls.items():
        scores = call.get()
        (out_dir / f"scores_{name}.json").write_text(json.dumps(scores, indent=1), encoding="utf-8")
        real = [s for k, s in scores.items() if label[k] == "bonafide"]
        print(f"\n=== {name}  ({len(scores)} clips)")
        overall, threshold = eer(real, [s for k, s in scores.items() if label[k] == "spoof"])
        print(f"  overall EER {overall * 100:5.1f}%   (threshold {threshold:.3f})")
        for gen in sorted({source[k] for k in scores if label[k] == "spoof"}):
            e, _ = eer(real, [s for k, s in scores.items() if source[k] == gen])
            print(f"  vs {gen:<12} EER {e * 100:5.1f}%")
        flagged = sum(1 for s in real if s < threshold)
        print(f"  real voices flagged as fake at that threshold: {flagged}/{len(real)}")
