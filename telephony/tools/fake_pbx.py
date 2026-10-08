"""Fake PBX - pretends to be Asterisk's ARI so ECFD's telephony path can be tested without a phone system.

It announces one answered call between 1002 (caller) and 1001 (employee). When ECFD taps a side
(snoop + External Media + bridge, exactly the ARI calls it makes against the real Asterisk), the script
streams that side's audio to ECFD the way External Media does: RTP, slin16, 20 ms packets, big-endian,
in real time. Both sides send continuously (silence while the other person talks), like a real call.
When the last turn has been played it hangs the call up (ChannelDestroyed).

Run (repo root), then start the backend with telephony pointed at it:
  ml/asr/.venv/Scripts/python telephony/tools/fake_pbx.py
  ml/asr/.venv/Scripts/python telephony/tools/fake_pbx.py --turn CALLER:a.wav --turn EMPLOYEE:b.m4a --turn CALLER:c.wav

  dotnet run --project backend/ECFD.Api/ECFD.Api.csproj --urls http://localhost:5000 \
      --MlServices:UseMocks=false --Asterisk:Enabled=true \
      --Asterisk:AriUrl=http://localhost:8088/ari --Asterisk:MediaHost=127.0.0.1

The call starts a few seconds after ECFD connects, so open the dashboard first.
"""

import argparse
import asyncio
import socket
import struct
import sys
from pathlib import Path

import av
import numpy as np
import uvicorn
from fastapi import FastAPI, Request, Response, WebSocket, WebSocketDisconnect

ROOT = Path(__file__).resolve().parents[2]
RATE = 16000
FRAME = RATE // 50          # 20 ms = 320 samples
PAUSE_BETWEEN_TURNS = 0.8   # seconds of silence between speakers
LEGS = {
    "CALLER": {"id": "fake-caller-1", "name": "PJSIP/1002-00000001"},
    "EMPLOYEE": {"id": "fake-employee-1", "name": "PJSIP/1001-00000002"},
}

app = FastAPI(title="Fake Asterisk ARI")
state = {"snoops": {}, "media": {}, "tapped": {}, "turns": [], "call_started": False}


def load_pcm(path: str) -> np.ndarray:
    """Any audio file -> 16 kHz mono int16."""
    resampler = av.AudioResampler(format="s16", layout="mono", rate=RATE)
    chunks = []
    with av.open(path) as container:
        for frame in container.decode(audio=0):
            chunks += [f.to_ndarray().reshape(-1) for f in resampler.resample(frame)]
        chunks += [f.to_ndarray().reshape(-1) for f in resampler.resample(None)]
    return np.concatenate(chunks).astype(np.int16) if chunks else np.zeros(0, np.int16)


def build_tracks(turns: list) -> dict:
    """Two time-aligned tracks: each speaker's turns, silence while the other one talks."""
    tracks = {"CALLER": [], "EMPLOYEE": []}
    pause = np.zeros(int(PAUSE_BETWEEN_TURNS * RATE), np.int16)
    for speaker, pcm in turns:
        other = "EMPLOYEE" if speaker == "CALLER" else "CALLER"
        tracks[speaker] += [pause, pcm]
        tracks[other] += [pause, np.zeros(len(pcm), np.int16)]
    tail = np.zeros(int(1.5 * RATE), np.int16)
    out = {}
    for leg, parts in tracks.items():
        audio = np.concatenate(parts + [tail])
        audio = np.concatenate([audio, np.zeros((-len(audio)) % FRAME, np.int16)])
        audio += np.random.default_rng(1).integers(-40, 41, len(audio)).astype(np.int16)  # faint line noise
        out[leg] = audio
    return out


async def stream_call(ws: WebSocket):
    tracks = build_tracks(state["turns"])
    socks = {}
    for leg, host in state["tapped"].items():
        h, p = host.rsplit(":", 1)
        socks[leg] = (socket.socket(socket.AF_INET, socket.SOCK_DGRAM), (h, int(p)))
    print(f"streaming {len(tracks['CALLER']) / RATE:.1f} s of call audio to {dict(state['tapped'])}")

    loop = asyncio.get_running_loop()
    start = loop.time()
    seq, ts = 0, 0
    for i in range(0, len(tracks["CALLER"]), FRAME):
        for leg, (sock, addr) in socks.items():
            payload = tracks[leg][i:i + FRAME].astype(">i2").tobytes()  # slin16 on the wire is big-endian
            header = struct.pack("!BBHII", 0x80, 118, seq & 0xFFFF, ts & 0xFFFFFFFF, 0x1234 if leg == "CALLER" else 0x5678)
            sock.sendto(header + payload, addr)
        seq += 1
        ts += FRAME
        await asyncio.sleep(max(0.0, start + seq * 0.02 - loop.time()))  # real-time pacing

    for leg in ("EMPLOYEE", "CALLER"):
        await ws.send_json({"type": "ChannelDestroyed", "channel": LEGS[leg]})
    print("call hung up")


