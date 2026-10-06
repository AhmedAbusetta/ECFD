"""ECFD Lab - a small development service behind the dashboard's Tests page.

It reuses the exact code and stored results of the command-line tests:
  * Test 0 (brain)  -> ml/brain/ecfd_brain + ml/brain/test0/results_<model>.json
  * Test 1 (speech) -> ml/asr/test1/score_test1.py + ml/asr/test1/results/ + recordings/

Internal test tool for the team, not part of the product pipeline. The dashboard reaches it
through a Next.js rewrite (/lab/* -> http://localhost:8010/*), so the browser never calls
an AI provider directly.

Run (repo root):
  ml/asr/.venv/Scripts/python -m uvicorn app:app --app-dir ml/lab --port 8010
"""

import asyncio
import json
import re
import sys
import threading
import time
from pathlib import Path
from typing import List, Optional

from fastapi import FastAPI, File, Form, HTTPException, UploadFile, WebSocket, WebSocketDisconnect
from fastapi.responses import FileResponse
from pydantic import BaseModel

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ml" / "brain"))
sys.path.insert(0, str(ROOT / "ml" / "asr" / "test1"))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import score_test1 as t1  # noqa: E402
from call import CallSession  # noqa: E402
from ecfd_brain import Brain  # noqa: E402
from ecfd_brain import test0 as t0  # noqa: E402
from ecfd_brain.tracker import CallTracker  # noqa: E402

t1.load_env(ROOT / ".env")

app = FastAPI(title="ECFD Lab", description="Dashboard backend for Test 0 and Test 1")

RECORDINGS = t1.HERE / "recordings"
NAME_OK = re.compile(r"^[A-Za-z0-9]{1,30}$")
# Free Speechmatics plan = 2 live sessions; the brain's free tier is rate-limited too.
# One request at a time for each keeps both well inside the limits.
_speech_lock = threading.Lock()
_brain_lock = threading.Lock()
_brain: Optional[Brain] = None


def brain() -> Brain:
    global _brain
    if _brain is None:
        _brain = Brain.from_env()
    return _brain


@app.get("/health")
def health():
    return {"status": "HEALTHY", "service": "ecfd-lab"}


# ------------------------------------------------------------------ Test 0 (brain)

@app.get("/test0")
def test0_results():
    model = brain().provider.model
    items = t0.load_items()["items"]
    store = t0.load_store(model)
    scored = {i["id"]: t0.score_item(i, store["answers"][i["id"]]) for i in items if i["id"] in store["answers"]}
    return {
        "model": model,
        "prompt_version": t0.prompt_version(),
        "items": items,
        "results": scored,
        "summary": t0.summarize(list(scored.values()), len(items)),
    }


@app.post("/test0/run/{item_id}")
def test0_run(item_id: str, fresh: bool = False):
    items = {i["id"]: i for i in t0.load_items()["items"]}
    if item_id not in items:
        raise HTTPException(404, f"Unknown sentence {item_id}")
    item = items[item_id]
    with _brain_lock:
        b = brain()
        store = t0.load_store(b.provider.model)
        if fresh or item_id not in store["answers"]:
            try:
                result = b.classify(item["text"], item.get("context", []))
            except RuntimeError as e:
                raise HTTPException(502, str(e)[:300])
            store["answers"][item_id] = {"raw": result.raw, "latency_ms": result.latency_ms}
            t0.save_store(b.provider.model, store)
    return t0.score_item(item, store["answers"][item_id])


class ClassifyRequest(BaseModel):
    text: str
    context: List[str] = []


@app.post("/brain/classify")
def brain_classify(req: ClassifyRequest):
    """Try any sentence. Not stored - this is a playground, not a test."""
    if not req.text.strip():
        raise HTTPException(400, "Text is required")
    with _brain_lock:
        try:
            r = brain().classify(req.text, req.context[-2:])
        except RuntimeError as e:
            raise HTTPException(502, str(e)[:300])
    return {
        "model": r.model,
        "latency_ms": r.latency_ms,
        "needs_more_context": r.needs_more_context,
        "tactics": [{"label": t.label, "confidence": t.confidence, "quote": t.quote} for t in r.tactics],
        "dropped": [{"label": t.label, "confidence": t.confidence, "quote": t.quote, "reason": why} for t, why in r.dropped],
    }


# ------------------------------------------------------------------ Test 1 (speech)

def _engine():
    try:
        return t1.make_engine("speechmatics")
    except RuntimeError as e:
        raise HTTPException(503, str(e))


