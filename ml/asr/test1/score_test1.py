"""Test 1 - speech to text on phone-quality clips.

Pass line: keyword recall >= 70% on the protected words (code, OTP, password, CVV,
card number, account, transfer, AnyDesk). Word error rate is reported for information.

For every recording in recordings/ named T1_<clip>_<speaker>.<ext>:
  1. convert to phone quality: 8 kHz mono G.711 mu-law (what Asterisk sends)
  2. transcribe with the chosen engine (each clip is transcribed once and cached)
  3. compare with the answer key in scripts.json

Engines:
  speechmatics   real-time API, settings from the project brief (needs SPEECHMATICS_API_KEY)
  whisper-service  the running ml/asr faster-whisper service - DRY RUN ONLY, to test this scorer
  cohere-modal     Cohere Transcribe Arabic on Modal (ml/asr/modal_app.py); URL from COHERE_ASR_URL in .env
  qwen-modal       QwenCleo-ASR on Modal (ml/asr/modal_qwen_app.py); URL from QWEN_ASR_URL
  nemotron-modal   NVIDIA Nemotron 3.5 ASR on Modal (ml/asr/modal_nemotron_app.py); URL from NEMOTRON_ASR_URL
  whisper-ah-modal / whisper-mm-modal   Whisper large-v3 + Egyptian LoRA (ml/asr/modal_whisper_lora_app.py)

Run with the ASR virtualenv (it has PyAV, numpy and websockets), from the repo root:
  ml/asr/.venv/Scripts/python ml/asr/test1/score_test1.py
  ml/asr/.venv/Scripts/python ml/asr/test1/score_test1.py --engine whisper-service
  ml/asr/.venv/Scripts/python ml/asr/test1/score_test1.py --speakers Ahmed,Mona   (DEV speakers only)
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / "ml" / "brain"))

from ecfd_brain.normalize import normalize  # noqa: E402

PASS_LINE = 0.70
FILE_PATTERN = re.compile(r"^T1_(C\d{2})_(.+)\.(m4a|mp3|wav|ogg|opus|aac|flac|webm|3gp|amr)$", re.IGNORECASE)


# ---------------------------------------------------------------- audio

def load_phone_audio(path: Path) -> bytes:
    """Decode any recording and return 8 kHz mono G.711 mu-law bytes."""
    import av

    pcm = []
    with av.open(str(path)) as container:
        resampler = av.AudioResampler(format="s16", layout="mono", rate=8000)
        stream = container.streams.audio[0]
        for packet in container.demux(stream):
            try:
                frames = packet.decode()
            except av.error.InvalidDataError:
                continue  # one damaged frame (some phone exports end with one): skip it like a player would
            for frame in frames:
                for out in resampler.resample(frame):
                    pcm.append(out.to_ndarray().reshape(-1))
        for out in resampler.resample(None):  # flush
            pcm.append(out.to_ndarray().reshape(-1))
    samples = np.concatenate(pcm).astype(np.int16) if pcm else np.zeros(0, np.int16)
    return mulaw_encode(samples).tobytes()


def mulaw_encode(samples: np.ndarray) -> np.ndarray:
    """16-bit linear PCM -> 8-bit G.711 mu-law (standard algorithm)."""
    bias, clip = 0x84, 32635
    x = samples.astype(np.int32)
    sign = (x < 0).astype(np.int32)
    x = np.minimum(np.abs(x), clip) + bias
    exponent = np.clip(np.floor(np.log2(x)).astype(np.int32) - 7, 0, 7)
    mantissa = (x >> (exponent + 3)) & 0x0F
    return (~((sign << 7) | (exponent << 4) | mantissa) & 0xFF).astype(np.uint8)


def mulaw_decode(data: bytes) -> np.ndarray:
    """8-bit G.711 mu-law -> 16-bit linear PCM."""
    u = ~np.frombuffer(data, dtype=np.uint8).astype(np.int32) & 0xFF
    sign, exponent, mantissa = u & 0x80, (u >> 4) & 0x07, u & 0x0F
    magnitude = (((mantissa << 3) + 0x84) << exponent) - 0x84
    return np.where(sign != 0, -magnitude, magnitude).astype(np.int16)


# ---------------------------------------------------------------- engines

class SpeechmaticsRealtime:
    """Speechmatics real-time WebSocket API with the settings from the project brief."""

    def __init__(self, api_key: str, url: str, language: str, vocab: list):
        self.api_key, self.url, self.language, self.vocab = api_key, url, language, vocab

    def config(self) -> dict:
        return {
            "language": self.language,
            "operating_point": "enhanced",
            "max_delay": 1.0,
            "enable_partials": True,
            "additional_vocab": self.vocab,
        }

    def describe(self) -> str:
        return f"speechmatics-rt:{self.language}:enhanced"

    def transcribe(self, mulaw: bytes) -> str:
        from websockets.sync.client import connect

        start = {
            "message": "StartRecognition",
            # 8 kHz mu-law sent untouched, exactly as it will come from Asterisk
            "audio_format": {"type": "raw", "encoding": "mulaw", "sample_rate": 8000},
            "transcription_config": self.config(),
        }
        words, seq = [], 0
        with connect(self.url, additional_headers={"Authorization": f"Bearer {self.api_key}"},
                     open_timeout=20, max_size=None) as ws:
            ws.send(json.dumps(start))
            self._wait_for(ws, "RecognitionStarted")
            chunk = 1600  # 0.2 s of 8 kHz mu-law
            for i in range(0, len(mulaw), chunk):
                ws.send(mulaw[i:i + chunk])
                seq += 1
                time.sleep(0.02)  # faster than real time, gentle on the API
            ws.send(json.dumps({"message": "EndOfStream", "last_seq_no": seq}))
            while True:
                msg = json.loads(ws.recv(timeout=60))
                kind = msg.get("message")
                if kind == "AddTranscript":
                    for r in msg.get("results", []):
                        if r.get("alternatives"):
                            words.append((r.get("type"), r["alternatives"][0]["content"]))
                elif kind == "EndOfTranscript":
                    break
                elif kind == "Error":
                    raise RuntimeError(f"Speechmatics error {msg.get('type')}: {msg.get('reason')}")
        # join words; punctuation attaches to the previous word
        text = ""
        for kind, content in words:
            text += content if kind == "punctuation" else (" " + content)
        return text.strip()

    @staticmethod
    def _wait_for(ws, wanted: str):
        while True:
            msg = json.loads(ws.recv(timeout=20))
            if msg.get("message") == wanted:
                return
            if msg.get("message") == "Error":
                raise RuntimeError(f"Speechmatics error {msg.get('type')}: {msg.get('reason')}")


class WhisperService:
    """DRY RUN ONLY: the project's own faster-whisper ASR service (ml/asr, port 8001),
    fed the same phone-quality audio (decoded back to 16 kHz). Re-uses the model the
    running service already holds, so no second copy is loaded into memory."""

    def __init__(self, url: str, label: str = "whisper-service", timeout: int = 5):
        import urllib.request
        self.url, self.label = url.rstrip("/"), label
        # A Modal cold start (loading the model onto the GPU) can take a minute or more.
        self.timeout = timeout
        with urllib.request.urlopen(self.url + "/health", timeout=timeout) as r:
            health = json.load(r)
        if not health.get("modelLoaded"):
            raise RuntimeError("ASR service is running in mock mode; start it with ML_USE_MOCK_MODE=false")
        self.model_name = health.get("model", "?")

    def describe(self) -> str:
        return f"{self.label}:{self.model_name.split('/')[-1]}"

    def config(self) -> dict:
        return {"model": self.model_name}

    def transcribe(self, mulaw: bytes) -> str:
        import base64
        import urllib.request
        pcm8 = mulaw_decode(mulaw).astype(np.float32)
        pcm16 = np.interp(np.arange(0, len(pcm8), 0.5), np.arange(len(pcm8)), pcm8).astype(np.int16)
        body = json.dumps({"sessionId": "test1", "segmentId": "test1", "sampleRate": 16000,
                           "audioFormat": "pcm_s16le", "audioBase64": base64.b64encode(pcm16.tobytes()).decode()})
        req = urllib.request.Request(self.url + "/v1/asr/analyze", body.encode(), {"content-type": "application/json"})
        with urllib.request.urlopen(req, timeout=max(120, self.timeout)) as r:
            return json.load(r)["text"]


# ---------------------------------------------------------------- scoring

_PUNCT = re.compile(r"[\.,!?؟،؛:\"'()\[\]{}«»…\-–—]")


def words_of(text: str) -> list:
    return _PUNCT.sub(" ", normalize(text)).split()


def contains_variant(transcript_norm: str, variant: str) -> bool:
    v = normalize(variant)
    if v.isascii():  # Latin words must match whole words ("otp" not inside another word)
        return re.search(rf"(?<![a-z0-9]){re.escape(v)}(?![a-z0-9])", transcript_norm) is not None
    return v in transcript_norm  # Arabic: allow attached prefixes/suffixes (ال، و، ب، ك)


def wer(reference: str, hypothesis: str) -> float:
    ref, hyp = words_of(reference), words_of(hypothesis)
    if not ref:
        return 0.0
    prev = list(range(len(hyp) + 1))
    for i, r in enumerate(ref, 1):
        cur = [i] + [0] * len(hyp)
        for j, h in enumerate(hyp, 1):
            cur[j] = min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (r != h))
        prev = cur
    return prev[-1] / len(ref)


def load_env(path: Path) -> None:
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                os.environ.setdefault(k.strip(), v.strip().strip('"').strip("'"))


def load_answer_key() -> dict:
    return json.loads((HERE / "scripts.json").read_text(encoding="utf-8"))


# extra engines deployed on Modal with the same contract: engine name -> .env variable holding its URL
MODAL_ENGINES = {
    "qwen-modal": "QWEN_ASR_URL",                  # ml/asr/modal_qwen_app.py
    "nemotron-modal": "NEMOTRON_ASR_URL",          # ml/asr/modal_nemotron_app.py
    "whisper-ah-modal": "WHISPER_AH_ASR_URL",      # modal_whisper_lora_app.py + AbdelrahmanHassan LoRA
    "whisper-mm-modal": "WHISPER_MM_ASR_URL",      # modal_whisper_lora_app.py + maryamas222 LoRA v4
}


def make_engine(name: str):
    if name == "speechmatics":
        api_key = os.getenv("SPEECHMATICS_API_KEY")
        if not api_key:
            raise RuntimeError("SPEECHMATICS_API_KEY is missing from .env")
        vocab = json.loads((Path(__file__).resolve().parent / "custom_dictionary.json").read_text(encoding="utf-8"))["additional_vocab"]
        return SpeechmaticsRealtime(api_key, os.getenv("SPEECHMATICS_RT_URL", "wss://eu2.rt.speechmatics.com/v2"),
                                    os.getenv("SPEECHMATICS_LANGUAGE", "ar_en"), vocab)
    if name == "cohere-modal":
        url = os.getenv("COHERE_ASR_URL")
        if not url:
            raise RuntimeError("COHERE_ASR_URL is missing from .env (the URL printed by `modal deploy`)")
        return WhisperService(url, label="cohere-modal", timeout=300)
    if name in MODAL_ENGINES:
        var = MODAL_ENGINES[name]
        url = os.getenv(var)
        if not url:
            raise RuntimeError(f"{var} is missing from .env (the URL printed by `modal deploy`)")
        return WhisperService(url, label=name, timeout=300)
    return WhisperService(os.getenv("ASR_SERVICE_URL", "http://localhost:8001"))


def config_hash(engine) -> str:
    return hashlib.sha256(json.dumps(engine.config(), sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:8]


def cache_path_for(engine) -> Path:
    """Transcripts are stored per engine + config, shared by the command line and the dashboard."""
    results_dir = HERE / "results"
    results_dir.mkdir(exist_ok=True)
    return results_dir / f"transcripts_{engine.describe().replace(':', '_')}_{config_hash(engine)}.json"


def load_cache(engine) -> dict:
    path = cache_path_for(engine)
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}


def transcribe_cached(engine, path: Path, cache: dict) -> dict:
    """Return the stored transcript for this exact audio, or transcribe it once and store it.

    Raises RuntimeError if the engine fails.
    """
    audio_hash = hashlib.sha1(path.read_bytes()).hexdigest()
    cached = cache.get(path.name)
    if cached and cached["audio_sha1"] == audio_hash:
        return cached
    mulaw = load_phone_audio(path)
    t0 = time.time()
    text = None
    for attempt in range(6):
        try:
            text = engine.transcribe(mulaw)
            break
        except Exception as e:
            # Free plan = 2 live sessions; a closed session takes a few seconds to be released.
            if "quota_exceeded" in str(e) and attempt < 5:
                time.sleep(5 * (attempt + 1))
                continue
            raise RuntimeError(str(e)) from None
    cached = {"audio_sha1": audio_hash, "transcript": text,
              "seconds": round(len(mulaw) / 8000, 2), "engine_ms": int((time.time() - t0) * 1000)}
    cache[path.name] = cached
    cache_path_for(engine).write_text(json.dumps(cache, ensure_ascii=False, indent=2), encoding="utf-8")
    return cached


def score_clip(clip: dict, groups: dict, transcript: str) -> dict:
    transcript_norm = " ".join(words_of(transcript))
    hits = [g for g in clip["keywords"] if any(contains_variant(transcript_norm, v) for v in groups[g])]
    misses = [g for g in clip["keywords"] if g not in hits]
    return {"hits": hits, "misses": misses, "wer": wer(clip["text"], transcript)}


def summarize(scored: list) -> dict:
    """scored: list of dicts with speaker, hits, misses, wer."""
    per_word, per_speaker = {}, {}
    for s in scored:
        for g, ok in [(g, True) for g in s["hits"]] + [(g, False) for g in s["misses"]]:
            for table, k in ((per_word, g), (per_speaker, s["speaker"])):
                table.setdefault(k, [0, 0])
                table[k][0] += ok
                table[k][1] += 1
    found = sum(len(s["hits"]) for s in scored)
    expected = sum(len(s["hits"]) + len(s["misses"]) for s in scored)
    wers = sorted(s["wer"] for s in scored)
    recall = found / expected if expected else 0.0
    return {
        "found": found, "expected": expected, "recall": recall,
        "median_wer": wers[len(wers) // 2] if wers else None,
        "per_word": per_word, "per_speaker": per_speaker,
        "pass_line": PASS_LINE, "passed": bool(expected) and recall >= PASS_LINE,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--engine", choices=["speechmatics", "whisper-service", "cohere-modal", *MODAL_ENGINES], default="speechmatics")
    parser.add_argument("--recordings", default=str(HERE / "recordings"))
    parser.add_argument("--speakers", help="comma-separated speaker names to include (e.g. the DEV speakers)")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    load_env(ROOT / ".env")

    key = load_answer_key()
    clips = {c["id"]: c for c in key["clips"]}
    groups = key["keyword_groups"]

    rec_dir = Path(args.recordings)
    files = sorted(p for p in rec_dir.glob("*") if FILE_PATTERN.match(p.name)) if rec_dir.exists() else []
    skipped = sorted(p.name for p in rec_dir.glob("*") if p.is_file() and not FILE_PATTERN.match(p.name)) if rec_dir.exists() else []
    wanted = {s.strip().lower() for s in args.speakers.split(",")} if args.speakers else None
    if wanted:
        files = [p for p in files if FILE_PATTERN.match(p.name).group(2).lower() in wanted]
    if not files:
        print(f"No recordings found in {rec_dir} (expected names like T1_C01_Ahmed.m4a).")
        return 1

    try:
        engine = make_engine(args.engine)
    except RuntimeError as e:
        print(e)
        return 1
    cache = load_cache(engine)

    rows = []
    for path in files:
        m = FILE_PATTERN.match(path.name)
        clip_id, speaker = m.group(1).upper(), m.group(2)
        if clip_id not in clips:
            skipped.append(path.name + " (unknown clip id)")
            continue
        try:
            cached = transcribe_cached(engine, path, cache)
        except RuntimeError as e:
            print(f"  ! {path.name}: {e}")  # keep going; report the failure per clip
            continue
        rows.append({"clip_id": clip_id, "speaker": speaker, "transcript": cached["transcript"],
                     **score_clip(clips[clip_id], groups, cached["transcript"])})

    if not rows:
        print("\nNo clip could be transcribed (see the errors above) - nothing to score.")
        return 1

    print(f"\nTest 1  engine={engine.describe()}  config={config_hash(engine)}  clips={len(rows)}")
    if args.engine == "whisper-service":
        print("DRY RUN: whisper-service only tests the scorer. Test 1 results must come from Speechmatics.")
    print()
    for r in sorted(rows, key=lambda r: (r["clip_id"], r["speaker"])):
        clip = clips[r["clip_id"]]
        print(f"{'OK  ' if not r['misses'] else 'MISS'} {r['clip_id']} {r['speaker']:<10} WER {r['wer']:4.0%}  heard: {r['transcript']}")
        print(f"      said:  {clip['text']}")
        if r["misses"]:
            print(f"      missed keywords: {', '.join(r['misses'])}")

    summary = summarize(rows)
    print("\nKeyword recall by word:")
    for g, (f, n) in sorted(summary["per_word"].items(), key=lambda kv: kv[1][0] / kv[1][1]):
        print(f"  {g:<12} {f}/{n}  {f / n:4.0%}")
    print("\nKeyword recall by speaker:")
    for sp, (f, n) in sorted(summary["per_speaker"].items()):
        print(f"  {sp:<12} {f}/{n}  {f / n:4.0%}")
    print(f"\nOverall keyword recall: {summary['found']}/{summary['expected']} = {summary['recall']:.0%}  (pass line {PASS_LINE:.0%})")
    print(f"Word error rate: median {summary['median_wer']:.0%} (brief expects ~30-45%; information only)")
    if skipped:
        print(f"Skipped files (bad name or unknown clip): {', '.join(skipped)}")
    if not wanted:
        print("Note: all speakers included. Tune only on DEV speakers (--speakers); keep TEST speakers for the final number.")
    print("TEST 1:", "PASS" if summary["passed"] else "FAIL", "(dry run)" if args.engine == "whisper-service" else "")
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
