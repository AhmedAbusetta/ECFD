"""Anti-spoofing test set: real team voices vs synthetic voices saying the same sentences, all at phone quality.

Real   ml/asr/test2/recordings (the Test 2 calls, 105 turns by 8 people)
Fake   the same 105 sentences from ml/asr/test2/calls.json, spoken by text-to-speech generators:
         edge-salma / edge-shakir   Microsoft neural TTS, Egyptian Arabic voices (ar-EG)
         gtts                       Google Translate TTS, Arabic
         (more generators can be added with --add DIR: a folder of <turn-id>.wav made elsewhere)
Every clip goes through the same phone channel as Test 2 (8 kHz G.711 mu-law) and is stored as
8 kHz 16-bit WAV, so a detector never sees a cleaner "fake" than "real" and can't cheat on quality.

No voice is cloned: the synthetic voices are the generators' stock voices, not team members.
Output (git-ignored: it contains the team's voices): ml/antispoof/testset/audio/<source>/<turn-id>.wav
and manifest.json. Run from the repo root with the ASR virtualenv:
  ml/asr/.venv/Scripts/python ml/antispoof/testset/make_testset.py
"""

import argparse
import asyncio
import io
import json
import re
import shutil
import sys
import wave
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / "ml" / "asr" / "test1"))
import score_test1 as t1  # noqa: E402  (load_phone_audio: any file -> 8 kHz mu-law)

CALLS = ROOT / "ml" / "asr" / "test2" / "calls.json"
RECORDINGS = ROOT / "ml" / "asr" / "test2" / "recordings"
OUT = HERE / "audio"
REAL = re.compile(r"^T2_(K\d{2})_(\d{2})_")


def mulaw_decode(data: bytes) -> np.ndarray:
    u = ~np.frombuffer(data, dtype=np.uint8).astype(np.int32) & 0xFF
    sign, exponent, mantissa = u & 0x80, (u >> 4) & 0x07, u & 0x0F
    sample = (((mantissa << 3) + 0x84) << exponent) - 0x84
    return np.where(sign != 0, -sample, sample).astype(np.int16)


def write_phone_wav(source: Path, dest: Path) -> float:
    """Any audio file -> phone channel -> 8 kHz 16-bit mono WAV. Returns seconds."""
    pcm = mulaw_decode(t1.load_phone_audio(source))
    dest.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(dest), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(8000)
        w.writeframes(pcm.tobytes())
    return len(pcm) / 8000


def sentences() -> dict:
    calls = json.loads(CALLS.read_text(encoding="utf-8"))["calls"]
    return {f"{c['id']}_{i:02d}": turn["text"] for c in calls for i, turn in enumerate(c["turns"], 1)}


async def edge(text: str, voice: str, path: Path) -> None:
    import edge_tts

    await edge_tts.Communicate(text, voice).save(str(path))


def gtts(text: str, path: Path) -> None:
    from gtts import gTTS

    gTTS(text, lang="ar").save(str(path))


GENERATORS = {
    "edge-salma": lambda text, path: asyncio.run(edge(text, "ar-EG-SalmaNeural", path)),
    "edge-shakir": lambda text, path: asyncio.run(edge(text, "ar-EG-ShakirNeural", path)),
    "gtts": gtts,
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", help="comma-separated sources to (re)build, e.g. real,gtts")
    parser.add_argument("--add", action="append", default=[], metavar="NAME=DIR",
                        help="extra fake source: a folder of <turn-id>.wav/.mp3 made by another generator")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    only = set(args.only.split(",")) if args.only else None
    texts = sentences()
    manifest_path = HERE / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else []
    keep = [m for m in manifest if only is not None and m["source"] not in only]
    added = []

    def build(source: str):
        return only is None or source in only

    if build("real"):
        for f in sorted(RECORDINGS.iterdir()):
            m = REAL.match(f.name)
            if m:
                turn = f"{m.group(1)}_{m.group(2)}"
                secs = write_phone_wav(f, OUT / "real" / f"{turn}.wav")
                added.append({"file": f"real/{turn}.wav", "source": "real", "label": "bonafide", "turn": turn, "seconds": round(secs, 2)})
        print(f"real: {sum(1 for a in added if a['source'] == 'real')} clips")

    tmp = HERE / "_tmp"
    tmp.mkdir(exist_ok=True)
    for name, make in GENERATORS.items():
        if not build(name):
            continue
        n = 0
        for turn, text in texts.items():
            raw = tmp / f"{name}_{turn}.mp3"
            for attempt in range(3):
                try:
                    make(text, raw)
                    break
                except Exception as e:  # network TTS: retry a couple of times
                    if attempt == 2:
                        print(f"  {name} {turn}: failed ({e})")
            if raw.exists() and raw.stat().st_size > 0:
                secs = write_phone_wav(raw, OUT / name / f"{turn}.wav")
                added.append({"file": f"{name}/{turn}.wav", "source": name, "label": "spoof", "turn": turn, "seconds": round(secs, 2)})
                n += 1
        print(f"{name}: {n} clips")

    for spec in args.add:
        name, folder = spec.split("=", 1)
        n = 0
        for f in sorted(Path(folder).iterdir()):
            if f.stem in texts:
                secs = write_phone_wav(f, OUT / name / f"{f.stem}.wav")
                added.append({"file": f"{name}/{f.stem}.wav", "source": name, "label": "spoof", "turn": f.stem, "seconds": round(secs, 2)})
                n += 1
        print(f"{name}: {n} clips (added)")

    shutil.rmtree(tmp, ignore_errors=True)
    manifest = keep + added
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=1), encoding="utf-8")
    real = sum(1 for m in manifest if m["label"] == "bonafide")
    print(f"\nmanifest: {len(manifest)} clips ({real} real, {len(manifest) - real} synthetic) -> {manifest_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