def _row(engine_cache: dict, clips: dict, groups: dict, path: Path) -> Optional[dict]:
    m = t1.FILE_PATTERN.match(path.name)
    if not m or m.group(1).upper() not in clips:
        return None
    clip_id, speaker = m.group(1).upper(), m.group(2)
    cached = engine_cache.get(path.name)
    row = {"clip_id": clip_id, "speaker": speaker, "file": path.name, "transcribed": cached is not None}
    if cached:
        row.update({"transcript": cached["transcript"], "seconds": cached.get("seconds"),
                    **t1.score_clip(clips[clip_id], groups, cached["transcript"])})
    return row


@app.get("/test1")
def test1_results(speakers: Optional[str] = None):
    key = t1.load_answer_key()
    clips = {c["id"]: c for c in key["clips"]}
    engine = _engine()
    cache = t1.load_cache(engine)
    wanted = {s.strip().lower() for s in speakers.split(",")} if speakers else None
    files = sorted(RECORDINGS.glob("T1_*")) if RECORDINGS.exists() else []
    rows = [r for r in (_row(cache, clips, key["keyword_groups"], p) for p in files) if r]
    all_speakers = sorted({r["speaker"] for r in rows}, key=str.lower)
    if wanted:
        rows = [r for r in rows if r["speaker"].lower() in wanted]
    scored = [r for r in rows if r["transcribed"]]
    return {
        "engine": engine.describe(),
        "config": t1.config_hash(engine),
        "clips": key["clips"],
        "keyword_groups": key["keyword_groups"],
        "speakers": all_speakers,
        "rows": rows,
        "summary": t1.summarize(scored),
    }


@app.post("/test1/record")
def test1_record(speaker: str = Form(...), clip_id: str = Form(...), audio: UploadFile = File(...)):
    """Save a browser recording as recordings/T1_<clip>_<speaker>.webm, transcribe it, score it."""
    clip_id = clip_id.upper()
    key = t1.load_answer_key()
    clips = {c["id"]: c for c in key["clips"]}
    if clip_id not in clips:
        raise HTTPException(404, f"Unknown clip {clip_id}")
    if not NAME_OK.match(speaker):
        raise HTTPException(400, "Speaker name: letters and digits only (e.g. Ahmed)")
    data = audio.file.read()
    if len(data) < 2000:
        raise HTTPException(400, "Recording is empty or too short")
    if len(data) > 10 * 1024 * 1024:
        raise HTTPException(400, "Recording too large")

    ext = "ogg" if "ogg" in (audio.content_type or "") else "webm"
    RECORDINGS.mkdir(exist_ok=True)
    # one file per clip and speaker: remove an older take in any format
    for old in RECORDINGS.glob(f"T1_{clip_id}_{speaker}.*"):
        old.unlink()
    path = RECORDINGS / f"T1_{clip_id}_{speaker}.{ext}"
    path.write_bytes(data)

    engine = _engine()
    with _speech_lock:
        cache = t1.load_cache(engine)
        t_start = time.time()
        try:
            cached = t1.transcribe_cached(engine, path, cache)
        except RuntimeError as e:
            raise HTTPException(502, f"Speechmatics failed: {str(e)[:250]}")
    row = _row({path.name: cached}, clips, key["keyword_groups"], path)
    row["engine_ms"] = int((time.time() - t_start) * 1000)
    return row


# ------------------------------------------------------------------ simulated call (mic -> full pipeline)

_call_lock = asyncio.Lock()  # free Speechmatics plan: one live call at a time


@app.websocket("/call/ws")
async def call_ws(ws: WebSocket):
    """Browser streams 16-bit PCM at 8 kHz; events (live words, turns, tactics, stage, score, alerts) come back."""
    await ws.accept()
    if _call_lock.locked():
        await ws.send_text(json.dumps({"type": "error", "msg": "Another simulated call is running. Try again when it ends."}))
        await ws.close()
        return
    async with _call_lock:
        try:
            engine = t1.make_engine("speechmatics")
            session = CallSession(ws, engine.url, engine.api_key, engine.config(), brain(), CallTracker(), t1.mulaw_encode)
            await session.run()
        except WebSocketDisconnect:
            pass
        except Exception as e:  # report to the screen instead of a silent drop
            try:
                await ws.send_text(json.dumps({"type": "error", "msg": str(e)[:300]}))
            except Exception:
                pass
        finally:
            try:
                await ws.close()
            except Exception:
                pass


@app.get("/test1/audio/{filename}")
def test1_audio(filename: str):
    if not t1.FILE_PATTERN.match(filename):
        raise HTTPException(400, "Bad file name")
    path = RECORDINGS / filename
    if not path.exists():
        raise HTTPException(404, "Not found")
    return FileResponse(path)
