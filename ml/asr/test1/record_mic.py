"""Record the Test 1 lines with your own microphone.

Shows each line from scripts.json; press Enter to start, read it naturally, press Enter to stop.
Files are saved as recordings/T1_<clip>_<YourName>.wav, ready for score_test1.py.

Usage (repo root):
    ml/asr/.venv/Scripts/python ml/asr/test1/record_mic.py Ahmed
    ml/asr/.venv/Scripts/python ml/asr/test1/record_mic.py Ahmed --only C04,C07     (re-record some lines)
"""

import argparse
import json
import sys
import threading
import wave
from pathlib import Path

import numpy as np
import sounddevice as sd

HERE = Path(__file__).resolve().parent
RATE = 16000  # recorded clean; score_test1.py converts it to 8 kHz phone quality


def record_until_enter() -> np.ndarray:
    chunks, stop = [], threading.Event()

    def callback(indata, frames, t, status):
        chunks.append(indata.copy())

    with sd.InputStream(samplerate=RATE, channels=1, dtype="int16", callback=callback):
        input()  # second Enter stops the recording
    return np.concatenate(chunks).reshape(-1) if chunks else np.zeros(0, np.int16)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("name", help="your name, used in the file names (letters only, e.g. Ahmed)")
    parser.add_argument("--only", help="comma-separated clip ids to (re)record, e.g. C04,C07")
    args = parser.parse_args()
    if not args.name.isalnum():
        print("Use letters/digits only for the name, e.g. Ahmed")
        return 1
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    clips = json.loads((HERE / "scripts.json").read_text(encoding="utf-8"))["clips"]
    if args.only:
        wanted = {c.strip().upper() for c in args.only.split(",")}
        clips = [c for c in clips if c["id"] in wanted]
    out_dir = HERE / "recordings"
    out_dir.mkdir(exist_ok=True)

    mic = sd.query_devices(kind="input")["name"]
    print(f"Microphone: {mic}")
    print("Talk naturally, like a phone call. Keep the mic about a hand's width from your mouth.")
    print("For each line: Enter = start, read the line, Enter = stop.  Ctrl+C = quit.\n")

    for c in clips:
        print("-" * 60)
        print(f"{c['id']}" + ("   (play TV / street noise in the background for this one)" if c.get("noise") else ""))
        print(f"\n    {c['text']}\n")
        while True:
            input("  Enter to START ...")
            print("  ● recording ... Enter to STOP")
            audio = record_until_enter()
            seconds = len(audio) / RATE
            peak = int(np.abs(audio).max()) if len(audio) else 0
            if seconds < 1.0:
                print(f"  too short ({seconds:.1f} s) - again.")
                continue
            if peak < 1000:
                print("  almost silent - is the mic muted? - again.")
                continue
            path = out_dir / f"T1_{c['id']}_{args.name}.wav"
            with wave.open(str(path), "wb") as w:
                w.setnchannels(1)
                w.setsampwidth(2)
                w.setframerate(RATE)
                w.writeframes(audio.astype(np.int16).tobytes())
            keep = input(f"  saved {path.name} ({seconds:.1f} s). Enter = next, r = record again: ").strip().lower()
            if keep != "r":
                break

    print(f"\nDone. Score your recordings with:\n"
          f"  ml/asr/.venv/Scripts/python ml/asr/test1/score_test1.py --speakers {args.name}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\nStopped. Lines already saved are kept.")
