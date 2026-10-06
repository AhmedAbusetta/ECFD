"""Simulated call: microphone -> Speechmatics real-time -> turns -> brain -> stage/score -> alerts.

The browser streams 16-bit PCM at 8 kHz over a WebSocket. This module runs pipeline stages 3-9
of the project brief for one caller and pushes every event back to the browser:

  3 Transcribe   Speechmatics RT, ar_en, enhanced, 8 kHz mu-law, partials on, max_delay 1.0, custom dictionary
  4 Build turns  a turn ends after 0.6 s of real silence AND once the final words have caught up,
                 or after 15 s of continuous speech
  5 Clean        Arabic normalisation (inside the brain)
  6 NLP          the brain on each turn + the previous 2 turns as context
  7 Track        stage machine (forward only, may skip)
  9 Score/alert  WATCH 30 / WARNING 60 / ALERT 80, ALERT on extraction, never drops

Each call is saved to ml/lab/calls/ (git-ignored) so Test 2 can measure it later.
"""

import asyncio
import json
import time
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

SILENCE_TO_CLOSE = 0.6      # seconds of real silence that end a turn
MAX_TURN_SECONDS = 15.0     # continuous speech that forces a turn to end
FINALS_TOLERANCE = 0.35     # how close the last final word must be to the end of speech
FINALS_GIVE_UP = 2.5        # if finals still lag this long after silence, close anyway
VOICE_RMS = 500             # 16-bit RMS above this counts as speech (simple energy VAD)
MAX_CALL_SECONDS = 600      # protect the free Speechmatics minutes
CALLS_DIR = Path(__file__).resolve().parent / "calls"


@dataclass
class Word:
    start: float
    end: float
    content: str
    kind: str  # "word" | "punctuation"


@dataclass
class Turn:
    n: int
    text: str
    start: float
    end: float
    reason: str
    speech_end_wall: float
    result: dict = field(default_factory=dict)


def join_words(words) -> str:
    text = ""
    for w in words:
        text += w.content if w.kind == "punctuation" else (" " + w.content)
    return text.strip()


