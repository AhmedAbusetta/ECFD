"""Generate the spoken warnings the employee hears during a risky call (telephony/asterisk/sounds).

Each file = a short two-tone chime + one Egyptian Arabic sentence, written as Asterisk plays them:
  ecfd-warn-<kind>.wav    8 kHz 16-bit mono (any phone codec)
  ecfd-warn-<kind>.sln16  16 kHz raw signed linear (used for G.722 phones - better quality)
The kinds match EmployeeWarnings in backend/ECFD.Application/Alerts/EmployeeWarnings.cs.

Needs: pip install edge-tts, and ffmpeg on PATH. Run again after editing a sentence:
  python telephony/tools/make_warning_sounds.py
"""

import asyncio
import subprocess
import tempfile
from pathlib import Path

import edge_tts

VOICE = "ar-EG-SalmaNeural"
OUT = Path(__file__).resolve().parents[1] / "asterisk" / "sounds"

WARNINGS = {
    "otp": "تنبيه، المتصل بيطلب كود التحقق، متدّيهوش.",
    "secret": "تنبيه، المتصل بيطلب بيانات سرية، متقولهاش.",
    "payment": "تنبيه، المتصل بيطلب تحويل فلوس، متحوّلش.",
    "remote": "تنبيه، المتصل عايزك تنزّل برنامج تحكم، متنزّلوش.",
    "impersonation": "تنبيه، المتصل ممكن ينتحل صفة البنك، اتأكد منه.",
    "pressure": "تنبيه، المتصل بيضغط عليك، خد وقتك.",
    "general": "تنبيه، احتمال نصب في المكالمة، خلّي بالك.",
}

# two rising tones, then a short pause before the voice
CHIME = ("sine=frequency=880:duration=0.15,volume=0.35[a];sine=frequency=1320:duration=0.2,volume=0.35[b];"
         "anullsrc=r=24000:cl=mono,atrim=duration=0.25[c];[a][b][c]concat=n=3:v=0:a=1")


async def speak(text: str, mp3: Path) -> None:
    await edge_tts.Communicate(text, VOICE, rate="+0%").save(str(mp3))


def ffmpeg(*args: str) -> None:
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *args], check=True)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as tmp:
        chime = Path(tmp) / "chime.wav"
        ffmpeg("-filter_complex", CHIME, "-ar", "24000", "-ac", "1", str(chime))
        for kind, text in WARNINGS.items():
            mp3 = Path(tmp) / f"{kind}.mp3"
            asyncio.run(speak(text, mp3))
            joined = ["-i", str(chime), "-i", str(mp3), "-filter_complex",
                      "[0:a]aresample=24000[x];[1:a]aresample=24000,aformat=channel_layouts=mono,"
                      # the voice pauses ~1 s at each full stop: keep at most 0.3 s, and none at the end
                      "silenceremove=stop_periods=-1:stop_duration=0.3:stop_threshold=-45dB:stop_silence=0.3,"
                      "areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse[y];"
                      "[x][y]concat=n=2:v=0:a=1,loudnorm=I=-16:TP=-1.5"]
            ffmpeg(*joined, "-ar", "8000", "-ac", "1", "-c:a", "pcm_s16le", str(OUT / f"ecfd-warn-{kind}.wav"))
            ffmpeg(*joined, "-ar", "16000", "-ac", "1", "-f", "s16le", str(OUT / f"ecfd-warn-{kind}.sln16"))
            print(f"ecfd-warn-{kind}: {text}")


if __name__ == "__main__":
    main()