@app.websocket("/ari/events")
async def events(ws: WebSocket):
    await ws.accept()
    print(f"ECFD connected to the fake ARI ({ws.query_params.get('app')})")
    if state["call_started"]:
        await ws.receive_text()  # keep later connections open, idle
        return
    state["call_started"] = True
    try:
        await asyncio.sleep(3)
        await ws.send_json({"type": "Dial", "dialstatus": "ANSWER",
                            "caller": {**LEGS["CALLER"], "state": "Up"}, "peer": {**LEGS["EMPLOYEE"], "state": "Up"}})
        print("call answered: 1002 -> 1001")
        for _ in range(100):  # wait until ECFD has tapped both sides
            if len(state["tapped"]) == 2:
                break
            await asyncio.sleep(0.1)
        if len(state["tapped"]) < 2:
            print(f"only {len(state['tapped'])} side(s) were tapped - streaming what we have")
        await stream_call(ws)
        while True:
            await ws.receive_text()
    except WebSocketDisconnect:
        print("ECFD disconnected")


@app.post("/ari/channels/{channel_id}/snoop")
async def snoop(channel_id: str, request: Request):
    snoop_id = request.query_params["snoopId"]
    state["snoops"][snoop_id] = channel_id
    print(f"  snoop on {channel_id} (spy={request.query_params.get('spy')})")
    return {"id": snoop_id, "name": f"Snoop/{channel_id}-00000001", "state": "Up"}


@app.post("/ari/channels/{channel_id}/play")
async def play(channel_id: str, request: Request):
    # the real Asterisk would speak this into the employee's ear through the whisper snoop
    print(f"  >> employee hears: {request.query_params.get('media')} (via {channel_id})")
    return {"id": request.query_params.get("playbackId"), "state": "queued"}


@app.post("/ari/channels/externalMedia")
async def external_media(request: Request):
    q = request.query_params
    state["media"][q["channelId"]] = q["external_host"]
    print(f"  external media -> {q['external_host']} ({q.get('format')})")
    return {"id": q["channelId"], "name": "UnicastRTP/127.0.0.1:5000-00000001", "state": "Up"}


@app.post("/ari/bridges")
async def bridge():
    return {"id": "bridge", "bridge_type": "mixing"}


@app.post("/ari/bridges/{bridge_id}/addChannel")
async def add_channel(bridge_id: str, request: Request):
    ids = request.query_params["channel"].split(",")
    channel = next((state["snoops"][i] for i in ids if i in state["snoops"]), None)
    host = next((state["media"][i] for i in ids if i in state["media"]), None)
    leg = next((k for k, v in LEGS.items() if v["id"] == channel), None)
    if leg and host:
        state["tapped"][leg] = host
        print(f"  tapped {leg} -> {host}")
    return Response(status_code=204)


@app.delete("/ari/channels/{channel_id}")
async def hangup(channel_id: str):
    return Response(status_code=204)


@app.delete("/ari/bridges/{bridge_id}")
async def destroy_bridge(bridge_id: str):
    return Response(status_code=204)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--turn", action="append", metavar="SPEAKER:FILE",
                        help="CALLER:path or EMPLOYEE:path, in call order (default: Test 1 clips)")
    parser.add_argument("--port", type=int, default=8088)
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    rec = ROOT / "ml" / "asr" / "test1" / "recordings"
    turns = args.turn or [f"CALLER:{rec / 'T1_C01_Ahmed.wav'}", f"EMPLOYEE:{rec / 'T1_C03_Ahmed.wav'}",
                          f"CALLER:{rec / 'T1_C02_Ahmed.wav'}"]
    for t in turns:
        speaker, path = t.split(":", 1)
        if speaker not in LEGS:
            print(f"speaker must be CALLER or EMPLOYEE: {t}")
            return 1
        state["turns"].append((speaker, load_pcm(path)))
        print(f"turn {len(state['turns'])}: {speaker:<8} {Path(path).name} ({len(state['turns'][-1][1]) / RATE:.1f} s)")

    uvicorn.run(app, host="127.0.0.1", port=args.port, log_level="warning")
    return 0


if __name__ == "__main__":
    sys.exit(main())