class CallSession:
    def __init__(self, browser, sm_url: str, sm_key: str, sm_config: dict, brain, tracker, mulaw_encode):
        self.browser = browser
        self.sm_url, self.sm_key, self.sm_config = sm_url, sm_key, sm_config
        self.brain, self.tracker, self.mulaw_encode = brain, tracker, mulaw_encode

        self.audio_time = 0.0          # seconds of audio sent so far (Speechmatics' clock)
        self.last_voice = 0.0          # audio time of the last voiced frame
        self.last_voice_wall = 0.0     # wall-clock time that frame arrived (for latency)
        self.final_end = 0.0           # end time of the newest final word
        self.partial_end = 0.0         # end time of the newest partial (0 = none pending)
        self.partial_text = ""
        self.pending: list = []        # final words not yet in a turn
        self.turns: list = []
        self.seq = 0
        self.started_wall = time.time()
        self.brain_queue: asyncio.Queue = asyncio.Queue()
        self.ended = asyncio.Event()

    async def send(self, **event):
        try:
            await self.browser.send_text(json.dumps(event, ensure_ascii=False))
        except Exception:
            pass  # browser went away; the call still finishes and is saved

    # ------------------------------------------------------------------ main
    async def run(self):
        from websockets.asyncio.client import connect

        start = {
            "message": "StartRecognition",
            "audio_format": {"type": "raw", "encoding": "mulaw", "sample_rate": 8000},
            "transcription_config": self.sm_config,
        }
        async with connect(self.sm_url, additional_headers={"Authorization": f"Bearer {self.sm_key}"},
                           open_timeout=20, max_size=None) as sm:
            await sm.send(json.dumps(start))
            while True:
                msg = json.loads(await asyncio.wait_for(sm.recv(), 20))
                if msg.get("message") == "RecognitionStarted":
                    break
                if msg.get("message") == "Error":
                    raise RuntimeError(f"Speechmatics: {msg.get('type')}: {msg.get('reason')}")
            await self.send(type="status", state="live", msg="Call connected. Speak as the caller.")

            reader = asyncio.create_task(self.read_speechmatics(sm))
            ticker = asyncio.create_task(self.tick())
            worker = asyncio.create_task(self.brain_worker())
            try:
                await self.pump_browser(sm)
            finally:
                # flush: tell Speechmatics the audio is over and wait for the last final words
                try:
                    await sm.send(json.dumps({"message": "EndOfStream", "last_seq_no": self.seq}))
                    await asyncio.wait_for(reader, 15)
                except Exception:
                    reader.cancel()
                ticker.cancel()
                if self.pending:
                    self.close_turn("call_end")
                await self.brain_queue.put(None)
                await asyncio.wait_for(worker, 60)
        await self.finish()

    async def pump_browser(self, sm):
        """Browser -> (energy VAD) -> mu-law -> Speechmatics, until the caller hangs up."""
        while True:
            if time.time() - self.started_wall > MAX_CALL_SECONDS:
                await self.send(type="status", state="ending", msg="Call reached the 10-minute limit.")
                return
            message = await self.browser.receive()
            if message.get("type") == "websocket.disconnect":
                return
            if message.get("text"):
                if json.loads(message["text"]).get("type") == "end":
                    return
                continue
            data = message.get("bytes")
            if not data:
                continue
            pcm = np.frombuffer(data, dtype=np.int16)
            now = time.time()
            for i in range(0, len(pcm), 160):  # 20 ms sub-frames
                frame = pcm[i:i + 160]
                self.audio_time += len(frame) / 8000
                if np.sqrt(np.mean(frame.astype(np.float64) ** 2)) > VOICE_RMS:
                    self.last_voice, self.last_voice_wall = self.audio_time, now
            await sm.send(self.mulaw_encode(pcm).tobytes())  # 8 kHz mu-law, like Asterisk
            self.seq += 1

    async def read_speechmatics(self, sm):
        async for raw in sm:
            msg = json.loads(raw)
            kind = msg.get("message")
            if kind == "AddPartialTranscript":
                results = msg.get("results", [])
                self.partial_text = msg.get("metadata", {}).get("transcript", "").strip()
                self.partial_end = max((r.get("end_time", 0) for r in results), default=0.0)
            elif kind == "AddTranscript":
                for r in msg.get("results", []):
                    # a full stop that arrives after its turn already closed belongs to that turn: drop it
                    if r.get("type") == "punctuation" and not self.pending:
                        continue
                    if r.get("alternatives"):
                        self.pending.append(Word(r.get("start_time", 0), r.get("end_time", 0),
                                                 r["alternatives"][0]["content"], r.get("type", "word")))
                        self.final_end = max(self.final_end, r.get("end_time", 0))
                self.partial_text, self.partial_end = "", 0.0
            elif kind == "EndOfTranscript":
                return
            elif kind == "Error":
                await self.send(type="error", msg=f"Speechmatics: {msg.get('type')}: {msg.get('reason')}")
                return
            else:
                continue
            await self.send(type="live", final=join_words(self.pending), partial=self.partial_text)

    # ------------------------------------------------------------------ turn builder
    async def tick(self):
        while True:
            await asyncio.sleep(0.1)
            if not self.pending:
                continue
            silence = self.audio_time - self.last_voice
            finals_caught_up = (self.final_end >= self.last_voice - FINALS_TOLERANCE
                                and (self.partial_end == 0 or self.final_end >= self.partial_end - 0.05))
            if self.pending[-1].end - self.pending[0].start >= MAX_TURN_SECONDS:
                self.close_turn("15s_max")
            elif silence >= SILENCE_TO_CLOSE and finals_caught_up:
                self.close_turn("silence")
            elif silence >= SILENCE_TO_CLOSE + FINALS_GIVE_UP:
                self.close_turn("silence_finals_late")

    def close_turn(self, reason: str):
        words, self.pending = self.pending, []
        text = join_words(words)
        if not any(w.kind != "punctuation" for w in words):
            return  # punctuation only - not a turn
        turn = Turn(len(self.turns) + 1, text, words[0].start, words[-1].end, reason,
                    self.last_voice_wall or time.time())
        self.turns.append(turn)
        self.brain_queue.put_nowait(turn)
        asyncio.create_task(self.send(type="turn", n=turn.n, text=text, start=round(turn.start, 2),
                                      end=round(turn.end, 2), reason=reason))

    # ------------------------------------------------------------------ brain + tracker
    async def brain_worker(self):
        """One turn at a time, in order, so the stage and score evolve like the real call."""
        while True:
            turn = await self.brain_queue.get()
            if turn is None:
                return
            context = [t.text for t in self.turns if t.n < turn.n][-2:]
            t0 = time.time()
            try:
                r = await asyncio.to_thread(self.brain.classify, turn.text, context)
            except Exception as e:
                turn.result = {"error": str(e)[:200]}
                await self.send(type="result", n=turn.n, error=turn.result["error"])
                continue
            tactics = [{"label": t.label, "confidence": t.confidence, "quote": t.quote} for t in r.tactics]
            state = self.tracker.update(tactics)
            turn.result = {
                "tactics": tactics,
                "dropped": [{"label": t.label, "quote": t.quote, "reason": why} for t, why in r.dropped],
                "needs_more_context": r.needs_more_context,
                "brain_ms": int((time.time() - t0) * 1000),
                # caller stopped speaking -> result on screen: the delay Test 2 measures
                "latency_ms": int((time.time() - turn.speech_end_wall) * 1000),
                **state,
            }
            await self.send(type="result", n=turn.n, **turn.result)

    async def finish(self):
        latencies = sorted(t.result["latency_ms"] for t in self.turns if "latency_ms" in t.result)
        summary = {
            "turns": len(self.turns),
            "audio_seconds": round(self.audio_time, 1),
            "stage": self.tracker.stage,
            "level": self.tracker.level,
            "score": self.turns[-1].result.get("score", 0) if self.turns and self.turns[-1].result else 0,
            "median_latency_ms": latencies[len(latencies) // 2] if latencies else None,
            "max_latency_ms": latencies[-1] if latencies else None,
        }
        CALLS_DIR.mkdir(exist_ok=True)
        name = time.strftime("call_%Y%m%d_%H%M%S.json", time.localtime(self.started_wall))
        (CALLS_DIR / name).write_text(json.dumps({
            "summary": summary,
            "model": getattr(self.brain.provider, "model", "?"),
            "speechmatics": self.sm_config,
            "turns": [{"n": t.n, "text": t.text, "start": t.start, "end": t.end, "reason": t.reason, **t.result}
                      for t in self.turns],
        }, ensure_ascii=False, indent=2), encoding="utf-8")
        await self.send(type="ended", saved=name, **summary)
